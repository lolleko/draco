using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class AttributesDecoder
{
    private readonly List<int> _pointAttributeIds = [];
    private PointCloudDecoder _pointCloudDecoder;

    public int NumAttributes => _pointAttributeIds.Count;

    public int GetAttributeId(int i) => _pointAttributeIds[i];

    public virtual bool Init(PointCloudDecoder decoder, DecoderBuffer buffer)
    {
        _pointCloudDecoder = decoder;
        return true;
    }

    public virtual bool DecodeAttributesDecoderData(DecoderBuffer buffer)
    {
        uint numAttributes;
        if (_pointCloudDecoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
        {
            if (!buffer.Decode(out numAttributes))
                return false;
        }
        else
        {
            if (!buffer.DecodeVarint(out numAttributes))
                return false;
        }
        if (numAttributes == 0)
            return false;

        var pointCloud = _pointCloudDecoder.PointCloud;

        for (uint i = 0; i < numAttributes; i++)
        {
            if (!buffer.Decode(out byte attType))
                return false;
            if (!buffer.Decode(out byte dataType))
                return false;
            if (!buffer.Decode(out byte numComponents))
                return false;
            if (!buffer.Decode(out byte normalized))
                return false;

            if (attType >= (byte)GeometryAttribute.AttributeType.NamedAttributesCount)
                return false;
            if (dataType == 0 || dataType >= (byte)DataType.TypesCount)
                return false;
            if (numComponents == 0)
                return false;

            uint uniqueId;
            if (_pointCloudDecoder.BitstreamVersion < BitstreamVersion.Make(1, 3))
            {
                if (!buffer.Decode(out ushort customId))
                    return false;
                uniqueId = customId;
            }
            else
            {
                if (!buffer.DecodeVarint(out uniqueId))
                    return false;
            }

            var pa = new PointAttribute();
            pa.Init(
                (GeometryAttribute.AttributeType)attType,
                numComponents,
                (DataType)dataType,
                normalized != 0,
                0);
            pa.UniqueId = uniqueId;

            int attId = pointCloud.AddAttribute(pa);
            _pointAttributeIds.Add(attId);
        }

        return true;
    }

    public virtual bool DecodeAttributes(DecoderBuffer buffer)
    {
        if (!DecodePortableAttributes(buffer))
            return false;
        if (!DecodeDataNeededByPortableTransforms(buffer))
            return false;
        if (!TransformAttributesToOriginalFormat())
            return false;
        return true;
    }

    protected virtual bool DecodePortableAttributes(DecoderBuffer buffer) => true;
    protected virtual bool DecodeDataNeededByPortableTransforms(DecoderBuffer buffer) => true;
    protected virtual bool TransformAttributesToOriginalFormat() => true;

    protected PointCloudDecoder PointCloudDecoder => _pointCloudDecoder;

    public virtual PointAttribute GetPortableAttribute(int pointAttributeId) => null;

    protected int GetLocalIdForPointAttribute(int pointAttributeId)
    {
        for (int i = 0; i < _pointAttributeIds.Count; i++)
        {
            if (_pointAttributeIds[i] == pointAttributeId)
                return i;
        }
        return -1;
    }
}
