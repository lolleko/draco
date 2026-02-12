using DracoSharp.Compression.Entropy;
using DracoSharp.Core;

namespace DracoSharp.Compression.BitCoders;

/// <summary>
/// Adaptive rANS bit decoder that adjusts its probability estimate after each decoded bit.
/// Port of draco's AdaptiveRAnsBitDecoder.
/// </summary>
public class AdaptiveRAnsBitDecoder
{
    private readonly AnsDecoder _ansDecoder = new();
    private double _p0F = 0.5;

    public bool StartDecoding(DecoderBuffer sourceBuffer)
    {
        Clear();

        if (!sourceBuffer.Decode(out uint sizeInBytes))
            return false;
        if (sizeInBytes > sourceBuffer.RemainingSize)
            return false;

        ReadOnlySpan<byte> dataHead = sourceBuffer.DataHead;
        int result = _ansDecoder.ReadInit(dataHead, (int)sizeInBytes);
        sourceBuffer.Advance(sizeInBytes);

        return result == 0;
    }

    public bool DecodeNextBit()
    {
        byte p0 = ClampProbability(_p0F);
        bool bit = _ansDecoder.RabsDescRead(p0) > 0;
        _p0F = UpdateProbability(_p0F, bit);
        return bit;
    }

    public void DecodeLeastSignificantBits32(int nbits, out uint value)
    {
        value = 0;
        int remaining = nbits;
        while (remaining > 0)
        {
            value = (value << 1) + (uint)(DecodeNextBit() ? 1 : 0);
            remaining--;
        }
    }

    public void EndDecoding() { }

    private void Clear()
    {
        _p0F = 0.5;
    }

    private static byte ClampProbability(double p)
    {
        uint pInt = (uint)(p * 256.0 + 0.5);
        if (pInt == 256) pInt--;
        if (pInt == 0) pInt++;
        return (byte)pInt;
    }

    private static double UpdateProbability(double oldP, bool bit)
    {
        const double w0 = 127.0 / 128.0;
        const double w1 = 1.0 / 128.0;
        return oldP * w0 + (bit ? 0.0 : w1);
    }
}
