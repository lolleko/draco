using DracoSharp.Core;

namespace DracoSharp.Compression.Entropy;

/// <summary>
/// Decodes symbols using the rANS algorithm with a probability table.
/// Port of draco's RAnsSymbolDecoder template class.
/// The unique_symbols_bit_length template parameter becomes a constructor argument.
/// </summary>
public class RAnsSymbolDecoder
{
    private readonly int _ransPrecisionBits;
    private uint _numSymbols;
    private uint[] _probabilityTable = [];
    private readonly RAnsDecoder _ans;

    public RAnsSymbolDecoder(int uniqueSymbolsBitLength)
    {
        _ransPrecisionBits = ComputeRAnsPrecisionFromUniqueSymbolsBitLength(uniqueSymbolsBitLength);
        _ans = new RAnsDecoder(_ransPrecisionBits);
    }

    public uint NumSymbols => _numSymbols;

    public bool Create(DecoderBuffer buffer)
    {
        if (buffer.BitstreamVersion == 0)
            return false;

        if (buffer.BitstreamVersion < BitstreamVersion.Make(2, 0))
        {
            if (!buffer.Decode(out _numSymbols))
                return false;
        }
        else
        {
            if (!buffer.DecodeVarint(out _numSymbols))
                return false;
        }

        if (_numSymbols / 64 > (uint)buffer.RemainingSize)
            return false;

        _probabilityTable = new uint[_numSymbols];
        if (_numSymbols == 0)
            return true;

        for (uint i = 0; i < _numSymbols; ++i)
        {
            if (!buffer.Decode(out byte probData))
                return false;

            int token = probData & 3;
            if (token == 3)
            {
                uint offset = (uint)(probData >> 2);
                if (i + offset >= _numSymbols)
                    return false;
                for (uint j = 0; j < offset + 1; ++j)
                    _probabilityTable[i + j] = 0;
                i += offset;
            }
            else
            {
                int extraBytes = token;
                uint prob = (uint)(probData >> 2);
                for (int b = 0; b < extraBytes; ++b)
                {
                    if (!buffer.Decode(out byte eb))
                        return false;
                    prob |= (uint)eb << (8 * (b + 1) - 2);
                }
                _probabilityTable[i] = prob;
            }
        }

        return _ans.RansBuildLookUpTable(_probabilityTable, _numSymbols);
    }

    public bool StartDecoding(DecoderBuffer buffer)
    {
        ulong bytesEncoded;
        if (buffer.BitstreamVersion < BitstreamVersion.Make(2, 0))
        {
            if (!buffer.Decode(out bytesEncoded))
                return false;
        }
        else
        {
            if (!buffer.DecodeVarint(out bytesEncoded))
                return false;
        }

        if (bytesEncoded > (ulong)buffer.RemainingSize)
            return false;

        ReadOnlySpan<byte> dataHead = buffer.DataHead;
        buffer.Advance((long)bytesEncoded);

        return _ans.ReadInit(dataHead, (int)bytesEncoded) == 0;
    }

    public uint DecodeSymbol() => _ans.RansRead();

    public void EndDecoding() => _ans.ReadEnd();

    private static int ComputeRAnsPrecisionFromUniqueSymbolsBitLength(int symbolsBitLength)
    {
        int unclamped = (3 * symbolsBitLength) / 2;
        return Math.Clamp(unclamped, 12, 20);
    }
}
