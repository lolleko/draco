using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.PredictionSchemes;

public class PredictionSchemeDecodingTransform
{
    private int _numComponents;

    public virtual PredictionSchemeTransformType TransformType => PredictionSchemeTransformType.Delta;

    public virtual void Init(int numComponents) => _numComponents = numComponents;

    public virtual void ComputeOriginalValue(ReadOnlySpan<int> predictedVals,
                                             ReadOnlySpan<int> corrVals,
                                             Span<int> outOriginalVals)
    {
        for (int i = 0; i < _numComponents; i++)
            outOriginalVals[i] = predictedVals[i] + corrVals[i];
    }

    public virtual bool DecodeTransformData(DecoderBuffer buffer) => true;

    public virtual bool AreCorrectionsPositive => false;

    protected int NumComponents => _numComponents;
}
