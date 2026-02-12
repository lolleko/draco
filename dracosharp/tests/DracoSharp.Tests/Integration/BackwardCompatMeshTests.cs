using DracoSharp.Attributes;
using DracoSharp.Compression;

namespace DracoSharp.Tests.Integration;

[TestClass]
public class BackwardCompatMeshTests
{
    private static string TestDataPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "testdata"));

    // ---- v2.1 (bitstream 1.1.0) sequential ----

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V21_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.1.1.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V21_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.1.1.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v2.1 (bitstream 1.1.0) edgebreaker ----

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V21_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.1.1.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V21_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.1.1.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v2.0 (bitstream 1.0.0) sequential ----

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V20_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.1.0.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V20_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.1.0.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v2.0 (bitstream 1.0.0) edgebreaker ----

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V20_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.1.0.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V20_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.1.0.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v1.2 (bitstream 0.10.0) sequential ----

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V12_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.0.10.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V12_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.0.10.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v1.2 (bitstream 0.10.0) edgebreaker ----

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V12_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.0.10.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V12_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.0.10.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v1.2 test_nm_quant (bitstream 0.9.0) ----

    [TestMethod]
    public void DecodeMesh_TestNmQuant_V12_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm_quant.0.9.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNmQuant_V12_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm_quant.0.9.0.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v1.1 (bitstream 0.9.1) sequential ----

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V11_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.0.9.1.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Sequential_V11_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.sequential.0.9.1.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v1.1 (bitstream 0.9.1) edgebreaker ----

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V11_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.0.9.1.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNm_Edgebreaker_V11_HasPositionAndNormal()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.0.9.1.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    // ---- v1.1 point cloud ----

    [TestMethod]
    public void DecodePointCloud_CubePc_V11_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_pc.drc"));
        var pc = Decoder.DecodePointCloud(data);
        Assert.IsTrue(pc.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodePointCloud_CubePc_V11_HasPositionAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_pc.drc"));
        var pc = Decoder.DecodePointCloud(data);
        int posId = pc.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }
}
