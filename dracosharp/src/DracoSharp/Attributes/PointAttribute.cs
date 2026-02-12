using DracoSharp.Core;

namespace DracoSharp.Attributes;

public class PointAttribute : GeometryAttribute
{
    private DataBuffer _attributeBuffer = new();
    private int[] _indicesMap = [];
    private int _numUniqueEntries;
    private bool _identityMapping = true;
    private AttributeTransformData _attributeTransformData = new();
    private bool _hasTransformData;

    public PointAttribute() { }

    public PointAttribute(GeometryAttribute att)
    {
        Type = att.Type;
    }

    public void Init(AttributeType attributeType, byte numComponents, DataType dataType,
                     bool normalized, int numAttributeValues)
    {
        _attributeBuffer = new DataBuffer();
        int entrySize = numComponents * dataType.ByteLength();
        _attributeBuffer.Resize((long)numAttributeValues * entrySize);
        base.Init(attributeType, _attributeBuffer, numComponents, dataType, normalized,
                  entrySize, 0);
        _numUniqueEntries = numAttributeValues;
        SetIdentityMapping();
    }

    public bool Reset(int numAttributeValues)
    {
        int entrySize = NumComponents * DataType.ByteLength();
        if (entrySize <= 0)
            return false;
        _attributeBuffer.Resize((long)numAttributeValues * entrySize);
        ResetBuffer(_attributeBuffer, entrySize, 0);
        _numUniqueEntries = numAttributeValues;
        return true;
    }

    public int Size => _numUniqueEntries;

    public int MappedIndex(int pointIndex) =>
        _identityMapping ? pointIndex : _indicesMap[pointIndex];

    public new DataBuffer Buffer => _attributeBuffer;

    public bool IsMappingIdentity => _identityMapping;

    public int IndicesMapSize => _identityMapping ? 0 : _indicesMap.Length;

    public void Resize(int newNumUniqueEntries)
    {
        int entrySize = NumComponents * DataType.ByteLength();
        _attributeBuffer.Resize((long)newNumUniqueEntries * entrySize);
        ResetBuffer(_attributeBuffer, entrySize, 0);
        _numUniqueEntries = newNumUniqueEntries;
    }

    public void SetIdentityMapping()
    {
        _identityMapping = true;
        _indicesMap = [];
    }

    public void SetExplicitMapping(int numPoints)
    {
        _identityMapping = false;
        _indicesMap = new int[numPoints];
        Array.Fill(_indicesMap, -1);
    }

    public void SetPointMapEntry(int pointIndex, int entryIndex)
    {
        _indicesMap[pointIndex] = entryIndex;
    }

    public void GetMappedValue(int pointIndex, Span<byte> output)
    {
        GetValue(MappedIndex(pointIndex), output);
    }

    public void SetAttributeTransformData(AttributeTransformData transformData)
    {
        _attributeTransformData = transformData;
        _hasTransformData = true;
    }

    public AttributeTransformData GetAttributeTransformData() =>
        _hasTransformData ? _attributeTransformData : null!;

    public bool HasAttributeTransformData => _hasTransformData;
}
