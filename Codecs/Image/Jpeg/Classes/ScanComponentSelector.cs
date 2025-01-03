﻿using Media.Common;

namespace Media.Codec.Jpeg.Classes;

public sealed class ScanComponentSelector : MemorySegment
{
    /// <summary>
    /// The amount of bytes in a <see cref="ScanComponentSelector"/>.
    /// </summary>
    public const int Length = 2;

    /// <summary>
    /// Scan component selector.
    /// </summary>
    public byte Csj
    {
        get => Array[Offset];
        set => Array[Offset] = value;
    }

    /// <summary>
    /// Entropy coding table selector DC.
    /// </summary>
    public byte Tdj
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1);
            return (byte)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset + 1);
            this.WriteBits(bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Entropy coding table selector AC.
    /// </summary>
    public byte Taj
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            return (byte)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            this.WriteBits(bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// This Raw Data of this <see cref="ScanComponentSelector"/> which should be blittable.
    /// </summary>
    public MemorySegment RawData => new MemorySegment(Array, Offset, Length);

    /// <summary>
    /// Constructs a new <see cref="ScanComponentSelector"/> instance from the given <paramref name="other"/> <see cref="MemorySegment"/>.
    /// </summary>
    /// <param name="other">Data which corresponds to a <see cref="ScanComponentSelector"/></param>
    public ScanComponentSelector(MemorySegment other):
        base(other)
    {
    }

    public ScanComponentSelector()
        : base(Length)
    {
    }
}
