// Copyright 2024 The Draco Authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Runtime.InteropServices;

namespace Draco.Decoder;

public class DecoderBuffer
{
    private byte[] data;
    private int position;
    private ushort bitstreamVersion;
    private BitDecoder bitDecoder;
    private bool bitMode;

    public DecoderBuffer()
    {
        data = Array.Empty<byte>();
        position = 0;
        bitstreamVersion = 0;
        bitDecoder = new BitDecoder();
        bitMode = false;
    }

    public void Init(byte[] inputData, ushort version = 0)
    {
        data = inputData ?? throw new ArgumentNullException(nameof(inputData));
        position = 0;
        bitstreamVersion = version;
        bitMode = false;
    }

    public int RemainingSize => data.Length - position;
    public int DecodedSize => position;
    public bool BitDecoderActive => bitMode;
    public ushort BitstreamVersion => bitstreamVersion;

    public void set_bitstreamVersion(ushort version) => bitstreamVersion = version;

    public bool Decode<T>(out T value) where T : unmanaged
    {
        value = default;
        int size = Marshal.SizeOf<T>();
        if (position + size > data.Length)
            return false;

        value = MemoryMarshal.Read<T>(data.AsSpan(position, size));
        position += size;
        return true;
    }

    public bool Decode(Span<byte> output)
    {
        if (position + output.Length > data.Length)
            return false;

        data.AsSpan(position, output.Length).CopyTo(output);
        position += output.Length;
        return true;
    }

    public bool Peek<T>(out T value) where T : unmanaged
    {
        value = default;
        int size = Marshal.SizeOf<T>();
        if (position + size > data.Length)
            return false;

        value = MemoryMarshal.Read<T>(data.AsSpan(position, size));
        return true;
    }

    public void Advance(int bytes) => position += bytes;

    public void StartDecodingFrom(int offset) => position = offset;

    public ReadOnlySpan<byte> DataHead() => data.AsSpan(position);

    public bool StartBitDecoding(bool decodeSize, out ulong outSize)
    {
        outSize = 0;
        if (bitMode)
            return false;

        if (decodeSize)
        {
            if (!Decode(out ulong size))
                return false;
            outSize = size;
        }

        bitDecoder.Reset(data, position, RemainingSize);
        bitMode = true;
        return true;
    }

    public void EndBitDecoding()
    {
        if (!bitMode)
            return;

        int bitsDecoded = (int)bitDecoder.BitsDecoded;
        int bytesDecoded = (bitsDecoded + 7) / 8;
        position += bytesDecoded;
        bitMode = false;
    }

    public bool DecodeLeastSignificantBits32(int numBits, out uint value)
    {
        value = 0;
        if (!bitMode)
            return false;
        return bitDecoder.GetBits(numBits, out value);
    }

    private class BitDecoder
    {
        private byte[] buffer;
        private int bufferOffset;
        private int bufferSize;
        private int bitOffset;

        public ulong BitsDecoded => (ulong)bitOffset;

        public void Reset(byte[] data, int offset, int size)
        {
            buffer = data;
            bufferOffset = offset;
            bufferSize = size;
            bitOffset = 0;
        }

        public bool GetBits(int numBits, out uint value)
        {
            value = 0;
            if (numBits > 32 || numBits < 0)
                return false;

            for (int bit = 0; bit < numBits; bit++)
            {
                if (!GetBit(out int bitValue))
                    return false;
                value |= (uint)(bitValue << bit);
            }
            return true;
        }

        private bool GetBit(out int bitValue)
        {
            bitValue = 0;
            int byteOffset = bitOffset / 8;
            int bitShift = bitOffset % 8;

            if (bufferOffset + byteOffset >= buffer.Length)
                return false;

            bitValue = (buffer[bufferOffset + byteOffset] >> bitShift) & 1;
            bitOffset++;
            return true;
        }
    }
}
