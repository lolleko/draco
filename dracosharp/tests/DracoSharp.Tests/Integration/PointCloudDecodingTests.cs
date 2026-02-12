using DracoSharp.Attributes;
using DracoSharp.Compression;
using DracoSharp.Core;

namespace DracoSharp.Tests.Integration;

[TestClass]
public class PointCloudDecodingTests
{
    private static string TestDataPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "testdata"));

    // ---- GetEncodedGeometryType ----

    [TestMethod]
    public void GetEncodedGeometryType_PcColor_ReturnsPointCloud()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "pc_color.drc"));
        var geometryType = Decoder.GetEncodedGeometryType(data);
        Assert.AreEqual(EncodedGeometryType.PointCloud, geometryType);
    }

    // ---- pc_color.drc (v2.2, point cloud, sequential) ----

    [TestMethod]
    public void DecodePointCloud_PcColor_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "pc_color.drc"));
        var pc = Decoder.DecodePointCloud(data);
        Assert.IsTrue(pc.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodePointCloud_PcColor_HasPositionAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "pc_color.drc"));
        var pc = Decoder.DecodePointCloud(data);
        int posId = pc.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }

    [TestMethod]
    public void DecodePointCloud_PcColor_HasColorAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "pc_color.drc"));
        var pc = Decoder.DecodePointCloud(data);
        int colorId = pc.GetNamedAttributeId(GeometryAttribute.AttributeType.Color);
        Assert.IsTrue(colorId >= 0, "Should have color attribute");
    }

    // ---- point_cloud_no_qp.drc (v2.3, point cloud, sequential) ----

    [TestMethod]
    public void DecodePointCloud_PointCloudNoQp_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "point_cloud_no_qp.drc"));
        var pc = Decoder.DecodePointCloud(data);
        Assert.IsTrue(pc.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodePointCloud_PointCloudNoQp_HasPositionAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "point_cloud_no_qp.drc"));
        var pc = Decoder.DecodePointCloud(data);
        int posId = pc.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }

    // ---- pc_kd_color.drc (v2.3, point cloud, KdTree — deferred) ----

    [TestMethod]
    [Ignore("KdTree point cloud decoder not yet implemented")]
    public void DecodePointCloud_PcKdColor_KdTreeEncoding()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "pc_kd_color.drc"));
        var pc = Decoder.DecodePointCloud(data);
        Assert.IsTrue(pc.NumPoints > 0, "Should have points");
    }
}
