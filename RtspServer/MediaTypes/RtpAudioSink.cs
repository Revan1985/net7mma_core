﻿using Media;
using Media.Codec.Interfaces;
using Media.Codecs.Audio.Alaw;
using Media.Codecs.Audio.Mulaw;
using Media.Common;
using Media.Common.Collections.Generic;
using Media.Common.Extensions.IPEndPoint;
using Media.Common.Extensions.TimeSpan;
using Media.Rtp;
using Media.Sdp;
using Media.Sdp.Lines;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Media.Rtsp.Server.MediaTypes;

public class RtpAudioSink : RtpSink
{
    internal protected readonly ConcurrentLinkedQueueSlim<RtpFrame> Frames = new ConcurrentLinkedQueueSlim<RtpFrame>();

    internal protected int m_FramesSentCounter = 0;

    /// <summary>
    /// The number of channels in this audio sink.
    /// </summary>
    public int Channels { get; protected set; }

    /// <summary>
    /// The clock rate of this audio sink.
    /// </summary>
    public int ClockRate { get; protected set; }

    /// <summary>
    /// The Payload type of this sink
    /// </summary>
    public int PayloadType { get; }

    /// <summary>
    /// The coded used to encode or decode
    /// </summary>
    public ICodec Codec { get; internal protected set; }

    /// <summary>
    /// Creates an audio sink and assigns <see cref="PayloadType"/>, <see cref="Channels"/> and <see cref="ClockRate"/>
    /// </summary>
    /// <param name="name"><inheritdoc/></param>
    /// <param name="source"><inheritdoc/></param>
    /// <param name="payloadType"></param>
    /// <param name="channels"></param>
    /// <param name="clockRate"></param>
    public RtpAudioSink(string name, Uri source, int payloadType, int channels, int clockRate) : base(name, source)
    {
        //SourceId = RFC3550.Random32(PayloadType ^ Channels); //Doesn't really matter what seed was used

        Channels = channels;

        PayloadType = payloadType;

        ClockRate = clockRate;
    }

    /// <summary>
    /// Logic is general enough to go into RtpSink but RtpSink is working with packets right now.
    /// </summary>
    internal override void SendPackets()
    {
        RtpClient.FrameChangedEventsEnabled = false;

        unchecked
        {
            while (State == StreamState.Started)
            {
                try
                {
                    if (Frames.Count == 0 && State == StreamState.Started)
                    {
                        if (RtpClient.IsActive) RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.Lowest;

                        System.Threading.Thread.Sleep(ClockRate / 1000);

                        continue;
                    }

                    //int period = (clockRate * 1000 / m_Frames.Count);

                    //Dequeue a frame or die
                    RtpFrame frame;

                    if (!Frames.TryDequeue(out frame) || IDisposedExtensions.IsNullOrDisposed(frame) || frame.IsEmpty) continue;

                    //Get the transportChannel for the packet
                    RtpClient.TransportContext transportContext = RtpClient.GetContextBySourceId(frame.SynchronizationSourceIdentifier);

                    //If there is a context
                    if (transportContext != null)
                    {
                        //Increase priority
                        RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.AboveNormal;

                        transportContext.RtpTimestamp += ClockRate;

                        frame.Timestamp = transportContext.RtpTimestamp;

                        //Fire a frame changed event manually
                        if (RtpClient.FrameChangedEventsEnabled) RtpClient.OnRtpFrameChanged(frame, transportContext, true);

                        //Take all the packet from the frame                            
                        IEnumerable<RtpPacket> packets = frame;

                        if (Loop) frame = new RtpFrame();

                        //Iterate each packet in the frame
                        foreach (RtpPacket packet in packets)
                        {
                            //Copy the values before we signal the server
                            //packet.Channel = transportContext.DataChannel;
                            packet.SynchronizationSourceIdentifier = SourceId;

                            packet.Timestamp = transportContext.RtpTimestamp;

                            //Assign next sequence number
                            switch (transportContext.RecieveSequenceNumber)
                            {
                                case ushort.MaxValue:
                                    packet.SequenceNumber = transportContext.RecieveSequenceNumber = 0;
                                    break;
                                //Increment the sequence number on the transportChannel and assign the result to the packet
                                default:
                                    packet.SequenceNumber = ++transportContext.RecieveSequenceNumber;
                                    break;
                            }

                            //Fire an event so the server sends a packet to all clients connected to this source
                            if (false == RtpClient.FrameChangedEventsEnabled) RtpClient.OnRtpPacketReceieved(packet, transportContext);

                            //Put the packet back to ensure the timestamp and other values are correct.
                            if (Loop) frame.Add(packet);

                            //Update the jitter and timestamp
                            transportContext.UpdateJitterAndTimestamp(packet);
                        }

                        packets = null;

                        //Check for if previews should be updated?
                        //if (DecodeFrames)
                        //{
                        //    //Codec.Decoder
                        //}

                        ++m_FramesSentCounter;
                    }

                    //If we are to loop images then add it back at the end
                    if (Loop)
                    {
                        Frames.Enqueue(frame);
                    }
                    else
                    {
                        frame.Dispose();
                    }

                    RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.BelowNormal;

                    System.Threading.Thread.Sleep(ClockRate / 1000);
                }
                catch (Exception ex)
                {
                    if (ex is System.Threading.ThreadAbortException)
                    {
                        //Handle the abort
                        System.Threading.Thread.ResetAbort();

                        Stop();

                        return;
                    }
                    
                    continue;
                }
            }
        }
    }

    /// <summary>
    /// Implements creating the <see cref="RtpClient"/> and <see cref="SessionDescription"/> required for this audio sink.
    /// </summary>
    public override void Start()
    {
        if (RtpClient != null) return;

        //Create a RtpClient so events can be sourced from the Server to many clients without this Client knowing about all participants
        //If this class was used to send directly to one person it would be setup with the recievers address
        RtpClient = new RtpClient();

        SessionDescription = new SessionDescription(0, "v√ƒ", Name);
        SessionDescription.Add(new SessionConnectionLine()
        {
            ConnectionNetworkType = SessionConnectionLine.InConnectionToken,
            ConnectionAddressType = SessionDescription.WildcardString,
            ConnectionAddress = System.Net.IPAddress.Any.ToString()
        });

        //Add a MediaDescription to our Sdp on any available port for RTP/AVP Transport using the PayloadType            
        var mediaDescription = new MediaDescription(MediaType.audio,
            RtpClient.RtpAvpProfileIdentifier,  //Any port...
            PayloadType,
            0);
        SessionDescription.Add(mediaDescription);

        //Indicate control to each media description contained
        SessionDescription.Add(new SessionDescriptionLine("a=control:*"));

        //Ensure the session members know they can only receive
        SessionDescription.Add(new SessionDescriptionLine("a=sendonly")); //recvonly?

        //that this a broadcast.
        SessionDescription.Add(new SessionDescriptionLine("a=type:broadcast"));


        //Add a Interleave (We are not sending Rtcp Packets becaues the Server is doing that) We would use that if we wanted to use this AudioStream without the server.            
        //See the notes about having a Generic.Dictionary to support various tracks

        //Create a context
        RtpClient.TryAddContext(new RtpClient.TransportContext(
            0, //Data Channel
            1, //Control Channel
            RFC3550.Random32(PayloadType), //A randomId which was alredy generated 
            mediaDescription, //This is the media description we just created.
            false, //Don't enable Rtcp reports because this source doesn't communicate with any clients
            SourceId, // This context is not in discovery
            0,
            true)
        {
            //Never has to send
            SendInterval = TimeSpanExtensions.InfiniteTimeSpan,
            //Never has to recieve
            ReceiveInterval = TimeSpanExtensions.InfiniteTimeSpan,
            //Assign a LocalRtp so IsActive is true
            LocalRtp = IPEndPointExtensions.Any,
            //Assign a RemoteRtp so IsActive is true
            RemoteRtp = IPEndPointExtensions.Any
        }); //This context is always valid from the first rtp packet received

        //Add the control line, could be anything... this indicates the URI which will appear in the SETUP and PLAY commands
        mediaDescription.Add(new SessionDescriptionLine("a=control:trackID=audio"));

        //Make the thread
        RtpClient.m_WorkerThread = new System.Threading.Thread(SendPackets);
        RtpClient.m_WorkerThread.TrySetApartmentState(System.Threading.ApartmentState.MTA);
        //RtpClient.m_WorkerThread.IsBackground = true;
        //RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.BelowNormal;
        RtpClient.m_WorkerThread.Name = nameof(RtpAudioSink) + "-" + Id;
        IsReady = true;
        State = StreamState.Started;
        RtpClient.m_WorkerThread.Start();

        //Finally the state is set to Started so the stream can be consumed
        base.Start();
    }

    /// <summary>
    /// Stops the audio stink
    /// </summary>
    public override void Stop()
    {
        base.Stop();

        Frames.Clear();

        SessionDescription = null;
    }

    /// <summary>
    /// Todo, IEncoder and IDecoder need to expose methods for this to work without a bunch of if else statements.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public bool Packetize(byte[] data, int offset, int length)
    {
        //Get the context for the payloadType so we can increment the timestamps and sequence numbers.
        var transportContext = RtpClient.GetContextBySourceId(SourceId);

        transportContext.RtpTimestamp += ClockRate;

        //Create a frame
        RtpFrame newFrame = new RtpFrame();

        //Create the packet
        RtpPacket newPacket = new RtpPacket(length / 2 + RtpHeader.Length)
        {
            SynchronizationSourceIdentifier = SourceId,
            Timestamp = transportContext.RtpTimestamp,
            PayloadType = PayloadType,
            Marker = true,
        };

        //Assign next sequence number
        switch (transportContext.RecieveSequenceNumber)
        {
            case ushort.MaxValue:
                newPacket.SequenceNumber = transportContext.RecieveSequenceNumber = 0;
                break;
            //Increment the sequence number on the transportChannel and assign the result to the packet
            default:
                newPacket.SequenceNumber = ++transportContext.RecieveSequenceNumber;
                break;
        }

        //Add the packet to the frame
        newFrame.Add(newPacket);

        //Loop all samples and put the [i]nput bytes into the encoder and [o]utput byte into the Payload

        if (Codec is ALawCodec)
        {
            for (int i = offset, o = 0; i < length; i += 2)
            {
                newPacket.Payload[o++] = ALawEncoder.LinearToALawSample(Common.Binary.Read16(data, i, System.BitConverter.IsLittleEndian));
            }
        }
        else
        {
            for (int i = offset, o = 0; i < length; i += 2)
            {
                newPacket.Payload[o++] = MuLawEncoder.LinearToMuLawSample(Common.Binary.Read16(data, i, System.BitConverter.IsLittleEndian));
            }
        }

        //Return the value indicating if the frame was queued.
        return Frames.TryEnqueue(ref newFrame);
    }
}
