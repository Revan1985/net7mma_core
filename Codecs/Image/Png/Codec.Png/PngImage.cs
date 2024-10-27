﻿using System.IO.Compression;
using System.IO.Hashing;
using System.Numerics;
using Media.Codecs.Image;
using Media.Common;
using Media.Common.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Media.Codec.Png;

public class PngImage : Image
{
    public const ulong PNGSignature = 0x89504E470D0A1A0A;

    private static ImageFormat CreateImageFormat(byte bitDepth, byte colorType)
    {
        switch (colorType)
        {
            case 0: // Grayscale
            case 3: // Indexed-color            
                return ImageFormat.Monochrome(bitDepth);
            case 4: // Grayscale with alpha
                return ImageFormat.WithPreceedingAlphaComponent(ImageFormat.Monochrome(bitDepth / 2), bitDepth / 2);
            case 2: // Truecolor (RGB)
                return ImageFormat.RGB(bitDepth/ 3);            
            case 6: // Truecolor with alpha (RGBA)
                return ImageFormat.RGBA(bitDepth / 4);
            default:
                throw new NotSupportedException($"Color type {colorType} is not supported.");
        }
    }

    #region Fields    

    public readonly byte ColorType;

    public readonly ConcurrentThesaurus<ChunkName, Chunk> Chunks;

    #endregion

    public PngImage(ImageFormat imageFormat, int width, int height)
        : base(imageFormat, width, height, new PngCodec())
    {
        Chunks = new ConcurrentThesaurus<ChunkName, Chunk>();
    }

    private PngImage(ImageFormat imageFormat, int width, int height, MemorySegment data)
        : base(imageFormat, width, height, data, new PngCodec())
    {
        Chunks = new ConcurrentThesaurus<ChunkName, Chunk>();
    }

    private PngImage(ImageFormat imageFormat, int width, int height, MemorySegment data, byte colorType, ConcurrentThesaurus<ChunkName, Chunk> chunks)
        : this(imageFormat, width, height, data)
    {
        ColorType = colorType;
        Chunks = chunks;
    }

    public static PngImage FromStream(Stream stream)
    {
        // Read the IHDR chunk
        int width = 0, height = 0;
        ImageFormat? imageFormat = default;
        MemorySegment? dataSegment = default;
        byte colorType = default;
        ConcurrentThesaurus<ChunkName, Chunk> chunks = new ConcurrentThesaurus<ChunkName, Chunk>();
        Crc32 crc32 = new Crc32();
        foreach(var chunk in PngCodec.ReadChunks(stream))
        {
            var actualCrc = chunk.Crc;
            if (actualCrc > 0)
            {
                crc32.Reset();
                using var header = chunk.Header;
                var headerSpan = header.ToSpan();
                crc32.Append(headerSpan.Slice(ChunkHeader.LengthBytes, ChunkHeader.NameLength));
                using var chunkData = chunk.Data;
                var chunkSpan = chunkData.ToSpan();
                crc32.Append(chunkSpan);
                var expectedCrc = crc32.GetCurrentHashAsUInt32();                
                if (actualCrc > 0 && expectedCrc != actualCrc)
                    throw new InvalidDataException($"There is an error in the provided stream Crc32 failed, Expected: ${expectedCrc}, Found: {actualCrc}.");
            }

            var chunkName = chunk.ChunkName;
            switch (chunkName)
            {
                case ChunkName.Header:
                    var offset = chunk.DataOffset;
                    width = Binary.Read32(chunk.Array, ref offset, Binary.IsLittleEndian);
                    height = Binary.Read32(chunk.Array, ref offset, Binary.IsLittleEndian);
                    var bitDepth = chunk.Array[offset++];
                    colorType = chunk.Array[offset++];
                    var compressionMethod = chunk.Array[offset++];
                    var filterMethod = chunk.Array[offset++];
                    var interlaceMethod = chunk.Array[offset++];

                    // Create the image format based on the IHDR data
                    imageFormat = CreateImageFormat(bitDepth, colorType);

                    //ToDo, Crc is in data segment, so we need to read it and validate it.
                    continue;
                case ChunkName.Data:

                    //TODO
                    //We should have a MeorySegmentStream and append to it because there can be multiple data chunks.
                    using(var ms = new MemoryStream(chunk.Array, chunk.DataOffset + 2, chunk.Count - chunk.DataOffset - 2))
                    {
                        using (MemoryStream decompressedStream = new MemoryStream())
                        {
                            using (DeflateStream deflateStream = new DeflateStream(ms, CompressionMode.Decompress))
                            {
                                try
                                {
                                    deflateStream.CopyTo(decompressedStream);
                                    dataSegment = new(decompressedStream.ToArray());
                                }
                                catch(InvalidDataException)
                                {
                                    dataSegment = chunk.Data;
                                }
                            }
                        }
                    }

                    continue;
                case ChunkName.End:
                    goto LoadImage;
                default:

                    chunks.Add(chunkName, chunk);
                    continue;
            }
        }

    LoadImage:

        if(imageFormat == null || dataSegment == null)
            throw new InvalidDataException("The provided stream does not contain valid PNG image data.");

        // Create and return the PngImage
        return new PngImage(imageFormat, width, height, dataSegment, colorType, chunks);
    }

    public void Save(Stream stream, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        // Write the PNG signature
        stream.Write(Binary.GetBytes(PNGSignature, BitConverter.IsLittleEndian));

        //TODO have Chunks class to make handling of reading and write more robust
        //Should implement like MarkerReader and MarkerWriter in Codec.Jpeg

        // Write the IHDR chunk
        WriteIHDRChunk(stream);

        if (Chunks != null)
        {
            foreach (var chunk in Chunks.Values)
            {
                stream.Write(chunk.Array, chunk.Offset, chunk.Count);
            }
        }

        // Write the IDAT chunk
        WriteIDATChunk(stream, compressionLevel);

        // Write the IEND chunk
        WriteIENDChunk(stream);
    }

    private void WriteIHDRChunk(Stream stream)
    {
        using var ihdr = new Chunk(ChunkName.Header, 13);
        var offset = ihdr.DataOffset;
        Binary.Write32(ihdr.Array, ref offset, Binary.IsLittleEndian, Width);
        Binary.Write32(ihdr.Array, ref offset, Binary.IsLittleEndian, Height);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, (byte)ImageFormat.Size);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, ColorType);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, 0);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, 0);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, 0);
        stream.Write(ihdr.Array, ihdr.Offset, ihdr.Count);
    }

    private void WriteIDATChunk(Stream stream, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        Chunk idat;
        using (MemoryStream ms = new MemoryStream(Data.Count))
        {
            using (DeflateStream deflateStream = new DeflateStream(ms, compressionLevel, true))
            {
                deflateStream.Write(Data.Array, Data.Offset, Data.Count);

                deflateStream.Close();

                ms.TryGetBuffer(out var buffer);

                const byte ZLibHeaderLength = 2;
                const byte Deflate32KbWindow = 120;
                const byte ChecksumBits = 1;

                // Create the chunk memory segment
                idat = new Chunk(ChunkName.Data, buffer.Count + ZLibHeaderLength);

                // Write the ZLib header.
                idat[idat.DataOffset] = Deflate32KbWindow;
                idat[idat.DataOffset + 1] = ChecksumBits;

                // Copy the compressed data.
                Buffer.BlockCopy(buffer.Array!, buffer.Offset, idat.Array, idat.DataOffset, buffer.Count);

                //Todo calculate the CRC and write to idat.
            }
        }
        stream.Write(idat.Array, idat.Offset, idat.Count);
        idat.Dispose();
    }

    private void WriteIENDChunk(Stream stream)
    {
        using var iend = new Chunk(ChunkName.End, 0);
        stream.Write(iend.Array, iend.Offset, iend.Count);
    }

    public MemorySegment GetPixelDataAt(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return MemorySegment.Empty;

        // PNG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);

        return Data.Slice(offset, ImageFormat.Length);
    }

    public Vector<byte> GetVectorDataAt(int x, int y)
    {
        // PNG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        return new Vector<byte>(Data.Array, Data.Offset + offset);
    }

    public void SetVectorDataAt(int x, int y, Vector<byte> vectorData)
    {
        // PNG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        vectorData.CopyTo(new Span<byte>(Data.Array, Data.Offset + offset, Vector<byte>.Count));
    }
}