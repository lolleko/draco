using System.Buffers.Binary;
using DracoSharp.Core;

namespace DracoSharp.Attributes;

public class AttributeTransformData
{
    private AttributeTransformType _transformType = AttributeTransformType.InvalidTransform;
    private readonly DataBuffer _buffer = new();

    public AttributeTransformType TransformType
    {
        get => _transformType;
        set => _transformType = value;
    }

    public T GetParameterValue<T>(int byteOffset) where T : struct
    {
        Span<byte> temp = stackalloc byte[System.Runtime.InteropServices.Marshal.SizeOf<T>()];
        _buffer.Read(byteOffset, temp);
        return System.Runtime.InteropServices.MemoryMarshal.Read<T>(temp);
    }

    public void SetParameterValue<T>(int byteOffset, T value) where T : struct
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        if (byteOffset + size > _buffer.DataSize)
            _buffer.Resize(byteOffset + size);
        Span<byte> temp = stackalloc byte[size];
        System.Runtime.InteropServices.MemoryMarshal.Write(temp, in value);
        _buffer.Write(byteOffset, temp);
    }

    public void AppendParameterValue<T>(T value) where T : struct
    {
        SetParameterValue((int)_buffer.DataSize, value);
    }
}
