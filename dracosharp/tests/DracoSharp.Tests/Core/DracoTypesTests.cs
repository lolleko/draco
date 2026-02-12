using DracoSharp.Core;

namespace DracoSharp.Tests.Core;

[TestClass]
public class DracoTypesTests
{
    [TestMethod]
    [DataRow(DataType.Int8, 1)]
    [DataRow(DataType.UInt8, 1)]
    [DataRow(DataType.Bool, 1)]
    [DataRow(DataType.Int16, 2)]
    [DataRow(DataType.UInt16, 2)]
    [DataRow(DataType.Int32, 4)]
    [DataRow(DataType.UInt32, 4)]
    [DataRow(DataType.Float32, 4)]
    [DataRow(DataType.Int64, 8)]
    [DataRow(DataType.UInt64, 8)]
    [DataRow(DataType.Float64, 8)]
    [DataRow(DataType.Invalid, -1)]
    public void DataType_ByteLength_IsCorrect(DataType dt, int expected)
    {
        Assert.AreEqual(expected, dt.ByteLength());
    }

    [TestMethod]
    [DataRow(DataType.Int8, true)]
    [DataRow(DataType.UInt8, true)]
    [DataRow(DataType.Int16, true)]
    [DataRow(DataType.UInt16, true)]
    [DataRow(DataType.Int32, true)]
    [DataRow(DataType.UInt32, true)]
    [DataRow(DataType.Int64, true)]
    [DataRow(DataType.UInt64, true)]
    [DataRow(DataType.Bool, true)]
    [DataRow(DataType.Float32, false)]
    [DataRow(DataType.Float64, false)]
    [DataRow(DataType.Invalid, false)]
    public void DataType_IsIntegral_IsCorrect(DataType dt, bool expected)
    {
        Assert.AreEqual(expected, dt.IsIntegral());
    }

    [TestMethod]
    public void DracoHeader_MetadataFlag()
    {
        var header = new DracoHeader { Flags = 0x8000 };
        Assert.IsTrue(header.HasMetadata);

        header.Flags = 0x0000;
        Assert.IsFalse(header.HasMetadata);

        header.Flags = 0x8001;
        Assert.IsTrue(header.HasMetadata);
    }
}
