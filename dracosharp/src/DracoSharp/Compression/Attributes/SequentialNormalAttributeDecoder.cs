using DracoSharp.Attributes;
using DracoSharp.Compression.PredictionSchemes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class SequentialNormalAttributeDecoder : SequentialIntegerAttributeDecoder
{
    private readonly AttributeOctahedronTransform _octahedralTransform = new();

    public override bool Init(PointCloudDecoder decoder, int attributeId)
    {
        if (!base.Init(decoder, attributeId))
            return false;
        if (Attribute.NumComponents != 3)
            return false;
        if (Attribute.DataType != DataType.Float32)
            return false;
        return true;
    }

    protected override int GetNumValueComponents() => 2;

    protected override bool DecodeIntegerValues(int[] pointIds, DecoderBuffer buffer)
    {
        // For v < 2.0, octahedral transform params are decoded here instead of
        // in DecodeDataNeededByPortableTransform.
        if (Decoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
        {
            if (!_octahedralTransform.DecodeParameters(Attribute, buffer))
                return false;
        }
        return base.DecodeIntegerValues(pointIds, buffer);
    }

    public override bool DecodeDataNeededByPortableTransform(
        int[] pointIds, DecoderBuffer buffer)
    {
        if (Decoder.BitstreamVersion >= BitstreamVersion.Make(2, 0))
        {
            // For v >= 2.0, decode parameters here.
            var att = GetPortableAttribute() ?? Attribute;
            if (!_octahedralTransform.DecodeParameters(att, buffer))
                return false;
        }
        return _octahedralTransform.TransferToAttribute(PortableAttribute);
    }

    protected override bool StoreValues(uint numValues)
    {
        return _octahedralTransform.InverseTransformAttribute(
            PortableAttribute, Attribute);
    }

    protected override IPredictionSchemeDecoder CreatePredictionSchemeOverride(
        PredictionSchemeMethod method, PredictionSchemeTransformType transformType)
    {
        if (transformType != PredictionSchemeTransformType.NormalOctahedronCanonicalized)
            return null;

        var transform = new PredictionSchemeNormalOctahedronCanonicalizedDecodingTransform();

        // For mesh prediction methods, create a mesh geometry-based scheme.
        if (method == PredictionSchemeMethod.GeometricNormal &&
            Decoder is MeshDecoder meshDecoder)
        {
            var cornerTable = meshDecoder.GetCornerTable();
            var encodingData = meshDecoder.GetAttributeEncodingData(AttributeId);
            if (cornerTable != null && encodingData != null)
            {
                var meshData = new MeshPredictionSchemeData();
                var attCornerTable = meshDecoder.GetAttributeCornerTable(AttributeId);
                if (attCornerTable != null)
                    meshData.Set(attCornerTable,
                        encodingData.EncodedAttributeValueIndexToCornerMap,
                        encodingData.VertexToEncodedAttributeValueIndexMap);
                else
                    meshData.Set(cornerTable,
                        encodingData.EncodedAttributeValueIndexToCornerMap,
                        encodingData.VertexToEncodedAttributeValueIndexMap);
                return new MeshPredictionSchemeGeometricNormalDecoder(transform, meshData);
            }
        }

        // Fallback to delta decoder for non-mesh methods.
        return new PredictionSchemeDeltaDecoder(transform);
    }
}
