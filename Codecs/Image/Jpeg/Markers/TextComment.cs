﻿using System.Text;
using Media.Codec.Jpeg;
using Media.Common;

namespace Codec.Jpeg.Markers;

public class TextComment : Marker
{
    public string Comment
    {
        get => Encoding.UTF8.GetString(Array, Data.Offset, Data.Count);
        set => Encoding.UTF8.GetBytes(value, 0, value.Length, Data.Array, Data.Offset);
    }

    public TextComment(byte functionCode, string comment)
        : base(functionCode, comment.Length)
    {
        Comment = comment;
    }

    public TextComment(MemorySegment data)
        : base(data)
    {
    }
}
