using DracoSharp.Attributes;
using DracoSharp.Compression;
using DracoSharp.Core;

namespace DracoSharp.Tests.Integration;

[TestClass]
public class AdditionalMeshTests
{
    private static string TestDataPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "testdata"));

    // ---- cube_att_sub_o_2.drc (v2.2 edgebreaker, metadata flag) ----

    [TestMethod]
    public void DecodeMesh_CubeAttSubO2_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att_sub_o_2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSubO2_HasPosition()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att_sub_o_2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }

    // ---- cube_att_sub_o_no_metadata.drc (v2.2 edgebreaker, no metadata) ----

    [TestMethod]
    public void DecodeMesh_CubeAttSubONoMetadata_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att_sub_o_no_metadata.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSubONoMetadata_HasPosition()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att_sub_o_no_metadata.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }

    // ---- octagon_preserved.drc (v2.2 edgebreaker, metadata flag) ----

    [TestMethod]
    public void DecodeMesh_OctagonPreserved_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "octagon_preserved.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_OctagonPreserved_HasPosition()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "octagon_preserved.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }
}
