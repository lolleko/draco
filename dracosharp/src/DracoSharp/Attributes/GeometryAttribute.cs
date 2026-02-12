using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using DracoSharp.Core;

namespace DracoSharp.Attributes;

public class GeometryAttribute
{
    public enum AttributeType
    {
        Invalid = -1,
        Position = 0,
        Normal = 1,
        Color = 2,
        TexCoord = 3,
        Generic = 4,
        NamedAttributesCount = 5
    }

    private DataBuffer _buffer = new();
    private byte _numComponents;
    private DataType _dataType;
    private bool _normalized;
    private long _byteStride;
    private long _byteOffset;
    private AttributeType _attributeType = AttributeType.Invalid;
    private uint _uniqueId;

    public void Init(AttributeType attributeType, DataBuffer buffer, byte numComponents,
                     DataType dataType, bool normalized, long byteStride, long byteOffset)
    {
        _buffer = buffer;
        _attributeType = attributeType;
        _numComponents = numComponents;
        _dataType = dataType;
        _normalized = normalized;
        _byteStride = byteStride;
        _byteOffset = byteOffset;
    }

    public bool IsValid => _buffer.DataSize > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetBytePos(int attValueIndex) =>
        _byteOffset + _byteStride * attValueIndex;

    public void GetValue(int attValueIndex, Span<byte> output)
    {
        long bytePos = GetBytePos(attValueIndex);
        _buffer.Read(bytePos, output);
    }

    public void SetAttributeValue(int entryIndex, ReadOnlySpan<byte> value)
    {
        long bytePos = entryIndex * _byteStride;
        _buffer.Write(bytePos, value);
    }

    public int ComponentByteSize => _dataType.ByteLength();
    public int EntryByteSize => _numComponents * _dataType.ByteLength();

    public AttributeType Type
    {
        get => _attributeType;
        set => _attributeType = value;
    }

    public DataType DataType => _dataType;
    public byte NumComponents => _numComponents;

    public bool Normalized
    {
        get => _normalized;
        set => _normalized = value;
    }

    public DataBuffer Buffer => _buffer;

    public long ByteStride => _byteStride;

    public long ByteOffset
    {
        get => _byteOffset;
        set => _byteOffset = value;
    }

    public uint UniqueId
    {
        get => _uniqueId;
        set => _uniqueId = value;
    }

    protected void ResetBuffer(DataBuffer buffer, long byteStride, long byteOffset)
    {
        _buffer = buffer;
        _byteStride = byteStride;
        _byteOffset = byteOffset;
    }

    public void ConvertValue(int attValueIndex, Span<float> output)
    {
        long bytePos = GetBytePos(attValueIndex);
        int components = Math.Min(_numComponents, (byte)output.Length);

        switch (_dataType)
        {
            case DataType.Float32:
                for (int i = 0; i < components; i++)
                    output[i] = BinaryPrimitives.ReadSingleLittleEndian(
                        _buffer.Data.Slice((int)(bytePos + i * 4)));
                break;
            case DataType.Float64:
                for (int i = 0; i < components; i++)
                    output[i] = (float)BinaryPrimitives.ReadDoubleLittleEndian(
                        _buffer.Data.Slice((int)(bytePos + i * 8)));
                break;
            case DataType.Int8:
                for (int i = 0; i < components; i++)
                {
                    sbyte val = (sbyte)_buffer.Data[(int)(bytePos + i)];
                    output[i] = _normalized ? val / (float)sbyte.MaxValue : val;
                }
                break;
            case DataType.UInt8:
                for (int i = 0; i < components; i++)
                {
                    byte val = _buffer.Data[(int)(bytePos + i)];
                    output[i] = _normalized ? val / (float)byte.MaxValue : val;
                }
                break;
            case DataType.Int16:
                for (int i = 0; i < components; i++)
                {
                    short val = BinaryPrimitives.ReadInt16LittleEndian(
                        _buffer.Data.Slice((int)(bytePos + i * 2)));
                    output[i] = _normalized ? val / (float)short.MaxValue : val;
                }
                break;
            case DataType.UInt16:
                for (int i = 0; i < components; i++)
                {
                    ushort val = BinaryPrimitives.ReadUInt16LittleEndian(
                        _buffer.Data.Slice((int)(bytePos + i * 2)));
                    output[i] = _normalized ? val / (float)ushort.MaxValue : val;
                }
                break;
            case DataType.Int32:
                for (int i = 0; i < components; i++)
                {
                    int val = BinaryPrimitives.ReadInt32LittleEndian(
                        _buffer.Data.Slice((int)(bytePos + i * 4)));
                    output[i] = _normalized ? val / (float)int.MaxValue : val;
                }
                break;
            case DataType.UInt32:
                for (int i = 0; i < components; i++)
                {
                    uint val = BinaryPrimitives.ReadUInt32LittleEndian(
                        _buffer.Data.Slice((int)(bytePos + i * 4)));
                    output[i] = _normalized ? val / (float)uint.MaxValue : val;
                }
                break;
            default:
                for (int i = 0; i < output.Length; i++)
                    output[i] = 0f;
                break;
        }

        for (int i = components; i < output.Length; i++)
            output[i] = 0f;
    }
}
