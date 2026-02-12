using DracoSharp.Core;

namespace DracoSharp.Tests.Core;

[TestClass]
public class DataBufferTests
{
    [TestMethod]
    public void NewBuffer_IsEmpty()
    {
        var buffer = new DataBuffer();
        Assert.AreEqual(0, buffer.DataSize);
    }

    [TestMethod]
    public void Constructor_WithSize_AllocatesCorrectly()
    {
        var buffer = new DataBuffer(100);
        Assert.AreEqual(100, buffer.DataSize);
    }

    [TestMethod]
    public void Resize_ChangesSize()
    {
        var buffer = new DataBuffer();
        buffer.Resize(256);
        Assert.AreEqual(256, buffer.DataSize);
    }

    [TestMethod]
    public void Update_SetsData()
    {
        var buffer = new DataBuffer();
        byte[] data = [1, 2, 3, 4];
        buffer.Update(data);

        Assert.AreEqual(4, buffer.DataSize);
        Assert.AreEqual(1, buffer.Data[0]);
        Assert.AreEqual(4, buffer.Data[3]);
    }

    [TestMethod]
    public void Update_AtOffset_WritesCorrectly()
    {
        var buffer = new DataBuffer(10);
        byte[] data = [0xAA, 0xBB];
        buffer.Update(data, 5);

        Assert.AreEqual(0xAA, buffer.Data[5]);
        Assert.AreEqual(0xBB, buffer.Data[6]);
    }

    [TestMethod]
    public void WriteAndRead_RoundTrips()
    {
        var buffer = new DataBuffer(16);
        byte[] toWrite = [10, 20, 30, 40];
        buffer.Write(4, toWrite);

        Span<byte> readBack = stackalloc byte[4];
        buffer.Read(4, readBack);

        Assert.AreEqual(10, readBack[0]);
        Assert.AreEqual(20, readBack[1]);
        Assert.AreEqual(30, readBack[2]);
        Assert.AreEqual(40, readBack[3]);
    }

    [TestMethod]
    public void CopyFrom_CopiesCorrectly()
    {
        var src = new DataBuffer();
        src.Update([0, 0, 0xAA, 0xBB, 0xCC, 0, 0]);

        var dst = new DataBuffer(10);
        dst.CopyFrom(2, src, 2, 3);

        Assert.AreEqual(0xAA, dst.Data[2]);
        Assert.AreEqual(0xBB, dst.Data[3]);
        Assert.AreEqual(0xCC, dst.Data[4]);
    }

    [TestMethod]
    public void MutableData_AllowsModification()
    {
        var buffer = new DataBuffer(4);
        buffer.MutableData[0] = 0xFF;
        Assert.AreEqual(0xFF, buffer.Data[0]);
    }
}
