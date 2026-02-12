namespace DracoSharp.Core;

public struct Dequantizer
{
    private float _delta;

    public bool Init(float range, int maxQuantizedValue)
    {
        if (maxQuantizedValue <= 0 || range <= 0f)
            return false;
        _delta = range / maxQuantizedValue;
        return true;
    }

    public float DequantizeFloat(int val) => val * _delta;
}
