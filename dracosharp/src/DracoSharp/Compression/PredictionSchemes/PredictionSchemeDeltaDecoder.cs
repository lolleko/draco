using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.PredictionSchemes;

public class PredictionSchemeDeltaDecoder : IPredictionSchemeDecoder
{
    private readonly PredictionSchemeDecodingTransform _transform;

    public PredictionSchemeDeltaDecoder(PredictionSchemeDecodingTransform transform)
    {
        _transform = transform;
    }

    public PredictionSchemeMethod PredictionMethod => PredictionSchemeMethod.Difference;
    public PredictionSchemeTransformType TransformType => _transform.TransformType;
    public bool IsInitialized => true;
    public int NumParentAttributes => 0;
    public bool AreCorrectionsPositive => _transform.AreCorrectionsPositive;

    public GeometryAttribute.AttributeType GetParentAttributeType(int i) =>
        GeometryAttribute.AttributeType.Invalid;

    public bool SetParentAttribute(PointAttribute att) => false;

    public bool DecodePredictionData(DecoderBuffer buffer) =>
        _transform.DecodeTransformData(buffer);

    public bool ComputeOriginalValues(Span<int> data, int size, int numComponents, int[] entryToPointIdMap)
    {
        _transform.Init(numComponents);

        // Decode the original value for the first element.
        Span<int> zeroVals = stackalloc int[numComponents];
        zeroVals.Clear();
        _transform.ComputeOriginalValue(zeroVals, data[..numComponents], data);

        // Decode data from the front: D(i) = D(i) + D(i - 1)
        for (int i = numComponents; i < size; i += numComponents)
        {
            _transform.ComputeOriginalValue(
                data.Slice(i - numComponents, numComponents),
                data.Slice(i, numComponents),
                data.Slice(i, numComponents));
        }
        return true;
    }
}
