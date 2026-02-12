using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class SequentialAttributeDecodersController : AttributesDecoder
{
    private readonly List<SequentialAttributeDecoder> _sequentialDecoders = [];
    private int[] _pointIds = [];
    private readonly PointsSequencer _sequencer;

    public SequentialAttributeDecodersController() : this(null) { }

    public SequentialAttributeDecodersController(PointsSequencer sequencer)
    {
        _sequencer = sequencer;
    }

    public override bool DecodeAttributesDecoderData(DecoderBuffer buffer)
    {
        if (!base.DecodeAttributesDecoderData(buffer))
            return false;

        int numAttributes = NumAttributes;
        _sequentialDecoders.Clear();
        for (int i = 0; i < numAttributes; i++)
        {
            if (!buffer.Decode(out byte decoderType))
                return false;
            var decoder = CreateSequentialDecoder((SequentialAttributeEncoderType)decoderType);
            if (decoder == null)
                return false;
            if (!decoder.Init(PointCloudDecoder, GetAttributeId(i)))
                return false;
            _sequentialDecoders.Add(decoder);
        }
        return true;
    }

    public override bool DecodeAttributes(DecoderBuffer buffer)
    {
        if (_sequencer != null)
        {
            // Use the provided sequencer (edgebreaker path).
            var pointIdList = new List<int>();
            if (!_sequencer.GenerateSequence(pointIdList))
                return false;
            _pointIds = pointIdList.ToArray();

            // Update attribute mapping using sequencer.
            for (int i = 0; i < _sequentialDecoders.Count; i++)
            {
                var att = PointCloudDecoder.PointCloud.Attribute(GetAttributeId(i));
                if (!_sequencer.UpdatePointToAttributeIndexMapping(att))
                    return false;
            }
        }
        else
        {
            // Generate a linear point sequence [0..numPoints-1] (sequential path).
            int numPoints = PointCloudDecoder.PointCloud.NumPoints;
            _pointIds = new int[numPoints];
            for (int i = 0; i < numPoints; i++)
                _pointIds[i] = i;

            // Set identity mapping for all attributes.
            for (int i = 0; i < _sequentialDecoders.Count; i++)
            {
                var att = PointCloudDecoder.PointCloud.Attribute(GetAttributeId(i));
                att.SetIdentityMapping();
            }
        }

        // Delegate to base which calls DecodePortableAttributes ->
        // DecodeDataNeededByPortableTransforms -> TransformAttributesToOriginalFormat.
        if (!base.DecodeAttributes(buffer))
            return false;
        return true;
    }

    public override PointAttribute GetPortableAttribute(int pointAttributeId)
    {
        int locId = GetLocalIdForPointAttribute(pointAttributeId);
        if (locId < 0)
            return null;
        return _sequentialDecoders[locId].GetPortableAttribute();
    }

    protected override bool DecodePortableAttributes(DecoderBuffer buffer)
    {
        for (int i = 0; i < _sequentialDecoders.Count; i++)
        {
            if (!_sequentialDecoders[i].DecodePortableAttribute(_pointIds, buffer))
                return false;
        }
        return true;
    }

    protected override bool DecodeDataNeededByPortableTransforms(DecoderBuffer buffer)
    {
        for (int i = 0; i < _sequentialDecoders.Count; i++)
        {
            if (!_sequentialDecoders[i].DecodeDataNeededByPortableTransform(_pointIds, buffer))
                return false;
        }
        return true;
    }

    protected override bool TransformAttributesToOriginalFormat()
    {
        for (int i = 0; i < _sequentialDecoders.Count; i++)
        {
            if (!_sequentialDecoders[i].TransformAttributeToOriginalFormat(_pointIds))
                return false;
        }
        return true;
    }

    private static SequentialAttributeDecoder CreateSequentialDecoder(
        SequentialAttributeEncoderType decoderType) =>
        decoderType switch
        {
            SequentialAttributeEncoderType.Generic => new SequentialAttributeDecoder(),
            SequentialAttributeEncoderType.Integer => new SequentialIntegerAttributeDecoder(),
            SequentialAttributeEncoderType.Quantization => new SequentialQuantizationAttributeDecoder(),
            SequentialAttributeEncoderType.Normals => new SequentialNormalAttributeDecoder(),
            _ => null
        };
}
