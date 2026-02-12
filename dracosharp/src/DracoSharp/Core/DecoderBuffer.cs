using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace DracoSharp.Core;

public class DecoderBuffer
{
    private byte[] _data = [];
    private long _dataSize;
    private long _pos;
    private readonly BitDecoder _bitDecoder = new();
    private bool _bitMode;
    private ushort _bitstreamVersion;

    public void Init(ReadOnlySpan<byte> data)
    {
        Init(data, _bitstreamVersion);
    }

    public void Init(ReadOnlySpan<byte> data, ushort version)
    {
        _data = data.ToArray();
        _dataSize = data.Length;
        _bitstreamVersion = version;
        _pos = 0;
    }

    public bool StartBitDecoding(bool decodeSize, out ulong size)
    {
        size = 0;
        if (decodeSize)
        {
            if (_bitstreamVersion < Core.BitstreamVersion.Make(2, 2))
            {
                if (!Decode(out size))
                    return false;
            }
            else
            {
                if (!DecodeVarint(out size))
                    return false;
            }
        }

        _bitMode = true;
        _bitDecoder.Reset(_data.AsSpan((int)_pos, (int)(_dataSize - _pos)));
        return true;
    }

    public void EndBitDecoding()
    {
        _bitMode = false;
        ulong bitsDecoded = _bitDecoder.BitsDecoded;
        ulong bytesDecoded = (bitsDecoded + 7) / 8;
        _pos += (long)bytesDecoded;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DecodeLeastSignificantBits32(int nbits, out uint value)
    {
        value = 0;
        if (!_bitMode)
            return false;
        return _bitDecoder.GetBits(nbits, out value);
    }

    public bool Decode(out byte value)
    {
        value = 0;
        if (_dataSize < _pos + 1)
            return false;
        value = _data[_pos];
        _pos += 1;
        return true;
    }

    public bool Decode(out sbyte value)
    {
        value = 0;
        if (_dataSize < _pos + 1)
            return false;
        value = (sbyte)_data[_pos];
        _pos += 1;
        return true;
    }

    public bool Decode(out ushort value)
    {
        value = 0;
        if (_dataSize < _pos + 2)
            return false;
        value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan((int)_pos));
        _pos += 2;
        return true;
    }

    public bool Decode(out short value)
    {
        value = 0;
        if (_dataSize < _pos + 2)
            return false;
        value = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan((int)_pos));
        _pos += 2;
        return true;
    }

    public bool Decode(out uint value)
    {
        value = 0;
        if (_dataSize < _pos + 4)
            return false;
        value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan((int)_pos));
        _pos += 4;
        return true;
    }

    public bool Decode(out int value)
    {
        value = 0;
        if (_dataSize < _pos + 4)
            return false;
        value = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan((int)_pos));
        _pos += 4;
        return true;
    }

    public bool Decode(out ulong value)
    {
        value = 0;
        if (_dataSize < _pos + 8)
            return false;
        value = BinaryPrimitives.ReadUInt64LittleEndian(_data.AsSpan((int)_pos));
        _pos += 8;
        return true;
    }

    public bool Decode(out long value)
    {
        value = 0;
        if (_dataSize < _pos + 8)
            return false;
        value = BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan((int)_pos));
        _pos += 8;
        return true;
    }

    public bool Decode(out float value)
    {
        value = 0;
        if (_dataSize < _pos + 4)
            return false;
        value = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan((int)_pos));
        _pos += 4;
        return true;
    }

    public bool Decode(out double value)
    {
        value = 0;
        if (_dataSize < _pos + 8)
            return false;
        value = BinaryPrimitives.ReadDoubleLittleEndian(_data.AsSpan((int)_pos));
        _pos += 8;
        return true;
    }

    public bool Decode(Span<byte> output)
    {
        if (_dataSize < _pos + output.Length)
            return false;
        _data.AsSpan((int)_pos, output.Length).CopyTo(output);
        _pos += output.Length;
        return true;
    }

    public bool Peek(out byte value)
    {
        value = 0;
        if (_dataSize < _pos + 1)
            return false;
        value = _data[_pos];
        return true;
    }

    public bool Peek(out uint value)
    {
        value = 0;
        if (_dataSize < _pos + 4)
            return false;
        value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan((int)_pos));
        return true;
    }

    public void Advance(long bytes) => _pos += bytes;

    public void StartDecodingFrom(long offset) => _pos = offset;

    public ushort BitstreamVersion
    {
        get => _bitstreamVersion;
        set => _bitstreamVersion = value;
    }

    public ReadOnlySpan<byte> DataHead => _data.AsSpan((int)_pos);
    public long RemainingSize => _dataSize - _pos;
    public long DecodedSize => _pos;
    public bool BitDecoderActive => _bitMode;

    public DecoderBuffer Clone()
    {
        var clone = new DecoderBuffer();
        clone._data = _data;
        clone._dataSize = _dataSize;
        clone._pos = _pos;
        clone._bitMode = _bitMode;
        clone._bitstreamVersion = _bitstreamVersion;
        return clone;
    }

    public bool DecodeVarint(out uint value)
    {
        value = 0;
        return DecodeVarintUnsigned(1, ref value);
    }

    public bool DecodeVarint(out ulong value)
    {
        value = 0;
        return DecodeVarintUnsigned(1, ref value);
    }

    private bool DecodeVarintUnsigned(int depth, ref uint outVal)
    {
        const int maxDepth = sizeof(uint) + 1 + (sizeof(uint) >> 3);
        if (depth > maxDepth)
            return false;

        if (!Decode(out byte b))
            return false;

        if ((b & (1 << 7)) != 0)
        {
            if (!DecodeVarintUnsigned(depth + 1, ref outVal))
                return false;
            outVal <<= 7;
            outVal |= (uint)(b & 0x7F);
        }
        else
        {
            outVal = b;
        }
        return true;
    }

    private bool DecodeVarintUnsigned(int depth, ref ulong outVal)
    {
        const int maxDepth = sizeof(ulong) + 1 + (sizeof(ulong) >> 3);
        if (depth > maxDepth)
            return false;

        if (!Decode(out byte b))
            return false;

        if ((b & (1 << 7)) != 0)
        {
            if (!DecodeVarintUnsigned(depth + 1, ref outVal))
                return false;
            outVal <<= 7;
            outVal |= (uint)(b & 0x7F);
        }
        else
        {
            outVal = b;
        }
        return true;
    }

    private sealed class BitDecoder
    {
        private byte[] _bitBuffer = [];
        private int _bitBufferLength;
        private long _bitOffset;

        public void Reset(ReadOnlySpan<byte> data)
        {
            _bitOffset = 0;
            _bitBuffer = data.ToArray();
            _bitBufferLength = data.Length;
        }

        public ulong BitsDecoded => (ulong)_bitOffset;

        public ulong AvailBits => (ulong)(_bitBufferLength * 8L - _bitOffset);

        public bool GetBits(int nbits, out uint value)
        {
            value = 0;
            if (nbits > 32)
                return false;
            for (int bit = 0; bit < nbits; bit++)
            {
                value |= (uint)GetBit() << bit;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBit()
        {
            long off = _bitOffset;
            long byteOffset = off >> 3;
            int bitShift = (int)(off & 0x7);
            if (byteOffset < _bitBufferLength)
            {
                int bit = (_bitBuffer[byteOffset] >> bitShift) & 1;
                _bitOffset = off + 1;
                return bit;
            }
            return 0;
        }
    }
}
