using System.Buffers.Binary;
using DracoSharp.Compression.BitCoders;

namespace DracoSharp.Tests.Compression.BitCoders;

[TestClass]
public class DirectBitDecoderTests
{
    [TestMethod]
    public void DecodesNextBitCorrectly_MSBFirst()
    {
        // DirectBitDecoder stores uint32 words and reads bits MSB-first.
        // Word = 0xA5000000 = 1010_0101_0000...
        // Bits MSB-first: 1,0,1,0,0,1,0,1,...
        byte[] data = new byte[4 + 4]; // 4 bytes size + 4 bytes data
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0), 4); // size=4 bytes
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0xA5000000);

        var buffer = new DracoSharp.Core.DecoderBuffer();
        buffer.Init(data);
        buffer.BitstreamVersion = DracoSharp.Core.BitstreamVersion.Mesh;

        var decoder = new DirectBitDecoder();
        Assert.IsTrue(decoder.StartDecoding(buffer));

        Assert.IsTrue(decoder.DecodeNextBit());   // 1
        Assert.IsFalse(decoder.DecodeNextBit());  // 0
        Assert.IsTrue(decoder.DecodeNextBit());   // 1
        Assert.IsFalse(decoder.DecodeNextBit());  // 0
        Assert.IsFalse(decoder.DecodeNextBit());  // 0
        Assert.IsTrue(decoder.DecodeNextBit());   // 1
        Assert.IsFalse(decoder.DecodeNextBit());  // 0
        Assert.IsTrue(decoder.DecodeNextBit());   // 1
    }

    [TestMethod]
    public void DecodeLeastSignificantBits32_ReadsMultipleBits()
    {
        // Word = 0xFF000000 = 11111111_00000000_...
        byte[] data = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0xFF000000);

        var buffer = new DracoSharp.Core.DecoderBuffer();
        buffer.Init(data);
        buffer.BitstreamVersion = DracoSharp.Core.BitstreamVersion.Mesh;

        var decoder = new DirectBitDecoder();
        Assert.IsTrue(decoder.StartDecoding(buffer));

        Assert.IsTrue(decoder.DecodeLeastSignificantBits32(8, out uint val));
        Assert.AreEqual(0xFFu, val);

        Assert.IsTrue(decoder.DecodeLeastSignificantBits32(8, out uint val2));
        Assert.AreEqual(0u, val2);
    }

    [TestMethod]
    public void DecodeLeastSignificantBits32_SpanningWordBoundary()
    {
        // Two words: 0x0000000F 0xF0000000
        // Bits MSB-first: word0 = 0000_0000_0000_0000_0000_0000_0000_1111
        //                 word1 = 1111_0000_...
        // Reading 32 bits gives all of word 0: 0x0000000F
        // Reading 8 more bits gives top 8 bits of word 1: 0xF0
        byte[] data = new byte[4 + 8];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0x0000000F);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 0xF0000000);

        var buffer = new DracoSharp.Core.DecoderBuffer();
        buffer.Init(data);
        buffer.BitstreamVersion = DracoSharp.Core.BitstreamVersion.Mesh;

        var decoder = new DirectBitDecoder();
        Assert.IsTrue(decoder.StartDecoding(buffer));

        // Read 28 zeros then 4 ones (spanning boundary)
        Assert.IsTrue(decoder.DecodeLeastSignificantBits32(28, out uint firstPart));
        Assert.AreEqual(0u, firstPart);

        // Now 4 remaining bits from word 0 + 4 from word 1
        Assert.IsTrue(decoder.DecodeLeastSignificantBits32(8, out uint middle));
        Assert.AreEqual(0xFFu, middle); // 1111_1111
    }

    [TestMethod]
    public void StartDecoding_RejectsInvalidSize()
    {
        // Size=3 (not a multiple of 4)
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 3);

        var buffer = new DracoSharp.Core.DecoderBuffer();
        buffer.Init(data);

        var decoder = new DirectBitDecoder();
        Assert.IsFalse(decoder.StartDecoding(buffer));
    }

    [TestMethod]
    public void StartDecoding_RejectsZeroSize()
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0);

        var buffer = new DracoSharp.Core.DecoderBuffer();
        buffer.Init(data);

        var decoder = new DirectBitDecoder();
        Assert.IsFalse(decoder.StartDecoding(buffer));
    }
}
