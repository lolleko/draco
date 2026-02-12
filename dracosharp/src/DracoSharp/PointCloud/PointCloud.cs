using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.PointCloud;

public class PointCloud
{
    private readonly List<PointAttribute> _attributes = [];
    private readonly List<int>[] _namedAttributeIndex;
    private int _numPoints;

    public PointCloud()
    {
        _namedAttributeIndex = new List<int>[(int)GeometryAttribute.AttributeType.NamedAttributesCount];
        for (int i = 0; i < _namedAttributeIndex.Length; i++)
            _namedAttributeIndex[i] = [];
    }

    public int NumNamedAttributes(GeometryAttribute.AttributeType type)
    {
        int idx = (int)type;
        return idx >= 0 && idx < _namedAttributeIndex.Length ? _namedAttributeIndex[idx].Count : 0;
    }

    public int GetNamedAttributeId(GeometryAttribute.AttributeType type) =>
        GetNamedAttributeId(type, 0);

    public int GetNamedAttributeId(GeometryAttribute.AttributeType type, int i)
    {
        int idx = (int)type;
        if (idx < 0 || idx >= _namedAttributeIndex.Length)
            return -1;
        if (i < 0 || i >= _namedAttributeIndex[idx].Count)
            return -1;
        return _namedAttributeIndex[idx][i];
    }

    public PointAttribute GetNamedAttribute(GeometryAttribute.AttributeType type)
    {
        int id = GetNamedAttributeId(type);
        return id < 0 ? null! : _attributes[id];
    }

    public PointAttribute GetNamedAttribute(GeometryAttribute.AttributeType type, int i)
    {
        int id = GetNamedAttributeId(type, i);
        return id < 0 ? null! : _attributes[id];
    }

    public PointAttribute GetAttributeByUniqueId(uint id)
    {
        int attId = GetAttributeIdByUniqueId(id);
        return attId < 0 ? null! : _attributes[attId];
    }

    public int GetAttributeIdByUniqueId(uint uniqueId)
    {
        for (int i = 0; i < _attributes.Count; i++)
        {
            if (_attributes[i].UniqueId == uniqueId)
                return i;
        }
        return -1;
    }

    public int NumAttributes => _attributes.Count;

    public PointAttribute Attribute(int attId) => _attributes[attId];

    public int AddAttribute(PointAttribute pa)
    {
        int attId = _attributes.Count;
        _attributes.Add(pa);

        var attType = pa.Type;
        if (attType >= 0 && (int)attType < _namedAttributeIndex.Length)
            _namedAttributeIndex[(int)attType].Add(attId);

        return attId;
    }

    public virtual void SetAttribute(int attId, PointAttribute pa)
    {
        if (attId >= _attributes.Count)
        {
            while (_attributes.Count <= attId)
                _attributes.Add(null!);
        }

        var oldType = _attributes[attId]?.Type ?? GeometryAttribute.AttributeType.Invalid;
        if (oldType >= 0 && (int)oldType < _namedAttributeIndex.Length)
            _namedAttributeIndex[(int)oldType].Remove(attId);

        _attributes[attId] = pa;

        var newType = pa.Type;
        if (newType >= 0 && (int)newType < _namedAttributeIndex.Length)
        {
            if (!_namedAttributeIndex[(int)newType].Contains(attId))
                _namedAttributeIndex[(int)newType].Add(attId);
        }
    }

    public int NumPoints
    {
        get => _numPoints;
        set => _numPoints = value;
    }
}
