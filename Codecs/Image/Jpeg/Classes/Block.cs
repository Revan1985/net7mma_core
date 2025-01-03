﻿using Media.Common;
using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Media.Codec.Jpeg.Classes;

//Based on:
//https://github.com/SixLabors/ImageSharp/blob/main/src/ImageSharp/Formats/Jpeg/Components/Block8x8.cs
//https://github.com/SixLabors/ImageSharp/blob/main/src/ImageSharp/Formats/Jpeg/Components/Block8x8F.cs

/// <summary>
/// Represents a block of data, typically 64 coeffients long. (256 bytes)
/// </summary>
internal class Block : MemorySegment
{
    /// <summary>
    /// By default, how many coefficients are in a block.
    /// </summary>
    public const int DefaultSize = JpegCodec.BlockSize * JpegCodec.BlockSize;

    /// <summary>
    /// A number of rows of 8 scalar coefficients each in <see cref="Block8x8F"/>
    /// </summary>
    public const int RowCount = 8;

    /// <summary>
    /// Gets a value indicating whether <see cref="Vector{T}"/> code is being JIT-ed to AVX2 instructions
    /// where both float and integer registers are of size 256 byte.
    /// </summary>
    public static bool HasVector8 { get; } =
        Vector.IsHardwareAccelerated && Vector<float>.Count == 8 && Vector<int>.Count == 8;

    #region Static Functions

    /// <summary>
    /// Swaps the two references.
    /// </summary>
    /// <typeparam name="T">The type to swap.</typeparam>
    /// <param name="a">The first item.</param>
    /// <param name="b">The second item.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    /// <summary>
    /// Swaps the two references.
    /// </summary>
    /// <typeparam name="T">The type to swap.</typeparam>
    /// <param name="a">The first item.</param>
    /// <param name="b">The second item.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Swap<T>(ref Span<T> a, ref Span<T> b)
    {
        // Tuple swap uses 2 more IL bytes
        Span<T> tmp = a;
        a = b;
        b = tmp;
    }

    /// <summary>
    /// Transform all scalars in 'v' in a way that converting them to <see cref="int"/> would have rounding semantics.
    /// </summary>
    /// <param name="v">The vector</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 PseudoRound(Vector4 v)
    {
        Vector4 sign = Vector4.Clamp(v, new Vector4(-1), new Vector4(1));

        return v + (sign * 0.5f);
    }

    /// <summary>
    /// Rounds all values in 'v' to the nearest integer following <see cref="MidpointRounding.ToEven"/> semantics.
    /// Source:
    /// <see>
    ///     <cref>https://github.com/g-truc/glm/blob/master/glm/simd/common.h#L110</cref>
    /// </see>
    /// </summary>
    /// <param name="v">The vector</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector<float> FastRound(Vector<float> v)
    {
        if (Avx2.IsSupported)
        {
            ref Vector256<float> v256 = ref Unsafe.As<Vector<float>, Vector256<float>>(ref v);
            Vector256<float> vRound = Avx.RoundToNearestInteger(v256);
            return Unsafe.As<Vector256<float>, Vector<float>>(ref vRound);
        }
        else
        {
            var magic0 = new Vector<int>(int.MinValue); // 0x80000000
            var sgn0 = Vector.AsVectorSingle(magic0);
            var and0 = Vector.BitwiseAnd(sgn0, v);
            var or0 = Vector.BitwiseOr(and0, new Vector<float>(8388608.0f));
            var add0 = Vector.Add(v, or0);
            return Vector.Subtract(add0, or0);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="row"></param>
    /// <param name="off"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    private static Vector<float> NormalizeAndRound(Vector<float> row, Vector<float> off, Vector<float> max)
    {
        row += off;
        row = Vector.Max(row, Vector<float>.Zero);
        row = Vector.Min(row, max);
        return FastRound(row);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="dest"></param>
    private static void MultiplyIntoInt16_Avx2(Block a, Block b, Block dest)
    {
        ref Vector256<float> aBase = ref a.V0f;
        ref Vector256<float> bBase = ref b.V0f;

        ref Vector256<short> destRef = ref dest.V01;
        Vector256<int> multiplyIntoInt16ShuffleMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);

        for (nuint i = 0; i < JpegCodec.BlockSize; i += 2)
        {
            Vector256<int> row0 = Avx.ConvertToVector256Int32(Avx.Multiply(Unsafe.Add(ref aBase, i + 0), Unsafe.Add(ref bBase, i + 0)));
            Vector256<int> row1 = Avx.ConvertToVector256Int32(Avx.Multiply(Unsafe.Add(ref aBase, i + 1), Unsafe.Add(ref bBase, i + 1)));

            Vector256<short> row = Avx2.PackSignedSaturate(row0, row1);
            row = Avx2.PermuteVar8x32(row.AsInt32(), multiplyIntoInt16ShuffleMask).AsInt16();

            Unsafe.Add(ref destRef, i / 2) = row;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="dest"></param>
    private static void MultiplyIntoInt16_Sse2(Block a, Block b, Block dest)
    {
        ref Vector128<float> aBase = ref Unsafe.As<byte, Vector128<float>>(ref a.Array[a.Offset]);
        ref Vector128<float> bBase = ref Unsafe.As<byte, Vector128<float>>(ref b.Array[b.Offset]);

        ref Vector128<short> destBase = ref Unsafe.As<Block, Vector128<short>>(ref dest);

        for (nuint i = 0; i < 16; i += 2)
        {
            Vector128<int> left = Sse2.ConvertToVector128Int32(Sse.Multiply(Unsafe.Add(ref aBase, i + 0), Unsafe.Add(ref bBase, i + 0)));
            Vector128<int> right = Sse2.ConvertToVector128Int32(Sse.Multiply(Unsafe.Add(ref aBase, i + 1), Unsafe.Add(ref bBase, i + 1)));

            Vector128<short> row = Sse2.PackSignedSaturate(left, right);
            Unsafe.Add(ref destBase, i / 2) = row;
        }
    }

    /// <summary>
    /// Transposes the block in place with AVX instructions.
    /// </summary>
    private void TransposeInplace_Avx()
    {
        // https://stackoverflow.com/questions/25622745/transpose-an-8x8-float-using-avx-avx2/25627536#25627536
        var vector = V4L;
        Vector256<float> r0 = Avx.InsertVector128(
            V0f,
            Unsafe.As<Vector4, Vector128<float>>(ref vector),
            1);

        vector = V5L;
        Vector256<float> r1 = Avx.InsertVector128(
           V1f,
           Unsafe.As<Vector4, Vector128<float>>(ref vector),
           1);

        vector = V6L;
        Vector256<float> r2 = Avx.InsertVector128(
           V2f,
           Unsafe.As<Vector4, Vector128<float>>(ref vector),
           1);

        vector = V7L;
        Vector256<float> r3 = Avx.InsertVector128(
           V3f,
           Unsafe.As<Vector4, Vector128<float>>(ref vector),
           1);

        vector = V0R;
        var right = V4R;
        Vector256<float> r4 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref vector).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref right),
           1);

        vector = V1R;
        right = V5R;
        Vector256<float> r5 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref vector).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref right),
           1);

        vector = V2R;
        right = V6R;
        Vector256<float> r6 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref vector).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref right),
           1);

        vector = V3R;
        right = V7R;
        Vector256<float> r7 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref vector).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref right),
           1);

        Vector256<float> t0 = Avx.UnpackLow(r0, r1);
        Vector256<float> t2 = Avx.UnpackLow(r2, r3);
        Vector256<float> v = Avx.Shuffle(t0, t2, 0x4E);
        V0f = Avx.Blend(t0, v, 0xCC);
        V1f = Avx.Blend(t2, v, 0x33);

        Vector256<float> t4 = Avx.UnpackLow(r4, r5);
        Vector256<float> t6 = Avx.UnpackLow(r6, r7);
        v = Avx.Shuffle(t4, t6, 0x4E);
        V4f = Avx.Blend(t4, v, 0xCC);
        V5f = Avx.Blend(t6, v, 0x33);

        Vector256<float> t1 = Avx.UnpackHigh(r0, r1);
        Vector256<float> t3 = Avx.UnpackHigh(r2, r3);
        v = Avx.Shuffle(t1, t3, 0x4E);
        V2f = Avx.Blend(t1, v, 0xCC);
        V3f = Avx.Blend(t3, v, 0x33);

        Vector256<float> t5 = Avx.UnpackHigh(r4, r5);
        Vector256<float> t7 = Avx.UnpackHigh(r6, r7);
        v = Avx.Shuffle(t5, t7, 0x4E);
        V6f = Avx.Blend(t5, v, 0xCC);
        V7f = Avx.Blend(t7, v, 0x33);
    }

    /// <summary>
    /// Calculate the total sum of absolute differences of elements in 'a' and 'b'.
    /// </summary>
    public static long TotalDifference(Block a, Block b)
    {
        long result = 0;

        for (int i = 0; i < DefaultSize; i++)
        {
            int d = a[i] - b[i];
            result += Math.Abs(d);
        }

        return result;
    }

    /// <summary>
    /// Loads a block from a span of short data
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Block Load(Span<short> data)
    {
        var block = new Block();
        var bytes = MemoryMarshal.Cast<short, byte>(data);
        bytes.CopyTo(block.Array);
        return block;
    }

    /// <summary>
    /// Loads a block from a span of float data
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Block Load(Span<float> data)
    {
        var block = new Block();
        var bytes = MemoryMarshal.Cast<float, byte>(data);
        bytes.CopyTo(block.Array);
        return block;
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a block with the <see cref="DefaultSize"/> of coefficients.
    /// </summary>
    public Block() 
        : base(DefaultSize * Binary.BytesPerInteger)
    {

    }

    /// <summary>
    /// Specifies the count of coefficients in the block.
    /// </summary>
    /// <param name="coefficientCount"></param>
    public Block(int coefficientCount)
        : base(coefficientCount * Binary.BytesPerInteger)
    {

    }

    /// <summary>
    /// Constructs a <see cref="Block"/> from the given <see cref="MemorySegment"/>.
    /// </summary>
    /// <param name="segment"></param>
    public Block(MemorySegment segment)
        : base(segment)
    {
    }

    #endregion

    #region Vector Properties

    #region Vector 256<float>

    public ref Vector256<float> V0f
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(0));
    }
    
    public ref Vector256<float> V1f
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(32));
    }
    
    public ref Vector256<float> V2f 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(64));
    }
    
    public ref Vector256<float> V3f
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(96));
    }
    
    public ref Vector256<float> V4f
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(128));
    }
    
    public ref Vector256<float> V5f
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(160));
    }
    
    public ref Vector256<float> V6f
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(192));
    }
    
    public ref Vector256<float> V7f
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<float>>(ref GetReference(224));
    }

    #endregion

    #region Vector 128<short>

    public ref Vector128<short> V0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(0));
    }

    public ref Vector128<short> V1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(16));
    }

    public ref Vector128<short> V2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(32));
    }

    public ref Vector128<short> V3
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(48));
    }

    public ref Vector128<short> V4
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(64));
    }

    public ref Vector128<short> V5
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(80));
    }

    public ref Vector128<short> V6
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(96));
    }

    public ref Vector128<short> V7
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector128<short>>(ref GetReference(112));
    }

    public ref Vector256<short> V01
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<short>>(ref GetReference(0));
    }

    public ref Vector256<short> V23
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<short>>(ref GetReference(32));
    }

    public ref Vector256<short> V45
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<short>>(ref GetReference(64));
    }

    public ref Vector256<short> V67
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.As<byte, Vector256<short>>(ref GetReference(96));
    }

    #endregion

    #region Vector4

    public Vector4 V0L
    {
        get => new Vector4(GetFourFloats(0));
        set => value.CopyTo(GetFourFloats(0));
    }

    public Vector4 V0R
    {
        get => new Vector4(GetFourFloats(16));
        set => value.CopyTo(GetFourFloats(16));
    }

    public Vector4 V1L
    {
        get => new Vector4(GetFourFloats(32));
        set => value.CopyTo(GetFourFloats(32));
    }

    public Vector4 V1R
    {
        get => new Vector4(GetFourFloats(48));
        set => value.CopyTo(GetFourFloats(48));
    }

    public Vector4 V2L
    {
        get => new Vector4(GetFourFloats(64));
        set => value.CopyTo(GetFourFloats(64));
    }

    public Vector4 V2R
    {
        get => new Vector4(GetFourFloats(80));
        set => value.CopyTo(GetFourFloats(80));
    }

    public Vector4 V3L
    {
        get => new Vector4(GetFourFloats(96));
        set => value.CopyTo(GetFourFloats(96));
    }

    public Vector4 V3R
    {
        get => new Vector4(GetFourFloats(112));
        set => value.CopyTo(GetFourFloats(112));
    }

    public Vector4 V4L
    {
        get => new Vector4(GetFourFloats(128));
        set => value.CopyTo(GetFourFloats(128));
    }

    public Vector4 V4R
    {
        get => new Vector4(GetFourFloats(144));
        set => value.CopyTo(GetFourFloats(144));
    }

    public Vector4 V5L
    {
        get => new Vector4(GetFourFloats(160));
        set => value.CopyTo(GetFourFloats(160));
    }

    public Vector4 V5R
    {
        get => new Vector4(GetFourFloats(176));
        set => value.CopyTo(GetFourFloats(176));
    }

    public Vector4 V6L
    {
        get => new Vector4(GetFourFloats(192));
        set => value.CopyTo(GetFourFloats(192));
    }

    public Vector4 V6R
    {
        get => new Vector4(GetFourFloats(208));
        set => value.CopyTo(GetFourFloats(208));
    }

    public Vector4 V7L
    {
        get => new Vector4(GetFourFloats(224));
        set => value.CopyTo(GetFourFloats(224));
    }

    public Vector4 V7R
    {
        get => new Vector4(GetFourFloats(240));
        set => value.CopyTo(GetFourFloats(240));
    }

    #endregion

    #endregion

    #region Properties

    /// <summary>
    /// The length of the block in <see cref="short"/> values
    /// </summary>
    public int ShortLength => Count / Binary.BytesPerShort;

    /// <summary>
    /// The length of the block in <see cref="float"/> values
    /// </summary>
    public int FloatLength => Count / Binary.BytesPerInteger;

    #endregion

    #region Indexers

    /// <summary>
    /// Gets or sets a <see cref="short"/> value at the given index
    /// </summary>
    /// <param name="idx">The index</param>
    /// <returns>The value</returns>
    public new short this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ref short selfRef = ref Unsafe.As<byte, short>(ref Array[Offset + index * Binary.BytesPerShort]);
            return selfRef;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ref short selfRef = ref Unsafe.As<byte, short>(ref Array[Offset + index * Binary.BytesPerShort]);
            selfRef = value;
        }
    }

    /// <summary>
    /// Gets or sets a value in a row and column of the block
    /// </summary>
    /// <param name="x">The x position index in the row</param>
    /// <param name="y">The column index</param>
    /// <returns>The value</returns>
    public short this[int x, int y]
    {
        get => this[(y * JpegCodec.BlockSize) + x];
        set => this[(y * JpegCodec.BlockSize) + x] = value;
    }

    /// <summary>
    /// Gets or sets a <see cref="float"/> value at the given index
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public float this[nuint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ref float selfRef = ref Unsafe.As<byte, float>(ref Array[(nuint)Offset + index * Binary.BytesPerInteger]);
            return selfRef;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ref float selfRef = ref Unsafe.As<byte, float>(ref Array[(nuint)Offset + index * Binary.BytesPerInteger]);
            selfRef = value;
        }
    }

    /// <summary>
    /// Gets or sets a <see cref="float"/> value at the given index
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public float this[uint index]
    {
        get => this[(nuint)index];
        set => this[(nuint)index] = value;
    }

    /// <summary>
    /// Gets or sets a value in a row and column of the block
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public float this[uint x, uint y]
    {
        get => this[(y * JpegCodec.BlockSize) + x];
        set => this[(y * JpegCodec.BlockSize) + x] = value;
    }

    #endregion

    #region Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LoadFrom(Block source)
    {
        if (HasVector8)
        {
            LoadFromInt16ExtendedAvx2(source);
            return;
        }

        LoadFromInt16Scalar(source);
    }

    public void LoadFromInt16Scalar(Block source)
    {
        ref short selfRef = ref Unsafe.As<byte, short>(ref source.GetReference(0));

        Vector4 stack = V0L;

        stack.X = Unsafe.Add(ref selfRef, 0);
        stack.Y = Unsafe.Add(ref selfRef, 1);
        stack.Z = Unsafe.Add(ref selfRef, 2);
        stack.W = Unsafe.Add(ref selfRef, 3);

        V0L = stack;

        stack = V0R;

        stack.X = Unsafe.Add(ref selfRef, 4);
        stack.Y = Unsafe.Add(ref selfRef, 5);
        stack.Z = Unsafe.Add(ref selfRef, 6);
        stack.W = Unsafe.Add(ref selfRef, 7);

        V0R = stack;

        stack = V1L;

        stack.X = Unsafe.Add(ref selfRef, 8);
        stack.Y = Unsafe.Add(ref selfRef, 9);
        stack.Z = Unsafe.Add(ref selfRef, 10);
        stack.W = Unsafe.Add(ref selfRef, 11);
        
        V1L = stack;

        stack = V1R;

        stack.X = Unsafe.Add(ref selfRef, 12);
        stack.Y = Unsafe.Add(ref selfRef, 13);
        stack.Z = Unsafe.Add(ref selfRef, 14);
        stack.W = Unsafe.Add(ref selfRef, 15);

        V1R = stack;

        stack = V2L;

        stack.X = Unsafe.Add(ref selfRef, 16);
        stack.Y = Unsafe.Add(ref selfRef, 17);
        stack.Z = Unsafe.Add(ref selfRef, 18);
        stack.W = Unsafe.Add(ref selfRef, 19);

        V2L = stack;

        stack = V2R;

        stack.X = Unsafe.Add(ref selfRef, 20);
        stack.Y = Unsafe.Add(ref selfRef, 21);
        stack.Z = Unsafe.Add(ref selfRef, 22);
        stack.W = Unsafe.Add(ref selfRef, 23);

        V2R = stack;

        stack = V3L;

        stack.X = Unsafe.Add(ref selfRef, 24);
        stack.Y = Unsafe.Add(ref selfRef, 25);
        stack.Z = Unsafe.Add(ref selfRef, 26);
        stack.W = Unsafe.Add(ref selfRef, 27);

        V3L = stack;

        stack = V3R;

        stack.X = Unsafe.Add(ref selfRef, 28);
        stack.Y = Unsafe.Add(ref selfRef, 29);
        stack.Z = Unsafe.Add(ref selfRef, 30);
        stack.W = Unsafe.Add(ref selfRef, 31);

        V3R = stack;

        stack = V4L;

        stack.X = Unsafe.Add(ref selfRef, 32);
        stack.Y = Unsafe.Add(ref selfRef, 33);
        stack.Z = Unsafe.Add(ref selfRef, 34);
        stack.W = Unsafe.Add(ref selfRef, 35);

        V4L = stack;

        stack = V4R;

        stack.X = Unsafe.Add(ref selfRef, 36);
        stack.Y = Unsafe.Add(ref selfRef, 37);
        stack.Z = Unsafe.Add(ref selfRef, 38);
        stack.W = Unsafe.Add(ref selfRef, 39);

        V4R = stack;

        stack = V5L;

        stack.X = Unsafe.Add(ref selfRef, 40);
        stack.Y = Unsafe.Add(ref selfRef, 41);
        stack.Z = Unsafe.Add(ref selfRef, 42);
        stack.W = Unsafe.Add(ref selfRef, 43);

        V5L = stack;

        stack = V5R;

        stack.X = Unsafe.Add(ref selfRef, 44);
        stack.Y = Unsafe.Add(ref selfRef, 45);
        stack.Z = Unsafe.Add(ref selfRef, 46);
        stack.W = Unsafe.Add(ref selfRef, 47);

        V5R = stack;

        stack = V6L;

        stack.X = Unsafe.Add(ref selfRef, 48);
        stack.Y = Unsafe.Add(ref selfRef, 49);
        stack.Z = Unsafe.Add(ref selfRef, 50);
        stack.W = Unsafe.Add(ref selfRef, 51);

        V6L = stack;

        stack = V6R;

        stack.X = Unsafe.Add(ref selfRef, 52);
        stack.Y = Unsafe.Add(ref selfRef, 53);
        stack.Z = Unsafe.Add(ref selfRef, 54);
        stack.W = Unsafe.Add(ref selfRef, 55);

        V6R = stack;

        stack = V7L;

        stack.X = Unsafe.Add(ref selfRef, 56);
        stack.Y = Unsafe.Add(ref selfRef, 57);
        stack.Z = Unsafe.Add(ref selfRef, 58);
        stack.W = Unsafe.Add(ref selfRef, 59);

        V7L = stack;

        stack = V7R;

        stack.X = Unsafe.Add(ref selfRef, 60);
        stack.Y = Unsafe.Add(ref selfRef, 61);
        stack.Z = Unsafe.Add(ref selfRef, 62);
        stack.W = Unsafe.Add(ref selfRef, 63);

        V7R = stack;
    }

    /// <summary>
    /// Loads values from <paramref name="source"/> using extended AVX2 intrinsics.
    /// </summary>
    /// <param name="source">The source <see cref="Block8x8"/></param>
    public void LoadFromInt16ExtendedAvx2(Block source)
    {
        ref short sRef = ref Unsafe.As<byte, short>(ref Array[Offset]);
        ref Vector256<float> dRef = ref Unsafe.As<byte, Vector256<float>>(ref Array[Offset]);

        // Vector256<ushort>.Count == 16 on AVX2
        // We can process 2 block rows in a single step
        Vector256<int> top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef));
        Vector256<int> bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)Vector256<int>.Count));
        dRef = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 1) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 2)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 3)));
        Unsafe.Add(ref dRef, 2) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 3) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 4)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 5)));
        Unsafe.Add(ref dRef, 4) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 5) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 6)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 7)));
        Unsafe.Add(ref dRef, 6) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 7) = Avx.ConvertToVector256Single(bottom);
    }

    /// <summary>
    /// Compares entire 8x8 block to a single scalar value.
    /// </summary>
    /// <param name="value">Value to compare to.</param>
    public bool EqualsToScalar(int value)
    {
        if (Avx2.IsSupported)
        {
            const int equalityMask = unchecked((int)0b1111_1111_1111_1111_1111_1111_1111_1111);

            Vector256<int> targetVector = Vector256.Create(value);
            Vector256<float> v0f = V0f;
            ref Vector256<float> blockStride = ref v0f;

            for (nuint i = 0; i < RowCount; i++)
            {
                Vector256<int> areEqual = Avx2.CompareEqual(Avx.ConvertToVector256Int32WithTruncation(Unsafe.Add(ref v0f, i)), targetVector);
                if (Avx2.MoveMask(areEqual.AsByte()) != equalityMask)
                {
                    return false;
                }
            }

            return true;
        }

        ref float scalars = ref Unsafe.As<byte, float>(ref Array[Offset]);

        for (nuint i = 0, e = (nuint)FloatLength; i < e; i++)
        {
            if ((int)Unsafe.Add(ref scalars, i) != value)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns index of the last non-zero element in given matrix.
    /// </summary>
    /// <returns>
    /// Index of the last non-zero element. Returns -1 if all elements are equal to zero.
    /// </returns>
    public nint GetLastNonZeroIndex()
    {
        if (Avx2.IsSupported)
        {
            const int equalityMask = unchecked((int)0b1111_1111_1111_1111_1111_1111_1111_1111);

            Vector256<short> zero16 = Vector256<short>.Zero;

            ref Vector256<short> mcuStride = ref Unsafe.As<byte, Vector256<short>>(ref Array[Offset]);

            for (nint i = 3; i >= 0; i--)
            {
                int areEqual = Avx2.MoveMask(Avx2.CompareEqual(Unsafe.Add(ref mcuStride, i), zero16).AsByte());

                if (areEqual != equalityMask)
                {
                    // Each 2 bits represents comparison operation for each 2-byte element in input vectors
                    // LSB represents first element in the stride
                    // MSB represents last element in the stride
                    // lzcnt operation would calculate number of zero numbers at the end

                    // Given mask is not actually suitable for lzcnt as 1's represent zero elements and 0's represent non-zero elements
                    // So we need to invert it
                    uint lzcnt = (uint)BitOperations.LeadingZeroCount(~(uint)areEqual);

                    // As input number is represented by 2 bits in the mask, we need to divide lzcnt result by 2
                    // to get the exact number of zero elements in the stride
                    uint strideRelativeIndex = 15 - (lzcnt / 2);
                    return (i * 16) + (nint)strideRelativeIndex;
                }
            }

            return -1;
        }
        else
        {
            nint index = ShortLength - 1;
            ref short elemRef = ref Unsafe.As<byte, short>(ref Array[Offset]);

            while (index >= 0 && Unsafe.Add(ref elemRef, index) == 0)
            {
                index--;
            }

            return index;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="maximum"></param>
    public void NormalizeColorsAndRoundInPlaceVector8(float maximum)
    {
        var off = new Vector<float>(MathF.Ceiling(maximum * 0.5F));
        var max = new Vector<float>(maximum);

        var v0l = V0L;
        ref Vector<float> row0 = ref Unsafe.As<Vector4, Vector<float>>(ref v0l);
        row0 = NormalizeAndRound(row0, off, max);

        var v1l = V1L;
        ref Vector<float> row1 = ref Unsafe.As<Vector4, Vector<float>>(ref v1l);
        row1 = NormalizeAndRound(row1, off, max);

        var v2l = V2L;
        ref Vector<float> row2 = ref Unsafe.As<Vector4, Vector<float>>(ref v2l);
        row2 = NormalizeAndRound(row2, off, max);

        var v3l = V3L;
        ref Vector<float> row3 = ref Unsafe.As<Vector4, Vector<float>>(ref v3l);
        row3 = NormalizeAndRound(row3, off, max);

        var v4l = V4L;
        ref Vector<float> row4 = ref Unsafe.As<Vector4, Vector<float>>(ref v4l);
        row4 = NormalizeAndRound(row4, off, max);

        var v5l = V5L;
        ref Vector<float> row5 = ref Unsafe.As<Vector4, Vector<float>>(ref v5l);
        row5 = NormalizeAndRound(row5, off, max);

        var v6l = V6L;
        ref Vector<float> row6 = ref Unsafe.As<Vector4, Vector<float>>(ref v6l);
        row6 = NormalizeAndRound(row6, off, max);

        var v7l = V7L;
        ref Vector<float> row7 = ref Unsafe.As<Vector4, Vector<float>>(ref v7l);
        row7 = NormalizeAndRound(row7, off, max);

    }

    /// <summary>
    /// Level shift by +maximum/2, clip to [0, maximum]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void NormalizeColorsInPlace(float maximum)
    {
        var CMin4 = new Vector4(0F);
        var CMax4 = new Vector4(maximum);
        var COff4 = new Vector4(MathF.Ceiling(maximum * 0.5F));

        V0L = Vector4.Clamp(V0L + COff4, CMin4, CMax4);
        V0R = Vector4.Clamp(V0R + COff4, CMin4, CMax4);
        V1L = Vector4.Clamp(V1L + COff4, CMin4, CMax4);
        V1R = Vector4.Clamp(V1R + COff4, CMin4, CMax4);
        V2L = Vector4.Clamp(V2L + COff4, CMin4, CMax4);
        V2R = Vector4.Clamp(V2R + COff4, CMin4, CMax4);
        V3L = Vector4.Clamp(V3L + COff4, CMin4, CMax4);
        V3R = Vector4.Clamp(V3R + COff4, CMin4, CMax4);
        V4L = Vector4.Clamp(V4L + COff4, CMin4, CMax4);
        V4R = Vector4.Clamp(V4R + COff4, CMin4, CMax4);
        V5L = Vector4.Clamp(V5L + COff4, CMin4, CMax4);
        V5R = Vector4.Clamp(V5R + COff4, CMin4, CMax4);
        V6L = Vector4.Clamp(V6L + COff4, CMin4, CMax4);
        V6R = Vector4.Clamp(V6R + COff4, CMin4, CMax4);
        V7L = Vector4.Clamp(V7L + COff4, CMin4, CMax4);
        V7R = Vector4.Clamp(V7R + COff4, CMin4, CMax4);
    }

    /// <summary>
    /// Multiply all elements of the block.
    /// </summary>
    /// <param name="value">The value to multiply by.</param>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MultiplyInPlace(float value)
    {
        if (Avx.IsSupported)
        {
            Vector256<float> valueVec = Vector256.Create(value);
            V0f = Avx.Multiply(V0f, valueVec);
            V1f = Avx.Multiply(V1f, valueVec);
            V2f = Avx.Multiply(V2f, valueVec);
            V3f = Avx.Multiply(V3f, valueVec);
            V4f = Avx.Multiply(V4f, valueVec);
            V5f = Avx.Multiply(V5f, valueVec);
            V6f = Avx.Multiply(V6f, valueVec);
            V7f = Avx.Multiply(V7f, valueVec);
        }
        else
        {
            Vector4 valueVec = new(value);
            V0L *= valueVec;
            V0R *= valueVec;
            V1L *= valueVec;
            V1R *= valueVec;
            V2L *= valueVec;
            V2R *= valueVec;
            V3L *= valueVec;
            V3R *= valueVec;
            V4L *= valueVec;
            V4R *= valueVec;
            V5L *= valueVec;
            V5R *= valueVec;
            V6L *= valueVec;
            V6R *= valueVec;
            V7L *= valueVec;
            V7R *= valueVec;
        }
    }

    /// <summary>
    /// Multiply all elements of the block by the corresponding elements of 'other'.
    /// </summary>
    /// <param name="other">The other block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MultiplyInPlace(Block other)
    {
        if (Avx.IsSupported)
        {
            V0f = Avx.Multiply(V0f, other.V0f);
            V1f = Avx.Multiply(V1f, other.V1f);
            V2f = Avx.Multiply(V2f, other.V2f);
            V3f = Avx.Multiply(V3f, other.V3f);
            V4f = Avx.Multiply(V4f, other.V4f);
            V5f = Avx.Multiply(V5f, other.V5f);
            V6f = Avx.Multiply(V6f, other.V6f);
            V7f = Avx.Multiply(V7f, other.V7f);
        }
        else
        {
            V0L *= other.V0L;
            V0R *= other.V0R;
            V1L *= other.V1L;
            V1R *= other.V1R;
            V2L *= other.V2L;
            V2R *= other.V2R;
            V3L *= other.V3L;
            V3R *= other.V3R;
            V4L *= other.V4L;
            V4R *= other.V4R;
            V5L *= other.V5L;
            V5R *= other.V5R;
            V6L *= other.V6L;
            V6R *= other.V6R;
            V7L *= other.V7L;
            V7R *= other.V7R;
        }
    }

    /// <summary>
    /// Adds a vector to all elements of the block.
    /// </summary>
    /// <param name="value">The added vector.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddInPlace(float value)
    {
        if (Avx.IsSupported)
        {
            Vector256<float> valueVec = Vector256.Create(value);
            V0f = Avx.Add(V0f, valueVec);
            V1f = Avx.Add(V1f, valueVec);
            V2f = Avx.Add(V2f, valueVec);
            V3f = Avx.Add(V3f, valueVec);
            V4f = Avx.Add(V4f, valueVec);
            V5f = Avx.Add(V5f, valueVec);
            V6f = Avx.Add(V6f, valueVec);
            V7f = Avx.Add(V7f, valueVec);
        }
        else
        {
            Vector4 valueVec = new(value);
            V0L += valueVec;
            V0R += valueVec;
            V1L += valueVec;
            V1R += valueVec;
            V2L += valueVec;
            V2R += valueVec;
            V3L += valueVec;
            V3R += valueVec;
            V4L += valueVec;
            V4R += valueVec;
            V5L += valueVec;
            V5R += valueVec;
            V6L += valueVec;
            V6R += valueVec;
            V7L += valueVec;
            V7R += valueVec;
        }
    }

    /// <summary>
    /// Quantize input block, transpose, apply zig-zag ordering and store as <see cref="Block8x8"/>.
    /// </summary>
    /// <param name="block">Source block.</param>
    /// <param name="dest">Destination block.</param>
    /// <param name="qt">The quantization table.</param>
    public static void Quantize(Block block, Block dest, Block qt)
    {
        if (Avx2.IsSupported)
        {
            MultiplyIntoInt16_Avx2(block, qt, dest);
            ZigZag.ApplyTransposingZigZagOrderingAvx2(dest);
        }
        else if (Ssse3.IsSupported)
        {
            MultiplyIntoInt16_Sse2(block, qt, dest);
            ZigZag.ApplyTransposingZigZagOrderingSsse3(dest);
        }
        else
        {
            for (int i = 0, e = block.FloatLength; i < e; i++)
            {
                int idx = ZigZag.TransposingOrder[i];
                float quantizedVal = block[idx] * qt[idx];
                quantizedVal += quantizedVal < 0 ? -0.5f : 0.5f;
                dest[i] = (short)quantizedVal;
            }
        }
    }

    public void RoundInto(Block dest)
    {
        for (int i = 0, e = FloatLength; i < e; i++)
        {
            float val = this[i];

            if (val < 0)
            {
                val -= 0.5f;
            }
            else
            {
                val += 0.5f;
            }

            dest[i] = (short)val;
        }
    }

    public Block RoundAsInt16Block()
    {
        Block result = new Block();
        RoundInto(result);
        return result;
    }

    /// <summary>
    /// Level shift by +maximum/2, clip to [0..maximum], and round all the values in the block.
    /// </summary>
    /// <param name="maximum">The maximum value.</param>
    public void NormalizeColorsAndRoundInPlace(float maximum)
    {
        if (HasVector8)
        {
            NormalizeColorsAndRoundInPlaceVector8(maximum);
        }
        else
        {
            NormalizeColorsInPlace(maximum);
            RoundInPlace();
        }
    }

    /// <summary>
    /// Rounds all values in the block.
    /// </summary>
    public void RoundInPlace()
    {
        for (uint i = 0, e = (uint)FloatLength; i < e; i++)
        {
            this[i] = MathF.Round(this[i]);
        }
    }

    /// <summary>
    /// Transpose the block inplace.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TransposeInplace()
    {
        if (Avx.IsSupported)
        {
            TransposeInplace_Avx();
        }
        else
        {
            TransposeInplace_Scalar();
        }
    }

    /// <summary>
    /// Transposes the block in place with scalar instructions.
    /// </summary>
    private void TransposeInplace_Scalar()
    {
        ref float elemRef = ref Unsafe.As<byte, float>(ref Array[Offset]);

        // row #0
        Swap(ref Unsafe.Add(ref elemRef, 1), ref Unsafe.Add(ref elemRef, 8));
        Swap(ref Unsafe.Add(ref elemRef, 2), ref Unsafe.Add(ref elemRef, 16));
        Swap(ref Unsafe.Add(ref elemRef, 3), ref Unsafe.Add(ref elemRef, 24));
        Swap(ref Unsafe.Add(ref elemRef, 4), ref Unsafe.Add(ref elemRef, 32));
        Swap(ref Unsafe.Add(ref elemRef, 5), ref Unsafe.Add(ref elemRef, 40));
        Swap(ref Unsafe.Add(ref elemRef, 6), ref Unsafe.Add(ref elemRef, 48));
        Swap(ref Unsafe.Add(ref elemRef, 7), ref Unsafe.Add(ref elemRef, 56));

        // row #1
        Swap(ref Unsafe.Add(ref elemRef, 10), ref Unsafe.Add(ref elemRef, 17));
        Swap(ref Unsafe.Add(ref elemRef, 11), ref Unsafe.Add(ref elemRef, 25));
        Swap(ref Unsafe.Add(ref elemRef, 12), ref Unsafe.Add(ref elemRef, 33));
        Swap(ref Unsafe.Add(ref elemRef, 13), ref Unsafe.Add(ref elemRef, 41));
        Swap(ref Unsafe.Add(ref elemRef, 14), ref Unsafe.Add(ref elemRef, 49));
        Swap(ref Unsafe.Add(ref elemRef, 15), ref Unsafe.Add(ref elemRef, 57));

        // row #2
        Swap(ref Unsafe.Add(ref elemRef, 19), ref Unsafe.Add(ref elemRef, 26));
        Swap(ref Unsafe.Add(ref elemRef, 20), ref Unsafe.Add(ref elemRef, 34));
        Swap(ref Unsafe.Add(ref elemRef, 21), ref Unsafe.Add(ref elemRef, 42));
        Swap(ref Unsafe.Add(ref elemRef, 22), ref Unsafe.Add(ref elemRef, 50));
        Swap(ref Unsafe.Add(ref elemRef, 23), ref Unsafe.Add(ref elemRef, 58));

        // row #3
        Swap(ref Unsafe.Add(ref elemRef, 28), ref Unsafe.Add(ref elemRef, 35));
        Swap(ref Unsafe.Add(ref elemRef, 29), ref Unsafe.Add(ref elemRef, 43));
        Swap(ref Unsafe.Add(ref elemRef, 30), ref Unsafe.Add(ref elemRef, 51));
        Swap(ref Unsafe.Add(ref elemRef, 31), ref Unsafe.Add(ref elemRef, 59));

        // row #4
        Swap(ref Unsafe.Add(ref elemRef, 37), ref Unsafe.Add(ref elemRef, 44));
        Swap(ref Unsafe.Add(ref elemRef, 38), ref Unsafe.Add(ref elemRef, 52));
        Swap(ref Unsafe.Add(ref elemRef, 39), ref Unsafe.Add(ref elemRef, 60));

        // row #5
        Swap(ref Unsafe.Add(ref elemRef, 46), ref Unsafe.Add(ref elemRef, 53));
        Swap(ref Unsafe.Add(ref elemRef, 47), ref Unsafe.Add(ref elemRef, 61));

        // row #6
        Swap(ref Unsafe.Add(ref elemRef, 55), ref Unsafe.Add(ref elemRef, 62));
    }

    /// <summary>
    /// Gets a <see cref="Span{float}"/> of length 4 in the block at the given index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    internal Span<float> GetFourFloats(int index)
    {
        var span = this.ToSpan();
        return MemoryMarshal.Cast<byte, float>(MemoryMarshal.CreateSpan(ref span[index], Binary.BytesPerInteger * Binary.Four));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0, e = ShortLength; i < e; i++)
        {
            sb.Append(this[i]);
            if (i < e - 1)
            {
                sb.Append(',');
            }
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
     => obj is Block block && Equals(block);

    /// <summary>
    /// Determines if the current instance is equal to the other instance
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(Block other)
     => V0L == other.V0L
        && V0R == other.V0R
        && V1L == other.V1L
        && V1R == other.V1R
        && V2L == other.V2L
        && V2R == other.V2R
        && V3L == other.V3L
        && V3R == other.V3R
        && V4L == other.V4L
        && V4R == other.V4R
        && V5L == other.V5L
        && V5R == other.V5R
        && V6L == other.V6L
        && V6R == other.V6R
        && V7L == other.V7L
        && V7R == other.V7R;

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int left = HashCode.Combine(
            V0L,
            V1L,
            V2L,
            V3L,
            V4L,
            V5L,
            V6L,
            V7L);

        int right = HashCode.Combine(
            V0R,
            V1R,
            V2R,
            V3R,
            V4R,
            V5R,
            V6R,
            V7R);

        return HashCode.Combine(left, right);
    }

    #endregion

    #region ScaledCopy

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ScaledCopyFrom(ref float areaOrigin, int areaStride) =>
        CopyFrom1x1Scale(ref Unsafe.As<float, byte>(ref areaOrigin), ref GetReference(0), areaStride);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ScaledCopyTo(ref float areaOrigin, int areaStride, int horizontalScale, int verticalScale)
    {
        if (horizontalScale == 1 && verticalScale == 1)
        {
            CopyTo1x1Scale(ref GetReference(0), ref Unsafe.As<float, byte>(ref areaOrigin), areaStride);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 2)
        {
            CopyTo2x2Scale(ref areaOrigin, areaStride);
            return;
        }

        // TODO: Optimize: implement all cases with scale-specific, loopless code!
        CopyArbitraryScale(ref areaOrigin, (uint)areaStride, (uint)horizontalScale, (uint)verticalScale);
    }

    private void CopyTo2x2Scale(ref float areaOrigin, int areaStride)
    {
        ref Vector2 destBase = ref Unsafe.As<float, Vector2>(ref areaOrigin);
        nuint destStride = (uint)areaStride / 2;

        var vector = V0L;

        WidenCopyRowImpl2x2(ref vector, ref destBase, 0, destStride);
        WidenCopyRowImpl2x2(ref vector, ref destBase, 1, destStride);
        WidenCopyRowImpl2x2(ref vector, ref destBase, 2, destStride);
        WidenCopyRowImpl2x2(ref vector, ref destBase, 3, destStride);
        WidenCopyRowImpl2x2(ref vector, ref destBase, 4, destStride);
        WidenCopyRowImpl2x2(ref vector, ref destBase, 5, destStride);
        WidenCopyRowImpl2x2(ref vector, ref destBase, 6, destStride);
        WidenCopyRowImpl2x2(ref vector, ref destBase, 7, destStride);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WidenCopyRowImpl2x2(ref Vector4 selfBase, ref Vector2 destBase, nuint row, nuint destStride)
        {
            ref Vector4 sLeft = ref Unsafe.Add(ref selfBase, 2 * row);
            ref Vector4 sRight = ref Unsafe.Add(ref sLeft, 1);

            nuint offset = 2 * row * destStride;
            ref Vector4 dTopLeft = ref Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref destBase, offset));
            ref Vector4 dBottomLeft = ref Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref destBase, offset + destStride));

            var xyLeft = new Vector4(sLeft.X);
            xyLeft.Z = sLeft.Y;
            xyLeft.W = sLeft.Y;

            var zwLeft = new Vector4(sLeft.Z);
            zwLeft.Z = sLeft.W;
            zwLeft.W = sLeft.W;

            var xyRight = new Vector4(sRight.X);
            xyRight.Z = sRight.Y;
            xyRight.W = sRight.Y;

            var zwRight = new Vector4(sRight.Z);
            zwRight.Z = sRight.W;
            zwRight.W = sRight.W;

            dTopLeft = xyLeft;
            Unsafe.Add(ref dTopLeft, 1) = zwLeft;
            Unsafe.Add(ref dTopLeft, 2) = xyRight;
            Unsafe.Add(ref dTopLeft, 3) = zwRight;

            dBottomLeft = xyLeft;
            Unsafe.Add(ref dBottomLeft, 1) = zwLeft;
            Unsafe.Add(ref dBottomLeft, 2) = xyRight;
            Unsafe.Add(ref dBottomLeft, 3) = zwRight;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopyArbitraryScale(ref float areaOrigin, uint areaStride, uint horizontalScale, uint verticalScale)
    {
        for (nuint y = 0; y < 8; y++)
        {
            nuint yy = y * verticalScale;
            nuint y8 = y * 8;

            for (nuint x = 0; x < 8; x++)
            {
                nuint xx = x * horizontalScale;

                float value = this[(int)(y8 + x)];
                nuint baseIdx = (yy * areaStride) + xx;

                for (nuint i = 0; i < verticalScale; i++, baseIdx += areaStride)
                {
                    for (nuint j = 0; j < horizontalScale; j++)
                    {
                        // area[xx + j, yy + i] = value;
                        Unsafe.Add(ref areaOrigin, baseIdx + j) = value;
                    }
                }
            }
        }
    }

    private static void CopyTo1x1Scale(ref byte origin, ref byte dest, int areaStride)
    {
        int destStride = areaStride * sizeof(float);

        CopyRowImpl(ref origin, ref dest, destStride, 0);
        CopyRowImpl(ref origin, ref dest, destStride, 1);
        CopyRowImpl(ref origin, ref dest, destStride, 2);
        CopyRowImpl(ref origin, ref dest, destStride, 3);
        CopyRowImpl(ref origin, ref dest, destStride, 4);
        CopyRowImpl(ref origin, ref dest, destStride, 5);
        CopyRowImpl(ref origin, ref dest, destStride, 6);
        CopyRowImpl(ref origin, ref dest, destStride, 7);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyRowImpl(ref byte origin, ref byte dest, int destStride, int row)
        {
            origin = ref Unsafe.Add(ref origin, (uint)row * 8 * sizeof(float));
            dest = ref Unsafe.Add(ref dest, (uint)(row * destStride));
            Unsafe.CopyBlock(ref dest, ref origin, 8 * sizeof(float));
        }
    }

    private static void CopyFrom1x1Scale(ref byte origin, ref byte dest, int areaStride)
    {
        int destStride = areaStride * sizeof(float);

        CopyRowImpl(ref origin, ref dest, destStride, 0);
        CopyRowImpl(ref origin, ref dest, destStride, 1);
        CopyRowImpl(ref origin, ref dest, destStride, 2);
        CopyRowImpl(ref origin, ref dest, destStride, 3);
        CopyRowImpl(ref origin, ref dest, destStride, 4);
        CopyRowImpl(ref origin, ref dest, destStride, 5);
        CopyRowImpl(ref origin, ref dest, destStride, 6);
        CopyRowImpl(ref origin, ref dest, destStride, 7);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyRowImpl(ref byte origin, ref byte dest, int sourceStride, int row)
        {
            origin = ref Unsafe.Add(ref origin, (uint)(row * sourceStride));
            dest = ref Unsafe.Add(ref dest, (uint)row * 8 * sizeof(float));
            Unsafe.CopyBlock(ref dest, ref origin, 8 * sizeof(float));
        }
    }

    #endregion
}
