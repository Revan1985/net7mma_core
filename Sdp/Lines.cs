﻿/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://github.com/juliusfriedman/net7mma_core
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. https://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Media.Sdp
{
    namespace /*Media.Sdp.*/Lines
    {
        /// <summary>
        /// Represents an 'a=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionAttributeLine : SessionDescriptionLine
        {
            internal const char AttributeType = 'a';

            /// <summary>
            /// Gets the parts of the attribute such as the name and value / type.
            /// </summary>
            public IEnumerable<string> AttributeParts
            {
                get
                {
                    if (m_AttributeParts is not null) return m_AttributeParts;

                    //Todo, should be contigious to all derived parts which do not occur within m_Parts
                    m_AttributeParts = new string[2];

                    Common.Extensions.String.StringExtensions.SplitTrim(GetPart(0), SessionDescription.ColonSplit, 2, StringSplitOptions.RemoveEmptyEntries).CopyTo(m_AttributeParts, 0);

                    return m_AttributeParts; //= Common.Extensions.String.StringExtensions.SplitTrim(GetPart(0), SessionDescription.ColonSplit, 2, StringSplitOptions.RemoveEmptyEntries);
                }
            }

            private string[] m_AttributeParts;

            /// <summary>
            /// 
            /// </summary>
            public string AttributeName
            {

                get
                {
                    return AttributeParts.FirstOrDefault();
                }

                protected set
                {
                    if (value.Equals(AttributeName, StringComparison.OrdinalIgnoreCase)) return;

                    m_AttributeParts[0] = value;

                    //Todo, Concat
                    //Bug with ToString()
                    SetPart(0, string.Join(HasAttributeValue ? SessionDescription.ColonString : string.Empty, m_AttributeParts));
                }

                //get
                //{
                //    return GetPart(0).Split(SessionDescription.Colon)[0].Trim();
                //}
                //protected set
                //{
                //    SetPart(0, string.Join(false == string.IsNullOrEmpty(value) && value.EndsWith(SessionDescription.ColonString) ? string.Empty : SessionDescription.ColonString, value, string.Empty));
                //}
            }

            /// <summary>
            /// Indicates if the <see cref="AttributeValue"/> is present
            /// </summary>
            public bool HasAttributeValue
            {
                get { return AttributeParts.Count() > 1; }
            }

            /// <summary>
            /// Get the value which occurs after the <see cref="AttributeName"/>
            /// </summary>
            public string AttributeValue
            {
                get { return AttributeParts.Skip(1).FirstOrDefault(); }
                protected set
                {
                    if (value.Equals(AttributeValue, StringComparison.OrdinalIgnoreCase)) return;

                    //byte-string
                    // 1*(%x01-09/%x0B-0C/%x0E-FF)

                    m_AttributeParts[1] = value;

                    SetPart(0, string.Join(SessionDescription.ColonString, m_AttributeParts));
                }
            }

            public SessionAttributeLine(SessionDescriptionLine line)
                : base(line)
            {
                if (false.Equals(m_Type.Equals(AttributeType))) throw new InvalidOperationException("Not a SessionAttributeLine line");
            }


            //Assuming given a value NOT a line text to parse...

            //Todo, should just add 2 parts, ':' and value...

            public SessionAttributeLine(string seperator = null, int partCount = 2,
                string attributeName = Common.Extensions.String.StringExtensions.UnknownString,
                string attributeValue = null)
                : base(AttributeType, seperator, partCount)
            {
                //If any parts are expected
                if (partCount > 0 && string.IsNullOrWhiteSpace(attributeName) is false)
                {
                    //(AttributeName)
                    //SetPart(0, attributeName);
                    SetPart(0, string.Concat(attributeName, SessionDescription.ColonString));

                    //If there is any value
                    if (partCount > 1 && string.IsNullOrWhiteSpace(attributeValue) is false)
                    {
                        int reduce = 0;

                        int valueLength = attributeValue.Length;

                        if (valueLength >= 1)
                        {
                            if (attributeValue[0].Equals(AttributeType)) ++reduce;

                            if (valueLength >= 2 && attributeValue[1].Equals(SessionDescription.EqualsSign)) ++reduce;

                            if (reduce > 0) attributeValue = attributeValue.Substring(reduce);
                        }

                        //(AttributeValue)
                        SetPart(1, attributeValue);
                    }
                }
            }

            public SessionAttributeLine(string attributeName, string attributeValue)
                : this(null, 2, attributeName, attributeValue)
            {
            }

            //Should have params values overload with optional seperator(s)

            public SessionAttributeLine(string[] sdpLines, ref int index, string seperator = null, int partCount = 1)
                : base(sdpLines, ref index, seperator, AttributeType, partCount)
            {

            }

            //If going to use SpaceString then should offer a property to get parts for dervived types which wish to use ':'
            //E.g. KeyValuePair<string, string> GetValues

            //Should check or charset or sdpland attribute and switch currentEncoding. ( would be a compositive line then)

            //String Equals gets messy here
            //public override string ToString()
            //{
            //    SetPart(0, string.Join(HasAttributeValue ? SessionDescription.ColonString : string.Empty, m_AttributeParts));

            //    return ToString();
            //}

        }

        //ControlLine

        //RangeLine

        //SdpLangAttribute a=sdplang:<language tag>

        //CharsetAttribute  a=charset:<character set>

        //LangAttribute a=lang:<language tag>

        //EncryptionTypeLine k=

        //https://tools.ietf.org/html/rfc4566 @ 5.8.  Bandwidth ("b=")
        /// <summary>
        /// Represents an 'b=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionBandwidthLine : SessionDescriptionLine
        {
            #region Statics

            /// <summary>
            /// Used to indicate disabled.
            /// </summary>
            private const int DisabledValue = 0;

            //Types registered with IANA
            private const string RecieveBandwidthToken = "RR", SendBandwdithToken = "RS", ApplicationSpecificBandwidthToken = "AS", ConferenceTotalBandwidthToken = "CT",
                IndependentApplicationSpecificBandwidthToken = "TIAS"; //https://tools.ietf.org/html/rfc3890

            //const string BandwidthFormat = "b={0}:{1}";

            //Calling Add will change the values of these lines. (Add is internal)
            //Must enforce with IUpdateable which is always under modification.

            public static readonly Sdp.SessionDescriptionLine DisabledReceiveLine = SessionBandwidthLine.Disabled(RecieveBandwidthToken);//new Sdp.SessionDescriptionLine(string.Format(BandwidthFormat, RecieveBandwidthToken));

            public static readonly Sdp.SessionDescriptionLine DisabledSendLine = SessionBandwidthLine.Disabled(SendBandwdithToken);//new Sdp.SessionDescriptionLine(string.Format(BandwidthFormat, SendBandwdithToken));

            public static readonly Sdp.SessionDescriptionLine DisabledApplicationSpecificLine = SessionBandwidthLine.Disabled(ApplicationSpecificBandwidthToken); //new Sdp.SessionDescriptionLine(string.Format(BandwidthFormat, ApplicationSpecificBandwidthToken));

            public static readonly Sdp.SessionDescriptionLine DisabledConferenceTotalLine = SessionBandwidthLine.Disabled(ConferenceTotalBandwidthToken); //new Sdp.SessionDescriptionLine(string.Format(BandwidthFormat, ConferenceTotalBandwidthToken));

            public static readonly Sdp.SessionDescriptionLine DisabledIndependentApplicationSpecificLine = SessionBandwidthLine.Disabled(IndependentApplicationSpecificBandwidthToken);//new Sdp.SessionDescriptionLine(string.Format(BandwidthFormat, IndependentApplicationSpecificBandwidthToken));

            public static SessionBandwidthLine Disabled(string token)
            {
                return new SessionBandwidthLine(token, DisabledValue);
            }

            public static bool TryParseBandwidthLine(Media.Sdp.SessionDescriptionLine line, out int result)
            {

                return TryParseBandwidthLine(line, out string token, out result);
            }

            public static bool TryParseBandwidthLine(Media.Sdp.SessionDescriptionLine line, out string token, out int result)
            {
                token = string.Empty;

                result = -1;

                if (line is null || line.m_Type != Sdp.Lines.SessionBandwidthLine.BandwidthType || line.m_Parts.Count <= 0) return false;

                string[] tokens = line.m_Parts[0].Split(Media.Sdp.SessionDescription.ColonSplit, StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length < 2) return false;

                token = tokens[0];

                return int.TryParse(tokens[1], out result);
            }

            //Todo, this should not be required or should be an extension method of the MediaDescription Type
            public static bool TryParseBandwidthDirectives(Media.Sdp.MediaDescription mediaDescription, out int rrDirective, out int rsDirective, out int asDirective) //tiasDirective
            {
                rrDirective = rsDirective = asDirective = -1;

                if (Common.IDisposedExtensions.IsNullOrDisposed(mediaDescription)) return false;

                int parsed = -1;

                string token = string.Empty;

                //Iterate all BandwidthLines
                foreach (Media.Sdp.SessionDescriptionLine line in mediaDescription.BandwidthLines)
                {
                    //If the line was parsed as a bandwidth line then determine the type of bandwidth line parsed and assign the value of the directive.
                    if (TryParseBandwidthLine(line, out token, out parsed))
                    {

                        //Anyparsed = true;

                        switch (token)
                        {
                            case RecieveBandwidthToken:
                                rrDirective = parsed;
                                continue;
                            case SendBandwdithToken:
                                rsDirective = parsed;
                                continue;
                            case IndependentApplicationSpecificBandwidthToken: //should have it's own out value...
                            case ApplicationSpecificBandwidthToken:
                                asDirective = parsed;
                                continue;

                        }
                    }
                }

                //should actually be returning true if any bandwidth tokens were parsed... Anyparsed...

                //Determine if rtcp is disabled
                return parsed >= 0;
            }

            //Overload for SessionDescription (which can fallback to use the MediaDescription overload above)
            //Because there can also be a Bandwidth attribute at a the session level.

            #endregion

            internal const char BandwidthType = 'b';

            #region Properties

            /// <summary>
            /// Gets the bwtype token.
            /// </summary>
            public string BandwidthTypeString
            {
                get { return GetPart(0); }
                //set { SetPart(0, value); }
            }

            /// <summary>
            /// Gets the bandwidth token
            /// </summary>
            public string BandwidthValueString
            {
                get { return GetPart(1); }
                //set { SetPart(1, value); }
            }

            /// <summary>
            /// Parses the <see cref="BandwidthValueString"/>
            /// </summary>
            public long BandwidthValue
            {
                get { return long.Parse(GetPart(1)); }
                //set { SetPart(1, value.ToString()); }
            }

            /// <summary>
            /// Indicates if the <see cref="BandwidthValue"/> is equal to 0
            /// </summary>
            public bool IsDisabled { get { return BandwidthValue == DisabledValue; } }

            #endregion

            #region Constructor

            //b=X-YZ:128

            public SessionBandwidthLine(SessionDescriptionLine line)
                : base(line)
            {
                if (m_Type != BandwidthType) throw new InvalidOperationException("Not a SessionBandwidthLine line");

                //Parts will probably not be parsed on ':'
                //Must reset and use the first line parts...

                //m_Parts.Clear();
            }

            public SessionBandwidthLine(string token, int value)
                : base(BandwidthType, SessionDescription.Colon.ToString())
            {
                Add(token);

                Add(value.ToString());
            }

            public SessionBandwidthLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.Colon.ToString(), BandwidthType) { }

            #endregion

            //KeyValuePair string t,int b ^
        }

        //Todo, Redo
        /// <summary>
        /// Represents an 'c=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionConnectionLine : SessionDescriptionLine
        {
            /*
             
            Multiple addresses or "c=" lines MAY be specified on a per-media
            basis only if they provide multicast addresses for different layers
            in a hierarchical or layered encoding scheme.  
             * They MUST NOT be specified for a session-level "c=" field.
             */

            public const string InConnectionToken = "IN";

            //Should be moved when defined

            //IANA https://www.iana.org/assignments/sdp-parameters/sdp-parameters.xhtml

            //Proto

            //NetType

            //AddrType

            public const string IP6 = "IP6";

            public const string IP4 = "IP4";

            internal const char ConnectionType = 'c';

            /// <summary>
            /// Gets the string value associated with the nettype token.
            /// </summary>
            public string ConnectionNetworkType
            {
                get { return GetPart(0); }
                protected internal set
                {
                    if (string.IsNullOrWhiteSpace(value)) throw new System.InvalidOperationException("Cannot be null or consist only of whitespace.");  //Todo, Exceptions in resources.
                    SetPart(0, value);
                }
            }

            /// <summary>
            /// Gets the string value associated with the addrtype token.
            /// </summary>
            public string ConnectionAddressType
            {
                get { return GetPart(1); }
                protected internal set
                {
                    if (string.IsNullOrWhiteSpace(value)) throw new System.InvalidOperationException("Cannot be null or consist only of whitespace.");  //Todo, Exceptions in resources.
                    SetPart(1, value);
                }
            }

            /// <summary>
            /// Gets the string value associated with the connection-address token, See 5.7.  Connection Data ("c=")
            /// </summary>
            /// <remarks>
            /// This may be a DNS fully qualified domain
            /// </remarks>
            public string ConnectionAddress
            {
                get { return GetPart(2); }
                protected internal set
                {
                    if (string.IsNullOrWhiteSpace(value)) throw new System.InvalidOperationException("Cannot be null or consist only of whitespace.");  //Todo, Exceptions in resources.

                    SetPart(2, value);

                    m_ConnectionParts = null;
                }
            }

            /// <summary>
            /// Indicates if the '/' character is present. The slash notation for multiple addresses MUST NOT be used for IP unicast addresses.
            /// </summary>
            public bool HasMultipleAddresses
            {
                get
                {
                    return ConnectionParts.Count() > 2;
                    //return ConnectionAddress.Contains((char)Common.ASCII.ForwardSlash);
                }
            }

            //public bool IsUnicastAddress { get { return false == HasMultipleAddresses; } }

            //Todo
            //Only split the ConnectionAddress one time and cache it
            //These are sub fields of a single datum
            private string[] m_ConnectionParts;

            /// <summary>
            /// Gets the parts of the <see cref="ConnectionAddress"/>
            /// </summary>
            public IEnumerable<string> ConnectionParts
            {
                get
                {
                    if (m_ConnectionParts is not null) return m_ConnectionParts;

                    if (string.IsNullOrWhiteSpace(ConnectionAddress)) return Enumerable.Empty<string>();

                    //Todo, should be contigious to all derived parts which do not occur within m_Parts

                    return m_ConnectionParts = ConnectionAddress.Split(SessionDescription.ForwardSlashSplit, 3);
                }
            }

            /// <summary>
            /// Only the host portion of the <see cref="ConnectionAddress"/>
            /// </summary>
            /// <remarks>
            ///  If no other information is present on the line then it will equal the <see cref="ConnectionAddress"/>.
            /// </remarks>
            public string Host
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(ConnectionAddress)) return null;

                    m_ConnectionParts ??= ConnectionAddress.Split(SessionDescription.ForwardSlashSplit, 3);

                    //Should verify that the string contains a . and is not shorter/longer than x, y...
                    return m_ConnectionParts[0];
                }
            }

            //Uri could help?

            //To obtain the IPAddress you would need to resolve the host if it was domain name, otherwise it would be parsed as an IPAddress.
            //public System.Net.IPAddress Resolve(System.Net.Sockets.AddressFamily family)
            //{
            //    string host = Host;
            //    System.Uri uri;
            //    if (System.Uri.TryCreate(host, UriKind.RelativeOrAbsolute, out uri))
            //    {
            //        if (uri.IsAbsoluteUri)
            //        {
            //            System.Net.Dns.GetHostAddresses(uri.DnsSafeHost).FirstOrDefault(l => l.AddressFamily == family);
            //        }
            //        throw new InvalidOperationException("Can't parse a relative Uri for a IPAddress");
            //    }
            //    return IPAddress.Parse(host);
            //}

            /// <summary>
            /// Indicates if the <see cref="ConnectionAddress"/> contains a Time to Live token.
            /// </summary>
            public bool HasTimeToLive
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(ConnectionAddress)) return false;

                    m_ConnectionParts ??= ConnectionAddress.Split(SessionDescription.ForwardSlashSplit, 3);

                    return m_ConnectionParts.Length > 1;
                }
            }

            /// <summary>
            /// Parses the Time To Live token as found in the <see cref="ConnectionAddress"/>, if not found 0 is returned.
            /// </summary>
            public int TimeToLive
            {
                get
                {
                    //Should not be present for IPv6 Addresses
                    if (string.IsNullOrWhiteSpace(ConnectionAddress))
                        return 0;

                    m_ConnectionParts ??= ConnectionAddress.Split(SessionDescription.ForwardSlashSplit, 3);

                    if (m_ConnectionParts.Length > 1)
                    {
                        return int.Parse(m_ConnectionParts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
                    }

                    return 0; //Default Ttl
                }
                //Todo, may have IReadOnly
                set
                {
                    //Values of 0 or less mean remove for now..
                    if (value <= 0)
                    {
                        //Remove the token if it exists

                        if (HasTimeToLive) m_ConnectionParts[1] = string.Empty;

                        return;
                    }
                    else if (value > byte.MaxValue) throw new ArgumentOutOfRangeException();

                    //value > 0 <= 255

                    //Make space if requried
                    Array.Resize(ref m_ConnectionParts, 2);

                    //Set the value
                    m_ConnectionParts[1] = value.ToString();
                }
            }

            ///// <summary>
            ///// Indicates if the <see cref="ConnectionAddress"/> contains a ports token.
            ///// </summary>
            //public bool HasMultiplePorts
            //{
            //    get
            //    {
            //        if (string.IsNullOrWhiteSpace(ConnectionAddress)) return false;

            //        if (m_ConnectionParts is null) m_ConnectionParts = ConnectionAddress.Split(SessionDescription.ForwardSlashSplit, 3);

            //        return m_ConnectionParts.Length > 2;
            //    }
            //}

            /// <summary>
            /// Indicates the amount of ports specified by the <see cref="ConnectionAddress"/> or 1 if unspecified.
            /// </summary>
            public int NumberOfAddresses
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(ConnectionAddress))
                        return 1;

                    m_ConnectionParts ??= ConnectionAddress.Split(SessionDescription.ForwardSlashSplit, 3);

                    return m_ConnectionParts.Length > 2
                        ? int.Parse(m_ConnectionParts[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture)
                        : 1;
                }
                //Todo, may have IReadOnly
                set
                {
                    //Values of 0 or less mean remove for now, the getter will return 1
                    if (value <= 0)
                    {
                        //Remove the token if it exists
                        Array.Resize(ref m_ConnectionParts, 2);

                        return;
                    }
                    else if (value > byte.MaxValue) throw new ArgumentOutOfRangeException();

                    //value > 0 <= 255

                    //Make space if requried
                    Array.Resize(ref m_ConnectionParts, 3);

                    //Set the value
                    m_ConnectionParts[2] = value.ToString();
                }
            }

            #region Constructor

            //Todo, constructor with values for Ttl and NumberOfPorts, could call this constructor with them as the connectionAddress..

            public SessionConnectionLine(string connectionNetworkType, string connectionAddressType, string connectionAddress)
                : this()
            {
                ConnectionNetworkType = connectionNetworkType;

                ConnectionAddressType = connectionAddressType;

                ConnectionAddress = connectionAddress;
            }


            public SessionConnectionLine(SessionDescriptionLine line)
                : this()
            {
                if (line.m_Type.Equals(ConnectionType) is false) throw new InvalidOperationException("Not a SessionConnectionLine");

                m_Parts.Clear();

                //Needs to parse with seperator 
                //Currently
                //ToString has logic which prevents this from mattering.
                if (line.m_Parts.Count is 1)
                {
                    string temp = line.m_Parts[0];

                    m_Parts.AddRange(temp.Split(SessionDescription.Space));
                }
                else m_Parts.AddRange(line.m_Parts);

                EnsureParts(3);
            }


            public SessionConnectionLine()
                : base(ConnectionType, SessionDescription.SpaceString, 3)
            {

            }

            public SessionConnectionLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, ConnectionType)
            {

            }

            public SessionConnectionLine(string line)
                : base(line, SessionDescription.SpaceString, 3)
            {
                if (m_Type.Equals(ConnectionType) is false) throw new InvalidOperationException("Not a SessionConnectionLine line");
            }

            #endregion
        }

        /// <summary>
        /// Represents an 'v=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionVersionLine : SessionDescriptionLine
        {
            internal const char VersionType = 'v';

            public SessionVersionLine(SessionDescriptionLine line)
                : base(line)
            {
                if (false.Equals(m_Type.Equals(VersionType))) throw new InvalidOperationException("Not a SessionVersionLine line");
            }

            /// <summary>
            /// The string value of the token on the version line.
            /// </summary>
            public string VersionToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value); }
            }

            /// <summary>
            /// Parses <see cref="VersionToken"/> as an integer
            /// </summary>
            public int Version
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {

                    int.TryParse(VersionToken, out int result);

                    return result;
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    VersionToken = value.ToString();
                }
            }

            public SessionVersionLine(int version)
                : base(VersionType, 1)
            {
                Version = version;
            }

            public SessionVersionLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, VersionType, 1)
            {
            }

        }

        /// <summary>
        /// Represents an 'o=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionOriginLine : SessionDescriptionLine
        {
            internal const char OriginType = 'o';

            public string Username
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value); }
            }

            public string SessionId
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(1); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(1, value); }
            }

            public string VersionToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(2); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(2, value); }
            }

            public long SessionVersion
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    string part = VersionToken;

                    if (string.IsNullOrWhiteSpace(part)) return 0;

                    //Range ...
                    //return (long)double.Parse(part);

                    return part[0].Equals((char)Common.ASCII.HyphenSign) ? long.Parse(part) : (long)ulong.Parse(part);
                }
                //set { VersionToken = value.ToString(); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(2, value.ToString()); }
            }

            public string NetworkType
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(3); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(3, value); }
            }

            public string AddressType
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(4); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(4, value); }
            }

            //UnicastAddress is what it's called

            //Should have a Uri property?

            /// <summary>
            /// The address of the machine from which the session was created. 
            /// For an address type of IP4, this is either:
            /// The fully qualified domain name of the machine or the dotted-
            /// decimal representation of the IP version 4 address of the machine.
            /// For an address type of IP6, this is either:
            /// The fully qualified domain name of the machine or the compressed textual
            /// representation of the IP version 6 address of the machine. 
            /// For both IP4 and IP6, the fully qualified domain name is the form that
            /// SHOULD be given unless this is unavailable, in which case the
            /// globally unique address MAY be substituted.  A local IP address
            /// MUST NOT be used in any context where the SDP description might
            /// leave the scope in which the address is meaningful (for example, a
            /// local address MUST NOT be included in an application-level
            /// referral that might leave the scope).
            /// </summary>
            public string Address
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(5); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(5, value); }
            }

            public SessionOriginLine()
                : base(OriginType, SessionDescription.SpaceString, 6)
            {
                //while (m_Parts.Count < 6) m_Parts.Add(string.Empty);
                //Username = string.Empty;
            }

            public SessionOriginLine(SessionDescriptionLine line)
                : this()
            {
                if (false.Equals(line.m_Type.Equals(OriginType))) throw new InvalidOperationException("Not a SessionOriginLine");

                m_Parts.Clear();

                //Needs to parse with seperator (ToString is hacked up to prevent this from mattering)
                if (line.m_Parts.Count is 1)
                {
                    string temp = line.m_Parts[0];

                    m_Parts.AddRange(temp.Split(SessionDescription.Space));
                }
                else m_Parts.AddRange(line.m_Parts);

                EnsureParts(6);
            }

            //Conflicts with base's concept of string constructor
            public SessionOriginLine(string owner)
                : base(OriginType, SessionDescription.SpaceString, 6)
            {
                if (string.IsNullOrWhiteSpace(owner)) goto EnsureParts;

                //Check for missing o=? (Adds overhead)
                if (false.Equals(owner[0].Equals(OriginType)))
                {
                    m_Parts.Clear();

                    //Replace NewLine? TrimLineValue
                    m_Parts.AddRange(owner.Split(SessionDescription.Space));
                }
                else m_Parts.AddRange(owner.Substring(2).Replace(SessionDescription.NewLineString, string.Empty).Split(SessionDescription.Space));

                EnsureParts:
                EnsureParts(6);
            }

            public SessionOriginLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, OriginType, 6) //,6
            {

            }
        }

        /// <summary>
        /// Represents an 's=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionNameLine : SessionDescriptionLine
        {

            internal const char NameType = 's';

            public string SessionName
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value); }
            }

            public SessionNameLine()
                : base(NameType, 1)
            {

            }

            public SessionNameLine(SessionDescriptionLine line)
                : base(line)
            {
                if (false.Equals(m_Type.Equals(NameType))) throw new InvalidOperationException("Not a SessionNameLine line");
            }

            //Conflicts with base's concept of string constructorm assumes that the param is a value.
            public SessionNameLine(string sessionName)
                : this()
            {
                //Should contain a single space if null or empty.
                if (string.IsNullOrEmpty(sessionName)) sessionName = SessionDescription.SpaceString;

                SessionName = sessionName;
            }

            //FromValue
            //public SessionNameLine(string text)
            //    : base(text, SessionDescription.SpaceString)
            //{
            //    if (Type != NameType) throw new InvalidOperationException("Not a SessionNameLine line");
            //}

            public SessionNameLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, NameType, 1) //this()
            {

            }

        }

        /// <summary>
        /// Represents an 'i=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionInformationLine : SessionDescriptionLine
        {

            internal const char InformationType = 'i';

            public string SessionName
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value); }
            }

            public SessionInformationLine()
                : base(InformationType)
            {

            }

            public SessionInformationLine(SessionDescriptionLine line)
                : base(line)
            {
                if (false.Equals(m_Type.Equals(InformationType))) throw new InvalidOperationException("Not a SessionInformationLine line");
            }

            public SessionInformationLine(string sessionName)
                : this()
            {
                SessionName = sessionName;
            }

            public SessionInformationLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, InformationType) //this()
            {

            }

        }

        /// <summary>
        /// Represents an 'p=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionPhoneNumberLine : SessionDescriptionLine
        {

            internal const char PhoneType = 'p';

            public string PhoneNumber
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value); }
            }

            public SessionPhoneNumberLine()
                : base(PhoneType)
            {

            }

            public SessionPhoneNumberLine(SessionDescriptionLine line)
                : base(line)
            {
                if (false.Equals(m_Type.Equals(PhoneType))) throw new InvalidOperationException("Not a SessionPhoneNumberLine");
            }

            public SessionPhoneNumberLine(string sessionName)
                : this()
            {
                PhoneNumber = sessionName;
            }

            public SessionPhoneNumberLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, PhoneType) //this()
            {

            }
        }

        /// <summary>
        /// Represents an 'e=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionEmailLine : SessionDescriptionLine
        {
            internal const char EmailType = 'e';

            public string Email
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value); }
            }

            #region Constructor

            public SessionEmailLine()
                : base(EmailType)
            {

            }

            public SessionEmailLine(SessionDescriptionLine line)
                : base(line)
            {
                if (m_Type != EmailType) throw new InvalidOperationException("Not a SessionEmailLine line");
            }

            public SessionEmailLine(string sessionName)
                : this()
            {
                Email = sessionName;
            }

            public SessionEmailLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, EmailType)
            {
                //try
                //{
                //    string sdpLine = sdpLines[index++].Trim();

                //    if (sdpLine[0] != EmailType) Media.Common.TaggedExceptionExtensions.RaiseTaggedException(this, "Invalid SessionEmailLine");

                //    sdpLine = SessionDescription.TrimLineValue(sdpLine.Substring(2));

                //    m_Parts.Add(sdpLine);
                //}
                //catch
                //{
                //    throw;
                //}
            }

            #endregion

            //public override string ToString()
            //{
            //    return EmailType.ToString() + Media.Sdp.SessionDescription.EqualsSign + (string.IsNullOrEmpty(Email) ? string.Empty : Email) + SessionDescription.NewLineString;
            //}
        }

        /// <summary>
        /// Represents an 'u=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionUriLine : SessionDescriptionLine
        {
            internal const char UriType = 'u';

            public Uri Location
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {

                    //UriDecode?
                    Uri.TryCreate(m_Parts[0], UriKind.RelativeOrAbsolute, out Uri result);

                    return result;
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { m_Parts.Clear(); m_Parts.Add(value.ToString()); }
            }

            public SessionUriLine()
                : base(UriType)
            {
            }

            public SessionUriLine(SessionDescriptionLine line)
                : base(line)
            {
                if (false.Equals(m_Type.Equals(UriType))) throw new InvalidOperationException("Not a SessionUriLine");
            }

            public SessionUriLine(string uri)
                : this()
            {
                try
                {
                    Location = new Uri(uri);
                }
                catch
                {
                    throw;
                }
            }

            public SessionUriLine(Uri uri)
                : this()
            {
                Location = uri;
            }

            public SessionUriLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, UriType)
            {

            }
        }

        /// <summary>
        /// Represents an 'z=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionTimeZoneLine : SessionDescriptionLine
        {
            internal const char TimeZoneType = 'z';

            #region Properties

            /// <summary>
            /// Gets the amount of adjustment times and offsets found in the parts of the line.
            /// </summary>
            /// <remarks>An adjustment time consists of a date and offset pair</remarks>
            public int AdjustmentTimesCount
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_Parts.Count + 1 >> 1;
                }
            }

            /// <summary>
            /// Gets the decimal representation (in seconds since the 1900 utc epoch) of the values stored on the line.
            /// </summary>
            public IEnumerable<double> AdjustmentValues
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    foreach (var part in m_Parts)
                    {
                        yield return SessionDescription.ParseTime(part).TotalSeconds;
                    }
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    //Remove existing entries
                    m_Parts.Clear();

                    //Enumerate the values selecting the ToString of the each value, add the range to m_Parts
                    m_Parts.AddRange(value.Select(v => v.ToString()));
                }
            }

            /// <summary>
            /// Gets a start or end date assoicated with the adjustmentIndex.
            /// Even values would retrieve a start date and Odd values would retrieve an end date.
            /// </summary>
            /// <param name="adjustmentIndex">The 0 based index of the adjustment date</param>
            /// <returns>The DateTime assoicated with the Adjustment Date at the specified index.</returns>
            public DateTime GetAdjustmentDate(int adjustmentIndex)
            {
                //Get the correct index
                adjustmentIndex <<= 1;// * 2

                if (adjustmentIndex >= m_Parts.Count) return Ntp.NetworkTimeProtocol.UtcEpoch1900;// DateTime.MinValue;

                return Ntp.NetworkTimeProtocol.UtcEpoch1900.Add(SessionDescription.ParseTime(m_Parts[adjustmentIndex]));
            }

            /// <summary>
            /// Returns the TimeSpan assoicated with the Adjustment Offset at the specified index.
            /// Even values would retrieve a start date and odd values would retrieve an end dat.
            /// </summary>
            /// <param name="adjustmentIndex">The Adjustment Offset index (Always 1 + the Adjustment Date Index)</param>
            /// <returns>The TimeSpan assoiciated with the Adjustment Offset at the specified index.</returns>
            public TimeSpan GetAdjustmentOffset(int adjustmentIndex)
            {
                //Get the correct index
                adjustmentIndex <<= 1;// * 2

                //Offsets are the value following the adjustmentIndex
                if (Common.Binary.IsEven(ref adjustmentIndex)) ++adjustmentIndex;

                //Ensure valid index
                if (adjustmentIndex >= m_Parts.Count) return TimeSpan.Zero;

                //Parse value as a time in seconds.
                return SessionDescription.ParseTime(m_Parts[adjustmentIndex]);
            }

            //public DateTimeOffset GetAdjustmentDateTimeOffset(int adjustmentIndex)
            //{
            //    return new DateTimeOffset(GetAdjustmentDate(adjustmentIndex).ToLocalTime(), GetAdjustmentOffset(adjustmentIndex));
            //}

            //Enumerator methods for DateTime or DateTimeOffset which calls the above with the index as necessary.

            #endregion

            #region Constructor

            public SessionTimeZoneLine()
                : base(TimeZoneType, SessionDescription.SpaceString)
            {

            }

            public SessionTimeZoneLine(SessionDescriptionLine line)
                : this()
            {
                if (line.m_Type != TimeZoneType) throw new InvalidOperationException("Not a SessionTimeZoneLine line");

                if (line.m_Parts.Count == 1)
                {
                    string temp = line.m_Parts[0];

                    //No parts, called this() not base()
                    //m_Parts.Clear();

                    m_Parts.AddRange(temp.Split(SessionDescription.Space));
                }
                else m_Parts.AddRange(line.m_Parts);
            }

            //Todo
            public SessionTimeZoneLine(string[] sdpLines, ref int index)
                : this() // base(sdpLines, ref index,  SessionDescription.SpaceString, TimeZoneType) (throws when valadating line in base)
            {
                try
                {
                    string sdpLine = sdpLines[index++].Trim();

                    if (string.IsNullOrWhiteSpace(sdpLine)
                        ||
                        sdpLine[0] != TimeZoneType) Media.Common.TaggedExceptionExtensions.RaiseTaggedException(this, "Invalid SessionTimeZoneLine");

                    sdpLine = SessionDescription.TrimLineValue(sdpLine.Substring(2));

                    m_Parts.AddRange(sdpLine.Split(SessionDescription.Space));
                }
                catch
                {
                    throw;
                }
            }

            #endregion

            #region Add

            public void Add(DateTime dateTime, TimeSpan offset)
            {
                m_Parts.Add((dateTime - Ntp.NetworkTimeProtocol.UtcEpoch1900).TotalSeconds.ToString());

                m_Parts.Add(offset.TotalSeconds.ToString());
            }

            public void Add(DateTime startTime, TimeSpan startOffset, DateTime endTime, TimeSpan endOffset)
            {
                Add(startTime, startOffset);

                Add(endTime, endOffset);
            }

            public void Add(DateTimeOffset dateTime)
            {
                m_Parts.Add((dateTime - Ntp.NetworkTimeProtocol.UtcEpoch1900).TotalSeconds.ToString());

                m_Parts.Add(dateTime.Offset.TotalSeconds.ToString());
            }

            public void Add(DateTimeOffset start, DateTimeOffset end)
            {
                Add(start);

                Add(end);
            }

            #endregion

            #region Remove

            //Rather than a Date based api just allow the start or end time to be removed?
            //These dates don't have to be in any order and although the end time should occur after the start time who really knows...

            //Bigger question is what is the use case of removing a start or end time?
            //Changing existing values may be more suitable..

            #endregion

            //public override string ToString()
            //{
            //    return TimeZoneType.ToString() + SessionDescription.EqualsSign + string.Join(SessionDescription.SpaceString, m_Parts) + SessionDescription.NewLine;
            //}
        }

        /// <summary>
        /// Represents an 'm=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionMediaDescriptionLine : SessionDescriptionLine
        {
            internal const char MediaDescriptionType = 'm';

            //internal const int PartCount = 4;

            #region Properties

            /// <summary>
            /// Gets the string assoicated with the media token.
            /// </summary>
            public string MediaToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                protected internal set { SetPart(0, value); }
            }

            /// <summary>
            /// Parses <see cref="MediaToken"/>
            /// </summary>
            public MediaType MediaType
            {

                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                //Unbounded split
                get { return (MediaType)Enum.Parse(typeof(MediaType), MediaToken.Split(SessionDescription.Space).First(), true); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value.ToString()); }
            }

            /// <summary>
            /// Gets the string assoicated with the port token.
            /// It represents the transport port to which the media stream is sent.
            /// </summary>
            public string PortToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(1); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(1, value); }
            }

            /// <summary>
            /// Parses <see cref="PortToken"/>
            /// </summary>
            public int MediaPort
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return int.Parse(PortToken.Split(SessionDescription.ForwardSlashSplit).FirstOrDefault(), System.Globalization.CultureInfo.InvariantCulture); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    if (value is < ushort.MinValue or > ushort.MaxValue) throw new ArgumentOutOfRangeException("The port value cannot be less than 0 or exceed 65535");

                    SetPart(1, value.ToString());
                }
            }

            //Could be extension method or static method of the Attribute class, this logic could then be reused in other places such as the ConnectionLine.

            /// <summary>
            /// Gets a value indicating if the <see cref="PortToken"/> has the <see cref="SessionDescription.ForwardSlashString"/>
            /// </summary>
            public bool HasMultiplePorts
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return NumberOfPorts + 1 > 2;
                }//PortToken.IndexOf(SessionDescription.ForwardSlashString) >= 0; }
            }

            /// <summary>
            /// Parses the <see cref="PortToken"/> to obtain the number of ports.
            /// Return 1 if no value was found.
            /// </summary>
            public int NumberOfPorts
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    foreach (string part in PortToken.Split(SessionDescription.ForwardSlash).Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(part)) continue;

                        return int.Parse(part);
                    }

                    return 1;
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    if (value is < ushort.MinValue or > ushort.MaxValue) throw new ArgumentOutOfRangeException("A value less than 0 or greater than 65535 is not valid.");

                    //if (NumberOfPorts == value) return;

                    //Todo, test what 0 means here as all ports is more than 1...

                    //If only one port then remove all existing ports except the MediaPort.
                    if (value <= 1)
                    {
                        SetPart(1, MediaPort.ToString());

                        return;
                    }

                    //Set the first part to the result of joining the existing MediaPort + / + value
                    SetPart(1, string.Join(SessionDescription.ForwardSlash.ToString(), MediaPort, value));

                    ////Take the first value which is the MediaPort and then add / and the new values
                    //if (HasPortRange) SetPart(1, string.Join(SessionDescription.ForwardSlash.ToString(), GetPart(1).Split(SessionDescription.ForwardSlash).First(), value));
                    //else SetPart(1, string.Join(SessionDescription.ForwardSlash.ToString(), GetPart(1), value));
                }
            }

            //public IEnumerable<int> MediaPorts
            //{
            //    get
            //    {
            //        int mediaPort = MediaPort;

            //        yield return mediaPort;

            //        //Iterate from ports in range
            //        for (int i = 0, e = PortRange; i < e; ++i) yield return mediaPort + i;
            //    }
            //    set
            //    {
            //        MediaPort = value.First();

            //        PortRange = value.Count() - 1;
            //    }
            //}

            /// <summary>
            ///  Gets or Sets the transport protocol, (proto token)
            /// </summary>
            public string MediaProtocol
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(2); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(2, value); }
            }

            /// <summary>
            /// MediaFormat or `fmt` is a media format description.  
            /// The fourth and any subsequent sub-fields describe the format of the media.  
            /// The interpretation of the media format depends on the value of the <see cref="MediaProtocol"/>.
            /// </summary>
            public string MediaFormat
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(3); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    SetPart(3, value);

                    //ClearState

                    PayloadTypeTokens = null;

                    ParsedPayloadTypes = null;
                }
            }

            //Todo getters
            private string[] PayloadTypeTokens;
            private int[] ParsedPayloadTypes;

            /// <summary>
            /// Parses the port tokens out of the MediaFormat field, this can probably be an extension method.
            /// </summary>
            public IEnumerable<int> PayloadTypes
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (PayloadTypeTokens is null)
                    {
                        //Todo, should be contigious to all derived parts which do not occur within m_Parts
                        PayloadTypeTokens = MediaFormat.Split(SessionDescription.Space);

                        ParsedPayloadTypes = Array.ConvertAll<string, int>(PayloadTypeTokens, Convert.ToInt32);
                    }

                    //foreach (var part in PayloadTypeTokens)
                    //{
                    //    if (string.IsNullOrWhiteSpace(part)) continue;

                    //    yield return int.Parse(part);
                    //}

                    return ParsedPayloadTypes;

                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    ////Proto token index
                    //int start = 3;

                    //foreach (var port in value)
                    //{
                    //    SetPart(start++, port.ToString());
                    //}

                    SetPart(3, string.Join(SessionDescription.SpaceString, value));

                    PayloadTypeTokens = null;

                    ParsedPayloadTypes = null;
                }
            }

            #endregion

            #region Constructor

            public SessionMediaDescriptionLine()
                : base(MediaDescriptionType, SessionDescription.SpaceString, 4)
            {

            }

            public SessionMediaDescriptionLine(SessionDescriptionLine line)
                : base(line)
            {
                if (m_Type != MediaDescriptionType) throw new InvalidOperationException("Not a SessionMediaDescriptionLine line");

                ////Not really needed unless the line was in an incorrect format?
                //if (line.m_Parts.Count == 1)
                //{
                //    string temp = line.m_Parts[0];

                //    m_Parts.Clear();

                //    m_Parts.AddRange(temp.Split(SessionDescription.Space));
                //}
            }

            public SessionMediaDescriptionLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, MediaDescriptionType, 4)
            {

            }

            //internal...
            public SessionMediaDescriptionLine(string text)
                : base(text, SessionDescription.SpaceString, 4)
            {
                if (m_Type != MediaDescriptionType) throw new InvalidOperationException("Not a SessionMediaDescriptionLine line");
            }

            #endregion

            //Todo, test this...
            private void Add(int payloadType)
            {
                //Add the part
                //Add(payloadType.ToString());

                //Join in the given type.
                //Should be concat(pt, space)?
                SetPart(3, string.Join(SessionDescription.SpaceString, payloadType.ToString()));

                //State?

                PayloadTypeTokens = null;

                ParsedPayloadTypes = null;
            }
        }

        //Possibly could be combined with repeat line logic.

        /// <summary>
        /// Represents an 't=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionTimeDescriptionLine : SessionDescriptionLine
        {
            internal const char TimeType = 't';

            //internal protected for setters or this can be changed on accident.
            public static readonly SessionTimeDescriptionLine Permanent = new(0, 0);

            //Method for Permanent or Unbounded?

            #region Constructor

            public SessionTimeDescriptionLine()
                : base(TimeType, SessionDescription.SpaceString, 2)
            {

            }

            public SessionTimeDescriptionLine(long startTime, long stopTime)
                : this()
            {
                StartTime = startTime;

                StopTime = stopTime;
            }

            public SessionTimeDescriptionLine(TimeSpan startTime, TimeSpan stopTime)
                : this()
            {
                StartTimeSpan = startTime;

                StopTimeSpan = stopTime;
            }

            public SessionTimeDescriptionLine(DateTime startDate, DateTime stopDate)
                : this()
            {
                NtpStartDateTime = startDate;

                NtpStopDateTime = stopDate;
            }

            public SessionTimeDescriptionLine(string[] sdpLines, ref int index)
                : base(sdpLines, ref index, SessionDescription.SpaceString, TimeType, 2)
            {

            }

            //internal...
            public SessionTimeDescriptionLine(string text)
                : base(text, SessionDescription.SpaceString, 2) //Should provide a max split count
            {
                if (m_Type != TimeType) throw new InvalidOperationException("Not a SessionTimeDescriptionLine line");
            }

            #endregion

            #region Properties

            //Todo, should have string field for formatting time values given to StartTimeSpan.

            public string StartTimeToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(0); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(0, value); }
            }

            /// <summary>
            /// Gets or sets the start time.
            /// If seto to 0 then the session is not bounded,  though it will not become active until after the <see cref="StartTime"/>.  
            /// </summary>
            /// <remarks>These values are the decimal representation of Network Time Protocol (NTP) time values in seconds since 1900 </remarks>
            public long StartTime
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return (long)StartTimeSpan.TotalSeconds; }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                internal set { SetPart(0, value.ToString()); }
            }

            /// <summary>
            /// TimeSpan representation of <see cref="StartTime"/>
            /// </summary>
            public TimeSpan StartTimeSpan
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return SessionDescription.ParseTime(GetPart(0)); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                internal set { SetPart(0, value.ToString()); }//Format, see above.
            }

            public string StopTimeToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return GetPart(1); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set { SetPart(1, value); }
            }

            /// <summary>
            /// Gets or sets the stop time.
            /// If set to 0 and the <see cref="StartTime"/> is also zero, the session is regarded as permanent.
            /// </summary>
            /// <remarks>These values are the decimal representation of Network Time Protocol (NTP) time values in seconds since 1900 </remarks>
            public long StopTime
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return (long)StopTimeSpan.TotalSeconds; }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                internal set { SetPart(1, value.ToString()); }
            }

            /// <summary>
            /// TimeSpan representation of <see cref="StopTime"/>
            /// </summary>
            public TimeSpan StopTimeSpan
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return SessionDescription.ParseTime(GetPart(1)); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                internal set { SetPart(1, value.ToString()); }//Format
            }

            /// <summary>
            /// Gets the DateTime representation of <see cref="StarTime"/>
            /// Throws an ArgumentOutOfRangeException if SessionStartTime was out of range.
            /// </summary>
            public DateTime NtpStartDateTime
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return Media.Ntp.NetworkTimeProtocol.UtcEpoch1900.AddSeconds(StartTime);// - Media.Ntp.NetworkTimeProtocol.NtpDifferenceUnix);
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                internal set
                {
                    //Convert to SDP timestamp
                    StartTime = (value.ToUniversalTime().Ticks - Ntp.NetworkTimeProtocol.UtcEpoch1900.Ticks) / TimeSpan.TicksPerSecond;

                    //Ensure Ntp Difference
                    //StartTime += Ntp.NetworkTimeProtocol.NtpDifferenceUnix;
                }
            }

            /// <summary>
            /// Gets the DateTime representation of <see cref="StopTime"/>
            /// Throws an ArgumentOutOfRangeException if SessionStopTime was out of range.
            /// </summary>
            public DateTime NtpStopDateTime
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return Media.Ntp.NetworkTimeProtocol.UtcEpoch1900.AddSeconds(StopTime);// - Media.Ntp.NetworkTimeProtocol.NtpDifferenceUnix);
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                internal set
                {
                    //Convert to SDP timestamp
                    StopTime = (value.ToUniversalTime().Ticks - Ntp.NetworkTimeProtocol.UtcEpoch1900.Ticks) / TimeSpan.TicksPerSecond;

                    //Ensure Ntp Difference
                    //StopTime += Ntp.NetworkTimeProtocol.NtpDifferenceUnix;
                }
            }

            /// <summary>
            /// If the <see cref="StopTime"/> is set to zero, then the session is not bounded,  though it will not become active until after the <see cref="StartTime"/>.  
            /// If the <see cref="StartTime"/> is also zero, the session is regarded as permanent.
            /// </summary>
            public bool IsPermanent
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return StartTimeSpan == TimeSpan.Zero && StopTimeSpan == TimeSpan.Zero; }
            }

            //HasDefinedStartTime !=

            /// <summary>
            /// Indicates if the <see cref="StartTime"/> is 0
            /// </summary>
            public bool Unbounded
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return StartTimeSpan == TimeSpan.Zero; }
            }

            #endregion
        }

        /// <summary>
        /// Represents an 'r=' <see cref="SessionDescriptionLine"/>
        /// </summary>
        public class SessionRepeatTimeLine : SessionDescriptionLine
        {
            internal const char RepeatType = 'r';

            #region Properties

            //Parts already gives the strings

            /// <summary>
            /// Gets or sets the the TimeSpan representation of each part of the line
            /// </summary>
            public IEnumerable<TimeSpan> RepeatTimes
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    foreach (var part in m_Parts)
                    {
                        yield return SessionDescription.ParseTime(part);
                    }
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    RepeatValues = value.Select(v => v.TotalSeconds);
                }
            }

            /// <summary>
            /// The sum of <see cref="RepeatTimes"/>
            /// </summary>
            public TimeSpan RepeatTimeSpan
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return TimeSpan.FromSeconds(RepeatTimeValue); }
                //set { } //[Clear parts and] Format value.

                //"8d 2h 0m 0s"
                //RepeatTimeSpan.ToString(@"d\d\ h\h\ m\m\ s\s")

                //Could also do check Days > 0, Hours > 0 and build a smaller string?
            }

            /// <summary>
            /// Gets or sets the decimal representation (in seconds since the 1900 utc epoch) of the values stored on the line.
            /// </summary>
            public IEnumerable<double> RepeatValues
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    foreach (var repeatTime in RepeatTimes)
                    {
                        yield return repeatTime.TotalSeconds;
                    }
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    //Remove existing entries
                    m_Parts.Clear();

                    //Enumerate the values selecting the ToString of the each value, add the range to m_Parts
                    m_Parts.AddRange(value.Select(v => v.ToString()));
                }
            }

            /// <summary>
            /// Gets or sets the sum of <see cref="RepeatValues"/>
            /// </summary>
            public double RepeatTimeValue
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return RepeatValues.Sum(); }
                //set { RepeatTimeSpan = TimeSpan.FromSeconds(value); }
            }

            #endregion

            #region Constructor

            public SessionRepeatTimeLine()
                : base(RepeatType, SessionDescription.SpaceString)
            {

            }

            //Use base constructor...

            public SessionRepeatTimeLine(SessionDescriptionLine line)
                : this()
            {
                if (line.m_Type != RepeatType) throw new InvalidOperationException("Not a SessionRepeatTimeLine line");

                //Checking is was previously parsed....
                if (line.m_Parts.Count == 1)
                {
                    string temp = line.m_Parts[0];

                    m_Parts.AddRange(temp.Split(SessionDescription.Space));
                }
                else m_Parts.AddRange(line.m_Parts);
            }

            public SessionRepeatTimeLine(string[] sdpLines, ref int index)
                : this()
            {
                try
                {
                    string sdpLine = sdpLines[index++].Trim();

                    if (string.IsNullOrWhiteSpace(sdpLine)
                        ||
                        sdpLine[0] != RepeatType) Media.Common.TaggedExceptionExtensions.RaiseTaggedException(this, "Invalid SessionRepeatTimeLine");

                    sdpLine = SessionDescription.TrimLineValue(sdpLine.Substring(2));

                    m_Parts.AddRange(sdpLine.Split(SessionDescription.Space));
                }
                catch
                {
                    throw;
                }
            }

            //checks for either a line to parse or just the line's data, should have a static method FromData which can be used for this as it would simpilify the design across derived instances.
            //could also have a bool valuesOnly = true which indicates that the data is not in line format...
            //internal...
            public SessionRepeatTimeLine(string text)
                : this()
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(text)) Media.Common.TaggedExceptionExtensions.RaiseTaggedException(this, "Invalid SessionRepeatTimeLine");

                    //sdpFormat
                    if (text[0] == RepeatType)
                    {

                        text = SessionDescription.TrimLineValue(text.Substring(2));
                    }

                    m_Parts.AddRange(text.Split(SessionDescription.Space));
                }
                catch
                {
                    throw;
                }
            }

            #endregion

            public void Add(double repeatValue)
            {
                Add(repeatValue.ToString());
            }

            public void Add(params double[] repeatValues)
            {
                foreach (double repeatValue in repeatValues)
                    Add(repeatValue.ToString());
            }

            public void Add(TimeSpan repeatTime)
            {
                Add(repeatTime.ToString());
            }

            public void Add(params TimeSpan[] repeatTimes)
            {
                foreach (TimeSpan repeatTime in repeatTimes)
                    Add(repeatTime.ToString());//Format
            }
        }

        //Todo, revisit AttributeLine

        /// <summary>
        /// Represents an 'a=type:pt'  line with a payload type and additional information
        /// </summary>
        public class PayloadTypeInformationLine : SessionAttributeLine
        {
            /// <summary>
            /// The backing field's for the <see cref="PayloadTypeToken"/> and <see cref="InformationToken"/>
            /// </summary>
            private string[] m_InformationParts;

            /// <summary>
            /// Contains at most 2 tokens, the <see cref="PayloadTypeToken"/> and <see cref="InformationToken"/>
            /// </summary>
            public IEnumerable<string> InformationParts
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (m_InformationParts is not null)
                        return m_InformationParts;

                    string attributeValue = AttributeValue;

                    if (string.IsNullOrWhiteSpace(attributeValue)) return Enumerable.Empty<string>();

                    m_InformationParts = attributeValue.Split(SessionDescription.SpaceSplit, 2, System.StringSplitOptions.RemoveEmptyEntries);

                    return m_InformationParts;
                }
            }

            /// <summary>
            /// The amount of elements contained in <see cref="InformationParts"/>
            /// </summary>
            internal int InformationPartCount
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return InformationParts.Count(); }
            }

            /// <summary>
            /// Indicates if the <see cref="InformationParts"/> contains at least 1 element.
            /// </summary>
            public bool HasPayloadTypeToken { get { return InformationPartCount > 0; } }

            /// <summary>
            /// Gets the token assoicated with the payload type
            /// </summary>
            public string PayloadTypeToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return InformationParts.FirstOrDefault();
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    if (value.Equals(PayloadTypeToken, StringComparison.OrdinalIgnoreCase)) return;

                    m_InformationParts[0] = value;

                    base.AttributeValue = string.Join(SessionDescription.SpaceString, m_InformationParts);//.Where(s => false == string.IsNullOrEmpty(s)));

                    ParsedPayloadTypeToken = -1;
                }
            }

            /// <summary>
            /// Backing field for <see cref="PayloadTypeValue"/>
            /// </summary>
            private int ParsedPayloadTypeToken = -1;

            /// <summary>
            /// The payload type value as parsed from the a=pt:x portion of the line, -1 if not found.
            /// </summary>
            public int PayloadTypeValue
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (ParsedPayloadTypeToken >= 0) return ParsedPayloadTypeToken;

                    int.TryParse(InformationParts.FirstOrDefault(), out ParsedPayloadTypeToken);

                    return ParsedPayloadTypeToken;
                }
            }

            /// <summary>
            /// Indicates if the <see cref="InformationParts"/> contains more than 1 token.
            /// </summary>
            public bool HasInformationToken { get { return InformationPartCount > 1; } }

            /// <summary>
            /// Gets the token which occurs after the <see cref="PayloadTypeToken"/> if <see cref="HasInformationToken"/> is <c>true</c>
            /// </summary>
            public string InformationToken
            {
                get { return HasInformationToken ? InformationParts.LastOrDefault() : string.Empty; }
                set
                {
                    if (value.Equals(InformationToken, StringComparison.OrdinalIgnoreCase)) return;

                    m_InformationParts[1] = value;

                    base.AttributeValue = string.Join(SessionDescription.SpaceString, m_InformationParts);
                }
            }

            #region Constructors

            public PayloadTypeInformationLine(SessionDescriptionLine line) : base(line)
            {
                //validate has payloadtype?
            }

            public PayloadTypeInformationLine(string attibuteType, string payloadTypeToken)
               : base(SessionDescription.SpaceString, 1, attibuteType, null)
            {
                //Todo, should be contigious to all derived parts which do not occur within m_Parts
                m_InformationParts = new string[2];

                PayloadTypeToken = payloadTypeToken;
            }

            public PayloadTypeInformationLine(string attibuteType, string payloadTypeToken, string informationToken)
                : this(attibuteType, payloadTypeToken)
            {
                InformationToken = informationToken;
            }

            public PayloadTypeInformationLine(string attibuteType, int payloadType, string informationToken)
                : this(attibuteType, payloadType.ToString(), informationToken)
            {

            }

            public PayloadTypeInformationLine(string[] sdpLines, ref int index, string seperator = null, int partCount = 1)
               : base(sdpLines, ref index, seperator ?? SessionDescription.SpaceString, partCount)
            {

            }

            //Could put information parts in Parts... see todo.

            public PayloadTypeInformationLine(string seperator = null, int partCount = 1,
                string attributeName = Common.Extensions.String.StringExtensions.UnknownString,
                string attributeValue = null)
                : base(seperator, partCount, attributeName, attributeValue)
            {
                m_InformationParts = new string[2];
            }

            #endregion

        }

        /// <summary>
        /// Line in the format of a=rtpmap:98 H264/90000(/X)
        /// </summary>
        public class RtpMapLine : PayloadTypeInformationLine
        {
            /// <summary>
            /// Gets the token which contains the encoding name, clock rate and encoding parameter
            /// </summary>
            public string EncodingParametersToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return InformationToken;
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    InformationToken = value;
                }
            }

            /// <summary>
            /// <see cref="EncodingParts"/>
            /// </summary>
            internal int EncodingPartCount
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return InformationPartCount; }
            }

            /// <summary>
            /// Split of m_EncodingParts[1], could change this to be a part of m_EncodingParts
            /// </summary>
            private string[] m_EncodingParameters;

            /// <summary>
            /// All tokens which are found in the <see cref="EncodingParameterToken"/>
            /// </summary>
            public IEnumerable<string> EncodingParameters
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_EncodingParameters is not null
                        ? m_EncodingParameters
                        : string.IsNullOrWhiteSpace(EncodingParametersToken)
                        ? Enumerable.Empty<string>()
                        : (m_EncodingParameters = EncodingParametersToken.Split(SessionDescription.ForwardSlashSplit, 3));
                }
            }

            /// <summary>
            /// The amount of tokens which are present in the <see cref="EncodingParameters"/>
            /// </summary>
            public int EncodingParametersCount
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return EncodingParameters.Count(); }
            }

            /// <summary>
            /// Indicates if there is a <see cref="EncodingParameter"/> other than the <see cref="EncodingNameToken"/> and <see cref="ClockRateToken"/>
            /// </summary>
            public bool HasEncodingParameter
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return m_Parts.Count >= 1 && EncodingParametersCount > 2; }
            }

            /// <summary>
            /// Indicates if there are 3 parts, usually the EncodingName, ClockRate and EncodingParameters
            /// </summary>
            /// <remarks>Telephone event doesn't have a clock rate...</remarks>
            //public bool IsValid { get { return EncodingParametersCount >= 2; } }

            /// <summary>
            /// Indicates if the <see cref="EncodingNameToken"/> is present,
            /// </summary>
            public bool HasEncodingNameToken { get { return EncodingParametersCount > 0; } }

            /// <summary>
            /// Gets the first <see cref="EncodingParameters"/>
            /// </summary>
            public string EncodingNameToken
            {
                get { return HasEncodingNameToken ? m_EncodingParameters[0] : string.Empty; }
                //get { return EncodingParts.Skip(1).First().Split(SessionDescription.ForwardSlashSplit, 2)[0];}
                set
                {
                    m_EncodingParameters[0] = value;
                    InformationToken = string.Join(SessionDescription.ForwardSlashString, m_EncodingParameters.Where(s => false == string.IsNullOrWhiteSpace(s)));
                }
            }

            /// <summary>
            /// Indicates if the <see cref="ClockRateToken"/> is present
            /// </summary>
            public bool HasClockRateToken { get { return EncodingParametersCount > 1; } }

            /// <summary>
            /// Gets the second <see cref="EncodingParameters"/> which usually indicates the clock rate of the mapped media in hertz
            /// </summary>
            public string ClockRateToken
            {
                //get { return EncodingParameters.Skip(1).First().Split(SessionDescription.ForwardSlashSplit, 2)[1]; }
                get { return HasClockRateToken ? m_EncodingParameters[1] : string.Empty; }
                set
                {
                    m_EncodingParameters[1] = value;
                    InformationToken = string.Join(SessionDescription.ForwardSlashString, m_EncodingParameters.Where(s => false == string.IsNullOrWhiteSpace(s)));
                    ParsedClockRateToken = -1;
                }
            }

            /// <summary>
            /// The backing field used to indicate the parsed or set value for <see cref="ClockRate"/>
            /// </summary>
            private int ParsedClockRateToken = -1;

            /// <summary>
            /// The clock rate value as parsed from the a=rtmap:x y/z(/w) portion of the line, -1 if not found.
            /// </summary>
            public int ClockRate
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (ParsedClockRateToken >= 0) return ParsedClockRateToken;

                    int.TryParse(ClockRateToken, out ParsedClockRateToken);

                    return ParsedClockRateToken;
                }
            }

            /// <summary>
            /// Gets the encoding parameter, in audio stream this usually indicates the number of channels.
            /// </summary>
            public string EncodingParameter
            {
                get { return HasEncodingParameter ? m_EncodingParameters[2] : string.Empty; }
                set
                {
                    m_EncodingParameters[2] = value;
                    InformationToken = string.Join(SessionDescription.ForwardSlashString, m_EncodingParameters.Where(s => false == string.IsNullOrWhiteSpace(s)));
                }
            }

            #region Constructors

            public RtpMapLine(SessionDescriptionLine line)
           : base(line)
            {
                if (m_Parts.Count is 0
                    ||
                    false.Equals(AttributeName.StartsWith(AttributeFields.RtpMap, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Not a RtpMap line");
                //Seperator?
            }

            public RtpMapLine(string payloadTypeToken)
               : base(SessionDescription.ForwardSlashString, 1, AttributeFields.RtpMap, null)
            {
                m_EncodingParameters = new string[3];

                PayloadTypeToken = payloadTypeToken;
            }

            public RtpMapLine(string payloadTypeToken, string encodingNameToken, string clockRateToken, string encodingParameter)
                : this(payloadTypeToken)
            {
                EncodingNameToken = encodingNameToken;

                ClockRateToken = clockRateToken;

                EncodingParameter = encodingParameter;
            }

            public RtpMapLine(int payloadType, string encodingNameToken, int clockRate, string encodingParameter)
                : this(payloadType.ToString(), encodingNameToken, clockRate.ToString(), encodingParameter)
            {

            }

            public RtpMapLine(string[] sdpLines, ref int index, string seperator = null, int partCount = 1)
                : base(sdpLines, ref index, seperator ?? SessionDescription.ForwardSlashString, partCount)
            {
                if (m_Parts.Count is 0
                    ||
                    false.Equals(AttributeName.StartsWith(AttributeFields.RtpMap, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Not a RtpMap line");
            }

            #endregion

            //Todo, Needs Add override? or could be specified in seperator overload of constructor?
        }

        /// <summary>
        /// Line in the format of a=fmtp:98 profile-level-id=42A01E; packetization-mode=1; sprop-parameter-sets=<parameter sets data>
        /// </summary>
        public class FormatTypeLine : PayloadTypeInformationLine
        {

            public IEnumerable<string> FormatParts
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return InformationParts;
                }
            }

            internal int FormatPartCount
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return InformationPartCount; }
            }

            /// <summary>
            /// <see cref="PayloadTypeInformationLine.PayloadTypeToken"/>
            /// </summary>
            public string FormatToken
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return PayloadTypeToken;
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    PayloadTypeToken = value;
                }
            }

            /// <summary>
            /// The format value as parsed from the a=fmtp:x portion of the line, -1 if not found.
            /// </summary>
            public int FormatValue
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return PayloadTypeValue;
                }
            }

            //-----

            /// <summary>
            /// Indicates if <see cref="FormatSpecificParameters"/> contains at least 1 element.
            /// </summary>
            public bool HasFormatSpecificParameters
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return m_Parts.Count >= 1 && FormatSpecificParametersCount > 0; }
            }

            /// <summary>
            /// <see cref="PayloadTypeInformationLine.InformationToken"/>
            /// </summary>
            public string FormatSpecificParameterToken
            {
                //get { return GetPart(1); }
                //set { SetPart(1, value); }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return InformationToken;
                }
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                set
                {
                    InformationToken = value;
                }
            }

            /// <summary>
            /// The backing field for <see cref="FormatSpecificParameters"/>
            /// </summary>
            private string[] m_FormatSpecificParameters;

            /// <summary>
            /// All tokens which are found in the <see cref="FormatSpecificParameterToken"/>
            /// </summary>
            public IEnumerable<string> FormatSpecificParameters
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_FormatSpecificParameters is not null
                        ? m_FormatSpecificParameters
                        : string.IsNullOrWhiteSpace(FormatSpecificParameterToken)
                        ? Enumerable.Empty<string>()
                        : (m_FormatSpecificParameters = FormatSpecificParameterToken.Split(SessionDescription.SemiColonSplit));
                }
            }

            /// <summary>
            /// The amount of tokens which are present in the <see cref="FormatSpecificParameters"/>
            /// </summary>
            public int FormatSpecificParametersCount
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get { return FormatSpecificParameters.Count(); }
            }

            #region Constructors

            //Could be verified in a common class given a start type.
            public FormatTypeLine(SessionDescriptionLine line)
                : base(line)
            {
                if (m_Parts.Count is 0
                    ||
                    false.Equals(AttributeName.StartsWith(AttributeFields.FormatType, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Not a FormatTypeLine line");
                //else if (m_Parts.Count == 1)
                //{
                //    //Extract parts
                //    var z = m_Parts[0].Split(SessionDescription.SpaceSplit, 2, StringSplitOptions.RemoveEmptyEntries);

                //    m_Parts.Clear();

                //    //Part 0 of above contains the FormatToken
                //    m_Parts.AddRange(z[0].Split(SessionDescription.ColonSplit, 2, StringSplitOptions.RemoveEmptyEntries));

                //    m_Parts.AddRange(z.Skip(1));
                //}
            }

            //So Add values are seperates by ;

            public FormatTypeLine(string formatToken)
                : base(SessionDescription.SemiColonString, 1, AttributeFields.FormatType, null/*formatToken*/)  //, null, null, null/*formatToken*/, SessionDescription.SpaceString)
            {
                FormatToken = formatToken;
            }

            public FormatTypeLine(string formatToken, string formatSpecificParameters)
                : this(formatToken)
            {
                FormatSpecificParameterToken = formatSpecificParameters;

                //Add(formatSpecificParameters);
            }

            public FormatTypeLine(int payloadType, string formatSpecificParameters)
                : this(payloadType.ToString(), formatSpecificParameters)
            {

            }

            public FormatTypeLine(string[] sdpLines, ref int index, string seperator = null, int partCount = 1)
                : base(sdpLines, ref index, seperator ?? SessionDescription.SemiColonString, partCount)
            {
                if (m_Parts.Count is 0
                    ||
                    false.Equals(AttributeName.StartsWith(AttributeFields.FormatType, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Not a FormatTypeLine line");
                //false == GetPart(0).StartsWith(AttributeFields.FormatType, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Not a FormatTypeLine line");
            }

            #endregion

            //Might need overloads for add with specific seperator.
        }
    }
}
