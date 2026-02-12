using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.PredictionSchemes;

public interface IPredictionSchemeDecoder
{
    PredictionSchemeMethod PredictionMethod { get; }
    PredictionSchemeTransformType TransformType { get; }
    bool IsInitialized { get; }
    bool DecodePredictionData(DecoderBuffer buffer);
    bool ComputeOriginalValues(Span<int> correctedValues, int size, int numComponents, int[] entryToPointIdMap);
    int NumParentAttributes { get; }
    GeometryAttribute.AttributeType GetParentAttributeType(int i);
    bool SetParentAttribute(PointAttribute att);
    bool AreCorrectionsPositive { get; }
}
