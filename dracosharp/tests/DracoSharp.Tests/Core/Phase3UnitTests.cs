using DracoSharp.Compression.PredictionSchemes;
using DracoSharp.Core;

namespace DracoSharp.Tests.Core;

[TestClass]
public class BitUtilsTests
{
    [TestMethod]
    [DataRow(0u, 0)]
    [DataRow(1u, -1)]
    [DataRow(2u, 1)]
    [DataRow(3u, -2)]
    [DataRow(4u, 2)]
    [DataRow(5u, -3)]
    [DataRow(100u, 50)]
    [DataRow(101u, -51)]
    public void ConvertSymbolToSignedInt_CorrectRoundTrip(uint symbol, int expected)
    {
        Assert.AreEqual(expected, BitUtils.ConvertSymbolToSignedInt(symbol));
    }

    [TestMethod]
    public void ConvertSymbolsToSignedInts_BatchConversion()
    {
        uint[] symbols = [0, 1, 2, 3, 4, 5];
        int[] expected = [0, -1, 1, -2, 2, -3];
        int[] output = new int[6];
        BitUtils.ConvertSymbolsToSignedInts(symbols, output);
        CollectionAssert.AreEqual(expected, output);
    }

    [TestMethod]
    [DataRow(1u, 0)]
    [DataRow(2u, 1)]
    [DataRow(3u, 1)]
    [DataRow(4u, 2)]
    [DataRow(0x80000000u, 31)]
    [DataRow(0xFFFFFFFFu, 31)]
    public void MostSignificantBit_ReturnsCorrectBit(uint n, int expected)
    {
        Assert.AreEqual(expected, BitUtils.MostSignificantBit(n));
    }

    [TestMethod]
    public void MostSignificantBit_Zero_ReturnsNegativeOne()
    {
        Assert.AreEqual(-1, BitUtils.MostSignificantBit(0));
    }
}

[TestClass]
public class DequantizerTests
{
    [TestMethod]
    public void Init_ValidParams_ReturnsTrue()
    {
        var dq = new Dequantizer();
        Assert.IsTrue(dq.Init(10f, 100));
    }

    [TestMethod]
    public void Init_ZeroRange_ReturnsFalse()
    {
        var dq = new Dequantizer();
        Assert.IsFalse(dq.Init(0f, 100));
    }

    [TestMethod]
    public void Init_ZeroMaxQuantized_ReturnsFalse()
    {
        var dq = new Dequantizer();
        Assert.IsFalse(dq.Init(10f, 0));
    }

    [TestMethod]
    public void DequantizeFloat_Zero_ReturnsZero()
    {
        var dq = new Dequantizer();
        dq.Init(10f, 100);
        Assert.AreEqual(0f, dq.DequantizeFloat(0));
    }

    [TestMethod]
    public void DequantizeFloat_MaxValue_ReturnsRange()
    {
        var dq = new Dequantizer();
        dq.Init(10f, 100);
        Assert.AreEqual(10f, dq.DequantizeFloat(100), 0.0001f);
    }

    [TestMethod]
    public void DequantizeFloat_MidValue_ReturnsHalfRange()
    {
        var dq = new Dequantizer();
        dq.Init(10f, 100);
        Assert.AreEqual(5f, dq.DequantizeFloat(50), 0.0001f);
    }
}

[TestClass]
public class PredictionSchemeDeltaDecoderTests
{
    [TestMethod]
    public void ComputeOriginalValues_DeltaDecoding_SingleComponent()
    {
        var transform = new PredictionSchemeDecodingTransform();
        var decoder = new PredictionSchemeDeltaDecoder(transform);

        // Corrections: [10, 5, 3, -2] -> Originals: [10, 15, 18, 16]
        int[] data = [10, 5, 3, -2];
        decoder.ComputeOriginalValues(data, 4, 1, [0, 1, 2, 3]);
        Assert.AreEqual(10, data[0]);
        Assert.AreEqual(15, data[1]);
        Assert.AreEqual(18, data[2]);
        Assert.AreEqual(16, data[3]);
    }

    [TestMethod]
    public void ComputeOriginalValues_DeltaDecoding_MultiComponent()
    {
        var transform = new PredictionSchemeDecodingTransform();
        var decoder = new PredictionSchemeDeltaDecoder(transform);

        // 2 components: corrections [(1,2), (3,4)] -> originals [(1,2), (4,6)]
        int[] data = [1, 2, 3, 4];
        decoder.ComputeOriginalValues(data, 4, 2, [0, 1]);
        Assert.AreEqual(1, data[0]);
        Assert.AreEqual(2, data[1]);
        Assert.AreEqual(4, data[2]);
        Assert.AreEqual(6, data[3]);
    }

    [TestMethod]
    public void PredictionMethod_IsDifference()
    {
        var transform = new PredictionSchemeDecodingTransform();
        var decoder = new PredictionSchemeDeltaDecoder(transform);
        Assert.AreEqual(PredictionSchemeMethod.Difference, decoder.PredictionMethod);
    }
}

[TestClass]
public class PredictionSchemeWrapTransformTests
{
    [TestMethod]
    public void ComputeOriginalValue_NoWrap()
    {
        var transform = new PredictionSchemeWrapDecodingTransform();
        // Simulate DecodeTransformData by using a DecoderBuffer with min=0, max=100
        var buffer = new DecoderBuffer();
        byte[] data = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0), 0);   // min
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 100); // max
        buffer.Init(data);
        Assert.IsTrue(transform.DecodeTransformData(buffer));

        transform.Init(1);
        int[] predicted = [50];
        int[] correction = [10];
        int[] output = new int[1];
        transform.ComputeOriginalValue(predicted, correction, output);
        Assert.AreEqual(60, output[0]);
    }

    [TestMethod]
    public void ComputeOriginalValue_WrapsAboveMax()
    {
        var transform = new PredictionSchemeWrapDecodingTransform();
        var buffer = new DecoderBuffer();
        byte[] data = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0), 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 100);
        buffer.Init(data);
        Assert.IsTrue(transform.DecodeTransformData(buffer));

        transform.Init(1);
        int[] predicted = [90];
        int[] correction = [20];
        int[] output = new int[1];
        transform.ComputeOriginalValue(predicted, correction, output);
        // 90 + 20 = 110 > 100, so wrap: 110 - 101 = 9
        Assert.AreEqual(9, output[0]);
    }

    [TestMethod]
    public void DecodeTransformData_MinGreaterThanMax_ReturnsFalse()
    {
        var transform = new PredictionSchemeWrapDecodingTransform();
        var buffer = new DecoderBuffer();
        byte[] data = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0), 100);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 0);
        buffer.Init(data);
        Assert.IsFalse(transform.DecodeTransformData(buffer));
    }
}
