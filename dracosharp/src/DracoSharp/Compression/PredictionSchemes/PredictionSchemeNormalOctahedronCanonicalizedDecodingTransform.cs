using DracoSharp.Core;

namespace DracoSharp.Compression.PredictionSchemes;

public class PredictionSchemeNormalOctahedronCanonicalizedDecodingTransform : PredictionSchemeDecodingTransform
{
    private readonly OctahedronToolBox _toolBox = new();

    public override PredictionSchemeTransformType TransformType =>
        PredictionSchemeTransformType.NormalOctahedronCanonicalized;

    public int QuantizationBits => _toolBox.QuantizationBits;

    public override bool AreCorrectionsPositive => true;

    public override void Init(int numComponents)
    {
        // No-op for octahedron transform — components are always 2.
        base.Init(numComponents);
    }

    public override bool DecodeTransformData(DecoderBuffer buffer)
    {
        if (!buffer.Decode(out int maxQuantizedValue))
            return false;
        if (!buffer.Decode(out int _)) // center_value (unused, derived from max)
            return false;

        if (maxQuantizedValue % 2 == 0)
            return false;
        if (!_toolBox.SetQuantizationBits(BitUtils.MostSignificantBit((uint)maxQuantizedValue) + 1))
            return false;
        if (_toolBox.QuantizationBits < 2 || _toolBox.QuantizationBits > 30)
            return false;
        return true;
    }

    public override void ComputeOriginalValue(ReadOnlySpan<int> predictedVals,
                                              ReadOnlySpan<int> corrVals,
                                              Span<int> outOriginalVals)
    {
        int centerValue = _toolBox.CenterValue;
        int t0 = predictedVals[0] - centerValue;
        int t1 = predictedVals[1] - centerValue;

        bool predIsInDiamond = _toolBox.IsInDiamond(t0, t1);
        if (!predIsInDiamond)
            _toolBox.InvertDiamond(ref t0, ref t1);

        bool predIsInBottomLeft = IsInBottomLeft(t0, t1);
        int rotationCount = GetRotationCount(t0, t1);
        if (!predIsInBottomLeft)
            RotatePoint(ref t0, ref t1, rotationCount);

        int origS = _toolBox.ModMax(AddAsUnsigned(t0, corrVals[0]));
        int origT = _toolBox.ModMax(AddAsUnsigned(t1, corrVals[1]));

        if (!predIsInBottomLeft)
        {
            int reverseRotation = (4 - rotationCount) % 4;
            RotatePoint(ref origS, ref origT, reverseRotation);
        }
        if (!predIsInDiamond)
            _toolBox.InvertDiamond(ref origS, ref origT);

        outOriginalVals[0] = origS + centerValue;
        outOriginalVals[1] = origT + centerValue;
    }

    private static bool IsInBottomLeft(int s, int t)
    {
        if (s == 0 && t == 0)
            return true;
        return s < 0 && t <= 0;
    }

    private static int GetRotationCount(int s, int t)
    {
        if (s == 0)
        {
            if (t == 0) return 0;
            return t > 0 ? 3 : 1;
        }
        if (s > 0)
            return t >= 0 ? 2 : 1;
        return t <= 0 ? 0 : 3;
    }

    private static void RotatePoint(ref int s, ref int t, int rotationCount)
    {
        switch (rotationCount)
        {
            case 1:
                (s, t) = (t, -s);
                break;
            case 2:
                (s, t) = (-s, -t);
                break;
            case 3:
                (s, t) = (-t, s);
                break;
        }
    }

    private static int AddAsUnsigned(int a, int b) =>
        (int)((uint)a + (uint)b);
}
