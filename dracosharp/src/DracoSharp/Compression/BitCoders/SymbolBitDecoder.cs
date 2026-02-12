using DracoSharp.Compression.Entropy;
using DracoSharp.Core;

namespace DracoSharp.Compression.BitCoders;

/// <summary>
/// Bit decoder that uses symbol entropy encoding (DecodeSymbols) to decode bits.
/// Port of draco's SymbolBitDecoder.
/// </summary>
public class SymbolBitDecoder
{
    private readonly List<uint> _symbols = [];

    public bool StartDecoding(DecoderBuffer sourceBuffer)
    {
        if (!sourceBuffer.Decode(out uint size))
            return false;

        uint[] symbols = new uint[size];
        if (!SymbolDecoding.DecodeSymbols(size, 1, sourceBuffer, symbols))
            return false;

        _symbols.Clear();
        _symbols.AddRange(symbols);
        _symbols.Reverse();
        return true;
    }

    public bool DecodeNextBit()
    {
        DecodeLeastSignificantBits32(1, out uint symbol);
        return symbol == 1;
    }

    public void DecodeLeastSignificantBits32(int nbits, out uint value)
    {
        value = _symbols[^1];
        _symbols.RemoveAt(_symbols.Count - 1);
        int discardedBits = 32 - nbits;
        value <<= discardedBits;
        value >>= discardedBits;
    }

    public void EndDecoding() => _symbols.Clear();
}
