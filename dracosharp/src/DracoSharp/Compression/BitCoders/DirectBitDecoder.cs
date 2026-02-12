using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using DracoSharp.Core;

namespace DracoSharp.Compression.BitCoders;

/// <summary>
/// Direct bit decoder that reads bits from a pre-loaded uint32 word array.
/// Port of draco's DirectBitDecoder.
/// </summary>
public class DirectBitDecoder
{
    private uint[] _bits = [];
    private int _pos;
    private int _numUsedBits;

    public bool StartDecoding(DecoderBuffer sourceBuffer)
    {
        Clear();
        if (!sourceBuffer.Decode(out uint sizeInBytes))
            return false;

        if (sizeInBytes == 0 || (sizeInBytes & 0x3) != 0)
            return false;
        if (sizeInBytes > sourceBuffer.RemainingSize)
            return false;

        int num32BitElements = (int)(sizeInBytes / 4);
        _bits = new uint[num32BitElements];

        Span<byte> raw = stackalloc byte[4];
        for (int i = 0; i < num32BitElements; i++)
        {
            if (!sourceBuffer.Decode(raw))
                return false;
            _bits[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw);
        }

        _pos = 0;
        _numUsedBits = 0;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DecodeNextBit()
    {
        uint selector = 1u << (31 - _numUsedBits);
        if (_pos >= _bits.Length)
            return false;
        bool bit = (_bits[_pos] & selector) != 0;
        _numUsedBits++;
        if (_numUsedBits == 32)
        {
            _pos++;
            _numUsedBits = 0;
        }
        return bit;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DecodeLeastSignificantBits32(int nbits, out uint value)
    {
        value = 0;
        int remaining = 32 - _numUsedBits;
        if (nbits <= remaining)
        {
            if (_pos >= _bits.Length)
                return false;
            value = (_bits[_pos] << _numUsedBits) >> (32 - nbits);
            _numUsedBits += nbits;
            if (_numUsedBits == 32)
            {
                _pos++;
                _numUsedBits = 0;
            }
        }
        else
        {
            if (_pos + 1 >= _bits.Length)
                return false;
            uint valueL = _bits[_pos] << _numUsedBits;
            _numUsedBits = nbits - remaining;
            _pos++;
            uint valueR = _bits[_pos] >> (32 - _numUsedBits);
            value = (valueL >> (32 - _numUsedBits - remaining)) | valueR;
        }
        return true;
    }

    public void EndDecoding() { }

    private void Clear()
    {
        _bits = [];
        _numUsedBits = 0;
        _pos = 0;
    }
}
