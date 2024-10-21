﻿using Media.Common;

namespace Media.Codec.Jpeg;

//Needs to implement a common class if the elements can be reused => 
public class Marker : MemorySegment
{
    /// <summary>
    /// The amount of bytes required at minimum to create a marker with a <see cref="Prefix"/> and <see cref="FunctionCode"/>
    /// </summary>
    public const int PrefixBytes = Binary.BytesPerShort;

    /// <summary>
    /// The amount of bytes which are required to store the <see cref="Length"/> of the marker.
    /// </summary>
    public const int LengthBytes = Binary.BytesPerShort;

    public byte Prefix
    {
        get => this[0];
        set => this[0] = value;
    }

    public byte FunctionCode
    {
        get => this[1];
        set => this[1] = value;
    }

    public int Length
    {
        get => Binary.ReadU16(Array, Offset + PrefixBytes, Binary.IsLittleEndian);
        set => Binary.Write16(Array, Offset + PrefixBytes, Binary.IsLittleEndian, (ushort)value);
    }

    public int DataLength => Binary.Max(0, Count - PrefixBytes - LengthBytes);

    public int MarkerLength => DataLength + 2;

    public int DataOffset => Offset + PrefixBytes + LengthBytes;

    public MemorySegment Data => Count > PrefixBytes + LengthBytes ? this.Slice(PrefixBytes + LengthBytes) : Empty;

    public bool IsEmpty => DataLength == 0;

    public Marker(byte functionCode, int size)
        : base(new byte[size > 0 ? size + PrefixBytes + LengthBytes : PrefixBytes])
    {
        Prefix = Markers.Prefix;
        FunctionCode = functionCode;
        if (size > 0)
            Length = size;
    }

    public Marker(MemorySegment data): base(data)
    {
        if (Count < PrefixBytes)
            throw new System.InvalidOperationException($"Atleast {PrefixBytes} are required to read a marker.");
    }

    public override string ToString()
        => Markers.ToTextualConvention(FunctionCode);
}
