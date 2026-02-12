using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace DracoSharp.Compression.Entropy;

/// <summary>
/// rANS (range Asymmetric Numeral System) decoder.
/// Port of draco's RAnsDecoder template class from ans.h.
/// The precision parameter replaces the C++ template parameter.
/// </summary>
public class RAnsDecoder
{
    private const int IoBase = 256;

    private readonly int _ransPrecision;
    private readonly int _lRansBase;

    private byte[] _buf = [];
    private int _bufOffset;
    private uint _state;

    private uint[] _lutTable = [];
    private RansSym[] _probabilityTable = [];

    public RAnsDecoder(int ransPrecisionBits)
    {
        _ransPrecision = 1 << ransPrecisionBits;
        _lRansBase = _ransPrecision * 4;
    }

    public int ReadInit(ReadOnlySpan<byte> buf, int offset)
    {
        if (offset < 1)
            return 1;

        _buf = buf.ToArray();
        uint x = (uint)(buf[offset - 1] >> 6);

        if (x == 0)
        {
            _bufOffset = offset - 1;
            _state = (uint)(buf[offset - 1] & 0x3F);
        }
        else if (x == 1)
        {
            if (offset < 2)
                return 1;
            _bufOffset = offset - 2;
            _state = MemGetLe16(buf, offset - 2) & 0x3FFF;
        }
        else if (x == 2)
        {
            if (offset < 3)
                return 1;
            _bufOffset = offset - 3;
            _state = MemGetLe24(buf, offset - 3) & 0x3FFFFF;
        }
        else if (x == 3)
        {
            if (offset < 4)
                return 1;
            _bufOffset = offset - 4;
            _state = MemGetLe32(buf, offset - 4) & 0x3FFFFFFF;
        }
        else
        {
            return 1;
        }

        _state += (uint)_lRansBase;
        if (_state >= (uint)(_lRansBase * IoBase))
            return 1;

        return 0;
    }

    public int ReadEnd() => _state == (uint)_lRansBase ? 1 : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint RansRead()
    {
        while (_state < (uint)_lRansBase && _bufOffset > 0)
        {
            _state = _state * IoBase + _buf[--_bufOffset];
        }

        uint quo = _state / (uint)_ransPrecision;
        uint rem = _state % (uint)_ransPrecision;

        uint symbol = _lutTable[rem];
        uint prob = _probabilityTable[symbol].Prob;
        uint cumProb = _probabilityTable[symbol].CumProb;

        _state = quo * prob + rem - cumProb;
        return symbol;
    }

    public bool RansBuildLookUpTable(uint[] tokenProbs, uint numSymbols)
    {
        _lutTable = new uint[_ransPrecision];
        _probabilityTable = new RansSym[numSymbols];

        uint cumProb = 0;
        uint actProb = 0;
        for (uint i = 0; i < numSymbols; ++i)
        {
            _probabilityTable[i] = new RansSym { Prob = tokenProbs[i], CumProb = cumProb };
            cumProb += tokenProbs[i];
            if (cumProb > (uint)_ransPrecision)
                return false;
            for (uint j = actProb; j < cumProb; ++j)
                _lutTable[j] = i;
            actProb = cumProb;
        }

        return cumProb == (uint)_ransPrecision;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MemGetLe16(ReadOnlySpan<byte> mem, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(mem.Slice(offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MemGetLe24(ReadOnlySpan<byte> mem, int offset) =>
        (uint)(mem[offset] | (mem[offset + 1] << 8) | (mem[offset + 2] << 16));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MemGetLe32(ReadOnlySpan<byte> mem, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(mem.Slice(offset));
}

public struct RansSym
{
    public uint Prob;
    public uint CumProb;
}

/// <summary>
/// Low-level ANS decoder for binary (one-bit) symbols with a fixed probability.
/// Port of AnsDecoder + rabs_desc_read from ans.h.
/// </summary>
public class AnsDecoder
{
    private const uint P8Precision = 256;
    private const uint LBase = 4096;
    private const int IoBase = 256;

    private byte[] _buf = [];
    private int _bufOffset;
    private uint _state;

    public int ReadInit(ReadOnlySpan<byte> buf, int offset)
    {
        if (offset < 1)
            return 1;

        _buf = buf.ToArray();
        uint x = (uint)(buf[offset - 1] >> 6);

        if (x == 0)
        {
            _bufOffset = offset - 1;
            _state = (uint)(buf[offset - 1] & 0x3F);
        }
        else if (x == 1)
        {
            if (offset < 2)
                return 1;
            _bufOffset = offset - 2;
            _state = (uint)(buf[offset - 2] | (buf[offset - 1] << 8)) & 0x3FFF;
        }
        else if (x == 2)
        {
            if (offset < 3)
                return 1;
            _bufOffset = offset - 3;
            _state = (uint)(buf[offset - 3] | (buf[offset - 2] << 8) | (buf[offset - 1] << 16)) & 0x3FFFFF;
        }
        else
        {
            return 1;
        }

        _state += LBase;
        if (_state >= LBase * IoBase)
            return 1;

        return 0;
    }

    public bool ReadEnd() => _state == LBase;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RabsDescRead(byte p0)
    {
        uint p = P8Precision - p0;

        if (_state < LBase && _bufOffset > 0)
        {
            _state = _state * (uint)IoBase + _buf[--_bufOffset];
        }

        uint x = _state;
        uint quot = x / P8Precision;
        uint rem = x % P8Precision;
        uint xn = quot * p;
        bool val = rem < p;

        _state = val ? xn + rem : x - xn - p;
        return val ? 1 : 0;
    }
}
