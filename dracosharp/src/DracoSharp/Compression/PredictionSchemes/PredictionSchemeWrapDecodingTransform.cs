using DracoSharp.Core;

namespace DracoSharp.Compression.PredictionSchemes;

public class PredictionSchemeWrapDecodingTransform : PredictionSchemeDecodingTransform
{
    private int _minValue;
    private int _maxValue;
    private int _maxDif;
    private int[] _clampedValue = [];

    public override PredictionSchemeTransformType TransformType => PredictionSchemeTransformType.Wrap;

    public override void Init(int numComponents)
    {
        base.Init(numComponents);
        _clampedValue = new int[numComponents];
    }

    public override void ComputeOriginalValue(ReadOnlySpan<int> predictedVals,
                                              ReadOnlySpan<int> corrVals,
                                              Span<int> outOriginalVals)
    {
        ClampPredictedValue(predictedVals);

        for (int i = 0; i < NumComponents; i++)
        {
            outOriginalVals[i] = (int)((uint)_clampedValue[i] + (uint)corrVals[i]);
            if (outOriginalVals[i] > _maxValue)
                outOriginalVals[i] -= _maxDif;
            else if (outOriginalVals[i] < _minValue)
                outOriginalVals[i] += _maxDif;
        }
    }

    public override bool DecodeTransformData(DecoderBuffer buffer)
    {
        if (!buffer.Decode(out int minValue))
            return false;
        if (!buffer.Decode(out int maxValue))
            return false;
        if (minValue > maxValue)
            return false;
        _minValue = minValue;
        _maxValue = maxValue;
        return InitCorrectionBounds();
    }

    private bool InitCorrectionBounds()
    {
        long dif = (long)_maxValue - (long)_minValue;
        if (dif < 0 || dif >= int.MaxValue)
            return false;
        _maxDif = 1 + (int)dif;
        return true;
    }

    private void ClampPredictedValue(ReadOnlySpan<int> predictedVal)
    {
        for (int i = 0; i < NumComponents; i++)
        {
            if (predictedVal[i] > _maxValue)
                _clampedValue[i] = _maxValue;
            else if (predictedVal[i] < _minValue)
                _clampedValue[i] = _minValue;
            else
                _clampedValue[i] = predictedVal[i];
        }
    }
}
