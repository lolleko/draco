using DracoSharp.Core;

namespace DracoSharp.Compression.Entropy;

/// <summary>
/// Entry point for decoding symbols from a DecoderBuffer.
/// Dispatches to tagged or raw symbol decoding based on the encoded scheme byte.
/// Port of draco's DecodeSymbols from symbol_decoding.cc.
/// </summary>
public static class SymbolDecoding
{
    public static bool DecodeSymbols(uint numValues, int numComponents,
                                     DecoderBuffer srcBuffer, Span<uint> outValues)
    {
        if (numValues == 0)
            return true;

        if (!srcBuffer.Decode(out byte scheme))
            return false;

        return scheme switch
        {
            (byte)SymbolCodingMethod.Tagged =>
                DecodeTaggedSymbols(numValues, numComponents, srcBuffer, outValues),
            (byte)SymbolCodingMethod.Raw =>
                DecodeRawSymbols(numValues, srcBuffer, outValues),
            _ => false
        };
    }

    private static bool DecodeTaggedSymbols(uint numValues, int numComponents,
                                            DecoderBuffer srcBuffer, Span<uint> outValues)
    {
        var tagDecoder = new RAnsSymbolDecoder(5);
        if (!tagDecoder.Create(srcBuffer))
            return false;

        if (!tagDecoder.StartDecoding(srcBuffer))
            return false;

        if (numValues > 0 && tagDecoder.NumSymbols == 0)
            return false;

        srcBuffer.StartBitDecoding(false, out _);
        int valueId = 0;
        for (uint i = 0; i < numValues; i += (uint)numComponents)
        {
            uint bitLength = tagDecoder.DecodeSymbol();
            for (int j = 0; j < numComponents; ++j)
            {
                if (!srcBuffer.DecodeLeastSignificantBits32((int)bitLength, out uint val))
                    return false;
                outValues[valueId++] = val;
            }
        }
        tagDecoder.EndDecoding();
        srcBuffer.EndBitDecoding();
        return true;
    }

    private static bool DecodeRawSymbols(uint numValues, DecoderBuffer srcBuffer,
                                         Span<uint> outValues)
    {
        if (!srcBuffer.Decode(out byte maxBitLength))
            return false;

        if (maxBitLength == 0 || maxBitLength > 18)
            return false;

        var decoder = new RAnsSymbolDecoder(maxBitLength);
        if (!decoder.Create(srcBuffer))
            return false;

        if (numValues > 0 && decoder.NumSymbols == 0)
            return false;

        if (!decoder.StartDecoding(srcBuffer))
            return false;

        for (uint i = 0; i < numValues; ++i)
            outValues[(int)i] = decoder.DecodeSymbol();

        decoder.EndDecoding();
        return true;
    }
}
