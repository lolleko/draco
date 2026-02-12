using System.Runtime.CompilerServices;

namespace DracoSharp.Core;

public class DataBuffer
{
    private byte[] _data = [];

    public DataBuffer() { }

    public DataBuffer(int initialSize)
    {
        _data = new byte[initialSize];
    }

    public void Resize(long newSize)
    {
        Array.Resize(ref _data, (int)newSize);
    }

    public void Update(ReadOnlySpan<byte> data)
    {
        _data = data.ToArray();
    }

    public void Update(ReadOnlySpan<byte> data, long offset)
    {
        long requiredSize = offset + data.Length;
        if (requiredSize > _data.Length)
            Resize(requiredSize);
        data.CopyTo(_data.AsSpan((int)offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Read(long bytePos, Span<byte> output)
    {
        _data.AsSpan((int)bytePos, output.Length).CopyTo(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(long bytePos, ReadOnlySpan<byte> input)
    {
        input.CopyTo(_data.AsSpan((int)bytePos));
    }

    public void CopyFrom(long dstOffset, DataBuffer srcBuf, long srcOffset, long size)
    {
        srcBuf._data.AsSpan((int)srcOffset, (int)size)
            .CopyTo(_data.AsSpan((int)dstOffset));
    }

    public long DataSize => _data.Length;
    public ReadOnlySpan<byte> Data => _data;
    public Span<byte> MutableData => _data;
}
