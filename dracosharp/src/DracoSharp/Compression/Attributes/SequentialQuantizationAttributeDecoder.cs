using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class SequentialQuantizationAttributeDecoder : SequentialIntegerAttributeDecoder
{
    private readonly AttributeQuantizationTransform _quantizationTransform = new();

    public override bool Init(PointCloudDecoder decoder, int attributeId)
    {
        if (!base.Init(decoder, attributeId))
            return false;
        // Currently we can quantize only floating point arguments.
        if (decoder.PointCloud.Attribute(attributeId).DataType != DataType.Float32)
            return false;
        return true;
    }

    protected override bool DecodeIntegerValues(int[] pointIds, DecoderBuffer buffer)
    {
        // For v < 2.0, quantization params are decoded here instead of
        // in DecodeDataNeededByPortableTransform.
        if (Decoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
        {
            if (!_quantizationTransform.DecodeParameters(Attribute, buffer))
                return false;
        }
        return base.DecodeIntegerValues(pointIds, buffer);
    }

    public override bool DecodeDataNeededByPortableTransform(
        int[] pointIds, DecoderBuffer buffer)
    {
        if (Decoder.BitstreamVersion >= BitstreamVersion.Make(2, 0))
        {
            // Decode quantization parameters here only for v >= 2.0.
            var att = GetPortableAttribute() ?? Attribute;
            if (!_quantizationTransform.DecodeParameters(att, buffer))
                return false;
        }

        // Store the decoded transform data in portable attribute.
        return _quantizationTransform.TransferToAttribute(PortableAttribute);
    }

    protected override bool StoreValues(uint numValues)
    {
        return DequantizeValues(numValues);
    }

    private bool DequantizeValues(uint numValues)
    {
        // Convert all quantized values back to floats.
        return _quantizationTransform.InverseTransformAttribute(
            PortableAttribute, Attribute);
    }
}
