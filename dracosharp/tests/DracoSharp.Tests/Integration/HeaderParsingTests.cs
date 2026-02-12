using DracoSharp.Core;

namespace DracoSharp.Tests.Integration;

[TestClass]
public class HeaderParsingTests
{
    private static string TestDataPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "testdata"));

    [TestMethod]
    public void ParseHeader_SequentialDrc()
    {
        string path = Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc");
        if (!File.Exists(path))
            Assert.Inconclusive($"Test data not found: {path}");

        byte[] data = File.ReadAllBytes(path);
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        DracoHeader header = ParseHeader(buffer);

        Assert.AreEqual(2, header.VersionMajor);
        Assert.AreEqual(2, header.VersionMinor);
        Assert.AreEqual((byte)EncodedGeometryType.TriangularMesh, header.EncoderType);
        Assert.AreEqual((byte)MeshEncoderMethod.SequentialEncoding, header.EncoderMethod);
    }

    [TestMethod]
    public void ParseHeader_EdgebreakerDrc()
    {
        string path = Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc");
        if (!File.Exists(path))
            Assert.Inconclusive($"Test data not found: {path}");

        byte[] data = File.ReadAllBytes(path);
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        DracoHeader header = ParseHeader(buffer);

        Assert.AreEqual(2, header.VersionMajor);
        Assert.AreEqual(2, header.VersionMinor);
        Assert.AreEqual((byte)EncodedGeometryType.TriangularMesh, header.EncoderType);
        Assert.AreEqual((byte)MeshEncoderMethod.EdgebreakerEncoding, header.EncoderMethod);
    }

    [TestMethod]
    public void ParseHeader_PointCloudDrc()
    {
        string path = Path.Combine(TestDataPath, "cube_pc.drc");
        if (!File.Exists(path))
            Assert.Inconclusive($"Test data not found: {path}");

        byte[] data = File.ReadAllBytes(path);
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        DracoHeader header = ParseHeader(buffer);

        Assert.AreEqual((byte)EncodedGeometryType.PointCloud, header.EncoderType);
    }

    [TestMethod]
    public void ParseHeader_DracoMagicBytes()
    {
        string path = Path.Combine(TestDataPath, "cube_att.drc");
        if (!File.Exists(path))
            Assert.Inconclusive($"Test data not found: {path}");

        byte[] data = File.ReadAllBytes(path);

        // Verify DRACO magic is present
        Assert.AreEqual((byte)'D', data[0]);
        Assert.AreEqual((byte)'R', data[1]);
        Assert.AreEqual((byte)'A', data[2]);
        Assert.AreEqual((byte)'C', data[3]);
        Assert.AreEqual((byte)'O', data[4]);
    }

    [TestMethod]
    [DataRow("cube_att.drc")]
    [DataRow("car.drc")]
    [DataRow("bunny_gltf.drc")]
    public void ParseHeader_AllTestFiles_HaveValidMagic(string filename)
    {
        string path = Path.Combine(TestDataPath, filename);
        if (!File.Exists(path))
            Assert.Inconclusive($"Test data not found: {path}");

        byte[] data = File.ReadAllBytes(path);
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        DracoHeader header = ParseHeader(buffer);

        Assert.IsTrue(header.VersionMajor >= 1);
        Assert.IsTrue(header.EncoderType == (byte)EncodedGeometryType.TriangularMesh
                   || header.EncoderType == (byte)EncodedGeometryType.PointCloud);
    }

    private static DracoHeader ParseHeader(DecoderBuffer buffer)
    {
        // Read "DRACO" magic (5 bytes)
        Span<byte> magic = stackalloc byte[5];
        Assert.IsTrue(buffer.Decode(magic), "Failed to read magic bytes");
        Assert.AreEqual((byte)'D', magic[0]);
        Assert.AreEqual((byte)'R', magic[1]);
        Assert.AreEqual((byte)'A', magic[2]);
        Assert.AreEqual((byte)'C', magic[3]);
        Assert.AreEqual((byte)'O', magic[4]);

        var header = new DracoHeader();
        Assert.IsTrue(buffer.Decode(out byte major));
        header.VersionMajor = major;
        Assert.IsTrue(buffer.Decode(out byte minor));
        header.VersionMinor = minor;
        Assert.IsTrue(buffer.Decode(out byte encoderType));
        header.EncoderType = encoderType;
        Assert.IsTrue(buffer.Decode(out byte encoderMethod));
        header.EncoderMethod = encoderMethod;
        Assert.IsTrue(buffer.Decode(out ushort flags));
        header.Flags = flags;

        return header;
    }
}
