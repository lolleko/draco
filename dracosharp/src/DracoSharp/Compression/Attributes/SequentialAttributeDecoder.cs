using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class SequentialAttributeDecoder
{
    protected PointAttribute Attribute;
    protected PointAttribute PortableAttribute;
    protected int AttributeId;
    private PointCloudDecoder _decoder;

    public virtual bool Init(PointCloudDecoder decoder, int attributeId)
    {
        _decoder = decoder;
        Attribute = decoder.PointCloud.Attribute(attributeId);
        AttributeId = attributeId;
        return true;
    }

    public virtual bool DecodeValues(int[] pointIds, DecoderBuffer buffer)
    {
        // Default implementation: read raw bytes for each point.
        int entrySize = Attribute.NumComponents * Attribute.DataType.ByteLength();
        Span<byte> valueBytes = stackalloc byte[entrySize];

        int numValues = Attribute.Size;
        for (int i = 0; i < numValues; i++)
        {
            if (!buffer.Decode(valueBytes))
                return false;
            Attribute.SetAttributeValue(i, valueBytes);
        }
        return true;
    }

    public virtual bool DecodePortableAttribute(int[] pointIds, DecoderBuffer buffer)
    {
        if (Attribute.NumComponents <= 0 || !Attribute.Reset(pointIds.Length))
            return false;
        if (!DecodeValues(pointIds, buffer))
            return false;
        return true;
    }

    public virtual bool DecodeDataNeededByPortableTransform(
        int[] pointIds, DecoderBuffer buffer)
    {
        // Default: no transform data needed.
        return true;
    }

    public virtual bool TransformAttributeToOriginalFormat(int[] pointIds)
    {
        // Default: no transform needed.
        return true;
    }

    public PointAttribute GetPortableAttribute()
    {
        // Copy point-to-attribute index mapping from the final attribute to the
        // portable attribute if the final attribute has explicit mapping but the
        // portable attribute still uses identity mapping.
        if (!Attribute.IsMappingIdentity && PortableAttribute != null &&
            PortableAttribute.IsMappingIdentity)
        {
            int mapSize = Attribute.IndicesMapSize;
            PortableAttribute.SetExplicitMapping(mapSize);
            for (int i = 0; i < mapSize; i++)
            {
                PortableAttribute.SetPointMapEntry(i, Attribute.MappedIndex(i));
            }
        }
        return PortableAttribute;
    }

    protected PointCloudDecoder Decoder => _decoder;
}
