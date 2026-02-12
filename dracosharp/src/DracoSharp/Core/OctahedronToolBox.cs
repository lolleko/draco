namespace DracoSharp.Core;

public class OctahedronToolBox
{
    private int _quantizationBits = -1;
    private int _maxQuantizedValue = -1;
    private int _maxValue = -1;
    private float _dequantizationScale = 1f;
    private int _centerValue = -1;

    public int QuantizationBits => _quantizationBits;
    public int MaxQuantizedValue => _maxQuantizedValue;
    public int MaxValue => _maxValue;
    public int CenterValue => _centerValue;
    public bool IsInitialized => _quantizationBits != -1;

    public bool SetQuantizationBits(int q)
    {
        if (q < 2 || q > 30)
            return false;
        _quantizationBits = q;
        _maxQuantizedValue = (1 << q) - 1;
        _maxValue = _maxQuantizedValue - 1;
        _dequantizationScale = 2f / _maxValue;
        _centerValue = _maxValue / 2;
        return true;
    }

    public void QuantizedOctahedralCoordsToUnitVector(int inS, int inT, Span<float> outVector)
    {
        OctahedralCoordsToUnitVector(
            inS * _dequantizationScale - 1f,
            inT * _dequantizationScale - 1f,
            outVector);
    }

    public bool IsInDiamond(int s, int t)
    {
        uint st = (uint)Math.Abs(s) + (uint)Math.Abs(t);
        return st <= (uint)_centerValue;
    }

    public void InvertDiamond(ref int s, ref int t)
    {
        int signS, signT;
        if (s >= 0 && t >= 0)
        {
            signS = 1;
            signT = 1;
        }
        else if (s <= 0 && t <= 0)
        {
            signS = -1;
            signT = -1;
        }
        else
        {
            signS = s > 0 ? 1 : -1;
            signT = t > 0 ? 1 : -1;
        }

        // Use unsigned arithmetic to match C++ and avoid signed overflow.
        uint cornerPointS = (uint)(signS * _centerValue);
        uint cornerPointT = (uint)(signT * _centerValue);
        uint us = (uint)s;
        uint ut = (uint)t;
        us = us + us - cornerPointS;
        ut = ut + ut - cornerPointT;
        if (signS * signT >= 0)
        {
            uint temp = us;
            us = 0u - ut;
            ut = 0u - temp;
        }
        else
        {
            (us, ut) = (ut, us);
        }
        us += cornerPointS;
        ut += cornerPointT;

        s = (int)us / 2;
        t = (int)ut / 2;
    }

    public int ModMax(int x)
    {
        if (x > _centerValue)
            return x - _maxQuantizedValue;
        if (x < -_centerValue)
            return x + _maxQuantizedValue;
        return x;
    }

    public int MakePositive(int x)
    {
        if (x < 0)
            return x + _maxQuantizedValue;
        return x;
    }

    public void CanonicalizeIntegerVector(Span<int> vec)
    {
        long absSum = (long)Math.Abs(vec[0]) + Math.Abs(vec[1]) + Math.Abs(vec[2]);
        if (absSum == 0)
        {
            vec[0] = _centerValue;
        }
        else
        {
            vec[0] = (int)((long)vec[0] * _centerValue / absSum);
            vec[1] = (int)((long)vec[1] * _centerValue / absSum);
            if (vec[2] >= 0)
                vec[2] = _centerValue - Math.Abs(vec[0]) - Math.Abs(vec[1]);
            else
                vec[2] = -(_centerValue - Math.Abs(vec[0]) - Math.Abs(vec[1]));
        }
    }

    public void IntegerVectorToQuantizedOctahedralCoords(
        ReadOnlySpan<int> intVec, out int outS, out int outT)
    {
        int s, t;
        if (intVec[0] >= 0)
        {
            s = intVec[1] + _centerValue;
            t = intVec[2] + _centerValue;
        }
        else
        {
            if (intVec[1] < 0)
                s = Math.Abs(intVec[2]);
            else
                s = _maxValue - Math.Abs(intVec[2]);

            if (intVec[2] < 0)
                t = Math.Abs(intVec[1]);
            else
                t = _maxValue - Math.Abs(intVec[1]);
        }
        CanonicalizeOctahedralCoords(s, t, out outS, out outT);
    }

    private void CanonicalizeOctahedralCoords(int s, int t, out int outS, out int outT)
    {
        if ((s == 0 && t == 0) || (s == 0 && t == _maxValue) ||
            (s == _maxValue && t == 0))
        {
            s = _maxValue;
            t = _maxValue;
        }
        else if (s == 0 && t > _centerValue)
        {
            t = _centerValue - (t - _centerValue);
        }
        else if (s == _maxValue && t < _centerValue)
        {
            t = _centerValue + (_centerValue - t);
        }
        else if (t == _maxValue && s < _centerValue)
        {
            s = _centerValue + (_centerValue - s);
        }
        else if (t == 0 && s > _centerValue)
        {
            s = _centerValue - (s - _centerValue);
        }
        outS = s;
        outT = t;
    }

    private static void OctahedralCoordsToUnitVector(float inSScaled, float inTScaled, Span<float> outVector)
    {
        float y = inSScaled;
        float z = inTScaled;
        float x = 1f - MathF.Abs(y) - MathF.Abs(z);

        float xOffset = -x;
        xOffset = xOffset < 0 ? 0 : xOffset;

        y += y < 0 ? xOffset : -xOffset;
        z += z < 0 ? xOffset : -xOffset;

        float normSquared = x * x + y * y + z * z;
        if (normSquared < 1e-6f)
        {
            outVector[0] = 0;
            outVector[1] = 0;
            outVector[2] = 0;
        }
        else
        {
            float d = 1.0f / MathF.Sqrt(normSquared);
            outVector[0] = x * d;
            outVector[1] = y * d;
            outVector[2] = z * d;
        }
    }
}
