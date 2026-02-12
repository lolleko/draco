using DracoSharp.Compression.Entropy;
using DracoSharp.Core;

namespace DracoSharp.Compression.BitCoders;

/// <summary>
/// rANS-based bit decoder with a fixed probability of zero.
/// Port of draco's RAnsBitDecoder.
/// </summary>
public class RAnsBitDecoder
{
    private readonly AnsDecoder _ansDecoder = new();
    private byte _probZero;

    public bool StartDecoding(DecoderBuffer sourceBuffer)
    {
        if (!sourceBuffer.Decode(out _probZero))
            return false;

        uint sizeInBytes;
        if (sourceBuffer.BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
        {
            if (!sourceBuffer.Decode(out sizeInBytes))
                return false;
        }
        else
        {
            if (!sourceBuffer.DecodeVarint(out sizeInBytes))
                return false;
        }

        if (sizeInBytes > sourceBuffer.RemainingSize)
            return false;

        ReadOnlySpan<byte> dataHead = sourceBuffer.DataHead;
        int result = _ansDecoder.ReadInit(dataHead, (int)sizeInBytes);
        sourceBuffer.Advance(sizeInBytes);

        return result == 0;
    }

    public bool DecodeNextBit() => _ansDecoder.RabsDescRead(_probZero) > 0;

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
}
