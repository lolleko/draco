using System.Buffers.Binary;
using DracoSharp.Core;

namespace DracoSharp.Tests.Core;

[TestClass]
public class DecoderBufferTests
{
    [TestMethod]
    public void Init_SetsUpBufferCorrectly()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([0x01, 0x02, 0x03]);

        Assert.AreEqual(3, buffer.RemainingSize);
        Assert.AreEqual(0, buffer.DecodedSize);
    }

    [TestMethod]
    public void DecodeByte_ReadsAndAdvances()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([0xAB, 0xCD]);

        Assert.IsTrue(buffer.Decode(out byte val1));
        Assert.AreEqual(0xAB, val1);
        Assert.AreEqual(1, buffer.DecodedSize);

        Assert.IsTrue(buffer.Decode(out byte val2));
        Assert.AreEqual(0xCD, val2);
        Assert.AreEqual(0, buffer.RemainingSize);
    }

    [TestMethod]
    public void DecodeByte_FailsWhenEmpty()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([]);

        Assert.IsFalse(buffer.Decode(out byte _));
    }

    [TestMethod]
    public void DecodeUInt16_LittleEndian()
    {
        byte[] data = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, 0x1234);

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out ushort val));
        Assert.AreEqual((ushort)0x1234, val);
    }

    [TestMethod]
    public void DecodeInt32_LittleEndian()
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(data, -42);

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out int val));
        Assert.AreEqual(-42, val);
    }

    [TestMethod]
    public void DecodeUInt32_LittleEndian()
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0xDEADBEEF);

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out uint val));
        Assert.AreEqual(0xDEADBEEF, val);
    }

    [TestMethod]
    public void DecodeFloat_LittleEndian()
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(data, 3.14f);

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out float val));
        Assert.AreEqual(3.14f, val, 1e-6f);
    }

    [TestMethod]
    public void DecodeDouble_LittleEndian()
    {
        byte[] data = new byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(data, 2.71828);

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out double val));
        Assert.AreEqual(2.71828, val, 1e-10);
    }

    [TestMethod]
    public void DecodeUInt64_LittleEndian()
    {
        byte[] data = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(data, 0xCAFEBABE12345678);

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out ulong val));
        Assert.AreEqual(0xCAFEBABE12345678UL, val);
    }

    [TestMethod]
    public void DecodeMultipleTypes_Sequential()
    {
        // Write: byte(0xFF), int32(-100), float(1.5f)
        byte[] data = new byte[1 + 4 + 4];
        data[0] = 0xFF;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), -100);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(5), 1.5f);

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out byte b));
        Assert.AreEqual(0xFF, b);

        Assert.IsTrue(buffer.Decode(out int i));
        Assert.AreEqual(-100, i);

        Assert.IsTrue(buffer.Decode(out float f));
        Assert.AreEqual(1.5f, f);

        Assert.AreEqual(0, buffer.RemainingSize);
    }

    [TestMethod]
    public void Peek_DoesNotAdvance()
    {
        byte[] data = [0x42, 0x00, 0x00, 0x00];
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Peek(out byte val));
        Assert.AreEqual(0x42, val);
        Assert.AreEqual(4, buffer.RemainingSize);

        // Peek again gives same result
        Assert.IsTrue(buffer.Peek(out byte val2));
        Assert.AreEqual(0x42, val2);
    }

    [TestMethod]
    public void PeekUInt32_DoesNotAdvance()
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 12345);
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Assert.IsTrue(buffer.Peek(out uint val));
        Assert.AreEqual(12345u, val);
        Assert.AreEqual(4, buffer.RemainingSize);
    }

    [TestMethod]
    public void Advance_MovesPosition()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([1, 2, 3, 4, 5]);

        buffer.Advance(3);
        Assert.AreEqual(2, buffer.RemainingSize);

        Assert.IsTrue(buffer.Decode(out byte val));
        Assert.AreEqual(4, val);
    }

    [TestMethod]
    public void StartDecodingFrom_ResetsPosition()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([10, 20, 30, 40]);

        buffer.Advance(3);
        buffer.StartDecodingFrom(1);
        Assert.AreEqual(3, buffer.RemainingSize);

        Assert.IsTrue(buffer.Decode(out byte val));
        Assert.AreEqual(20, val);
    }

    [TestMethod]
    public void DecodeSpan_ReadsRawBytes()
    {
        byte[] data = [0xAA, 0xBB, 0xCC, 0xDD];
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        Span<byte> output = stackalloc byte[3];
        Assert.IsTrue(buffer.Decode(output));
        Assert.AreEqual(0xAA, output[0]);
        Assert.AreEqual(0xBB, output[1]);
        Assert.AreEqual(0xCC, output[2]);
        Assert.AreEqual(1, buffer.RemainingSize);
    }

    [TestMethod]
    public void DecodeSpan_FailsWhenNotEnoughData()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([0x01]);

        Span<byte> output = stackalloc byte[5];
        Assert.IsFalse(buffer.Decode(output));
    }

    [TestMethod]
    public void BitDecoding_ReadsSingleBits()
    {
        // Byte 0b10110100 = 0xB4
        // LSB first: bits are 0,0,1,0,1,1,0,1
        var buffer = new DecoderBuffer();
        buffer.Init([0xB4]);
        buffer.BitstreamVersion = BitstreamVersion.Make(2, 2);

        Assert.IsTrue(buffer.StartBitDecoding(false, out _));
        Assert.IsTrue(buffer.BitDecoderActive);

        // Read 8 bits one at a time (LSB first)
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b0));
        Assert.AreEqual(0u, b0); // bit 0
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b1));
        Assert.AreEqual(0u, b1); // bit 1
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b2));
        Assert.AreEqual(1u, b2); // bit 2
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b3));
        Assert.AreEqual(0u, b3); // bit 3
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b4));
        Assert.AreEqual(1u, b4); // bit 4
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b5));
        Assert.AreEqual(1u, b5); // bit 5
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b6));
        Assert.AreEqual(0u, b6); // bit 6
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(1, out uint b7));
        Assert.AreEqual(1u, b7); // bit 7

        buffer.EndBitDecoding();
        Assert.IsFalse(buffer.BitDecoderActive);
    }

    [TestMethod]
    public void BitDecoding_ReadsMultipleBitsAtOnce()
    {
        // 0b10110100 = 0xB4, reading 4 bits at a time (LSB first)
        // Lower nibble: 0100 -> value 4
        // Upper nibble: 1011 -> value 11
        var buffer = new DecoderBuffer();
        buffer.Init([0xB4]);
        buffer.BitstreamVersion = BitstreamVersion.Make(2, 2);

        Assert.IsTrue(buffer.StartBitDecoding(false, out _));

        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(4, out uint lower));
        Assert.AreEqual(4u, lower);

        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(4, out uint upper));
        Assert.AreEqual(0xBu, upper);

        buffer.EndBitDecoding();
    }

    [TestMethod]
    public void BitDecoding_SpansMultipleBytes()
    {
        // Two bytes: 0xFF 0x00
        // 16 bits LSB first: 11111111 00000000
        var buffer = new DecoderBuffer();
        buffer.Init([0xFF, 0x00]);
        buffer.BitstreamVersion = BitstreamVersion.Make(2, 2);

        Assert.IsTrue(buffer.StartBitDecoding(false, out _));

        // Read 8 bits: should be 0xFF (all ones)
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(8, out uint first));
        Assert.AreEqual(0xFFu, first);

        // Read 8 bits: should be 0x00 (all zeros)
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(8, out uint second));
        Assert.AreEqual(0x00u, second);

        buffer.EndBitDecoding();
    }

    [TestMethod]
    public void BitDecoding_EndAdvancesPosition()
    {
        // 3 bytes: used for bit decoding, then 1 more byte after
        var buffer = new DecoderBuffer();
        buffer.Init([0xFF, 0xFF, 0xAA, 0x42]);
        buffer.BitstreamVersion = BitstreamVersion.Make(2, 2);

        Assert.IsTrue(buffer.StartBitDecoding(false, out _));
        // Read 20 bits -> spans 3 bytes (ceil(20/8) = 3)
        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(20, out _));
        buffer.EndBitDecoding();

        // Position should be advanced by 3 bytes
        Assert.AreEqual(1, buffer.RemainingSize);
        Assert.IsTrue(buffer.Decode(out byte remaining));
        Assert.AreEqual(0x42, remaining);
    }

    [TestMethod]
    public void Clone_ProducesIndependentCopy()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([1, 2, 3, 4]);
        buffer.BitstreamVersion = BitstreamVersion.Make(2, 2);
        buffer.Advance(2);

        var clone = buffer.Clone();
        Assert.AreEqual(buffer.RemainingSize, clone.RemainingSize);
        Assert.AreEqual(buffer.DecodedSize, clone.DecodedSize);
        Assert.AreEqual(buffer.BitstreamVersion, clone.BitstreamVersion);

        // Advancing clone doesn't affect original
        clone.Advance(1);
        Assert.AreEqual(2, buffer.DecodedSize);
        Assert.AreEqual(3, clone.DecodedSize);
    }

    [TestMethod]
    public void BitstreamVersion_Make_RoundTrips()
    {
        ushort version = BitstreamVersion.Make(2, 3);
        Assert.AreEqual(2, BitstreamVersion.Major(version));
        Assert.AreEqual(3, BitstreamVersion.Minor(version));
    }

    [TestMethod]
    public void BitstreamVersion_Constants_AreCorrect()
    {
        Assert.AreEqual(BitstreamVersion.Make(2, 3), BitstreamVersion.PointCloud);
        Assert.AreEqual(BitstreamVersion.Make(2, 2), BitstreamVersion.Mesh);
    }

    [TestMethod]
    public void DecodeVarint_SmallValue()
    {
        // Value 42 (< 128): single byte, no continuation bit
        var buffer = new DecoderBuffer();
        buffer.Init([42]);

        Assert.IsTrue(buffer.DecodeVarint(out uint val));
        Assert.AreEqual(42u, val);
    }

    [TestMethod]
    public void DecodeVarint_TwoByteValue()
    {
        // Varint encoding of 200:
        // Draco varint is MSB-first with continuation bit in bit 7
        // 200 = 0b11001000
        // Split into 7-bit chunks (MSB first): 0b1 (high), 0b1001000 (low)
        // Byte 1: 0b10000001 = 0x81 (continuation + high bits)
        // Byte 2: 0b01001000 = 0x48 (no continuation + low bits)
        // Decoding: read byte1 (0x81), continuation set, recurse
        //   read byte2 (0x48), no continuation -> outVal = 0x48
        //   outVal <<= 7 -> 0x48 << 7 = 0x2400
        //   outVal |= (0x81 & 0x7F) = 0x01
        //   outVal = 0x2401 = 9217... that's wrong
        // Actually, let me re-read the decoding logic:
        // The recursion decodes deeper bytes first, then shifts and ORs.
        // For value 200 = 0xC8:
        //   We need 200 in 7-bit big-endian: high=1, low=72 (0xC8 >> 7 = 1, 0xC8 & 0x7F = 0x48)
        //   Byte 1 (first read): 0x80 | 0x48 = 0xC8 (continuation + low 7 bits)
        //   Byte 2 (second read): 0x01 (no continuation, high bits)
        //   Decode: read byte1=0xC8, continuation, recurse
        //     read byte2=0x01, no continuation -> outVal = 1
        //   outVal <<= 7 -> 8... no wait, outVal = 1, <<= 7 -> 128
        //   outVal |= (0xC8 & 0x7F) -> 128 | 0x48 = 128 + 72 = 200. Correct!
        var buffer = new DecoderBuffer();
        buffer.Init([0xC8, 0x01]);

        Assert.IsTrue(buffer.DecodeVarint(out uint val));
        Assert.AreEqual(200u, val);
    }

    [TestMethod]
    public void DecodeVarint_UInt64_LargeValue()
    {
        // Varint for 300 = 0x12C
        // 300 >> 7 = 2, 300 & 0x7F = 0x2C
        // Byte 1: 0x80 | 0x2C = 0xAC
        // Byte 2: 0x02
        var buffer = new DecoderBuffer();
        buffer.Init([0xAC, 0x02]);

        Assert.IsTrue(buffer.DecodeVarint(out ulong val));
        Assert.AreEqual(300UL, val);
    }

    [TestMethod]
    public void Decode_FailsBeyondEnd_Int32()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([1, 2, 3]);

        Assert.IsFalse(buffer.Decode(out int _));
    }

    [TestMethod]
    public void Decode_FailsBeyondEnd_UInt16()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([1]);

        Assert.IsFalse(buffer.Decode(out ushort _));
    }

    [TestMethod]
    public void DataHead_ReturnsCorrectSlice()
    {
        var buffer = new DecoderBuffer();
        buffer.Init([10, 20, 30]);
        buffer.Advance(1);

        var head = buffer.DataHead;
        Assert.AreEqual(2, head.Length);
        Assert.AreEqual(20, head[0]);
        Assert.AreEqual(30, head[1]);
    }
}
