using System.Runtime.InteropServices;
using DracoSharp.Attributes;
using DracoSharp.Compression;
using DracoSharp.Core;

namespace DracoSharp.Tests.Integration;

[TestClass]
public class EdgebreakerDecodingTests
{
    private static string TestDataPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "testdata"));

    // ---- cube_att edgebreaker cl10 (valence) ----

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL10_CorrectFaceCount()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.AreEqual(12, mesh.NumFaces);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL10_HasPositionAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        var posAttr = mesh.Attribute(posId);
        Assert.AreEqual(3, posAttr.NumComponents);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL10_HasNormalAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int nrmId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(nrmId >= 0, "Should have normal attribute");
        var normAttr = mesh.Attribute(nrmId);
        Assert.AreEqual(3, normAttr.NumComponents);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL10_HasTexCoordAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int tcId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.TexCoord);
        Assert.IsTrue(tcId >= 0, "Should have texcoord attribute");
        var tcAttr = mesh.Attribute(tcId);
        Assert.AreEqual(2, tcAttr.NumComponents);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL10_PositionValuesReasonable()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        var posAttr = mesh.Attribute(posId);
        byte[] buf = new byte[posAttr.EntryByteSize];
        for (int i = 0; i < posAttr.Size; i++)
        {
            posAttr.GetValue(i, buf);
            var vals = MemoryMarshal.Cast<byte, float>(buf);
            for (int c = 0; c < 3; c++)
            {
                Assert.IsTrue(vals[c] >= -2.0f && vals[c] <= 2.0f,
                    $"Position component[{c}] = {vals[c]} out of expected range for cube");
            }
        }
    }

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL10_NormalsUnitLength()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int nrmId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        var normAttr = mesh.Attribute(nrmId);
        byte[] buf = new byte[normAttr.EntryByteSize];
        for (int i = 0; i < normAttr.Size; i++)
        {
            normAttr.GetValue(i, buf);
            var vals = MemoryMarshal.Cast<byte, float>(buf);
            float length = MathF.Sqrt(vals[0] * vals[0] + vals[1] * vals[1] + vals[2] * vals[2]);
            Assert.IsTrue(MathF.Abs(length - 1.0f) < 0.1f,
                $"Normal[{i}] length = {length}, expected ~1.0");
        }
    }

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL10_FaceIndicesValid()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        for (int f = 0; f < mesh.NumFaces; f++)
        {
            var face = mesh.Face(f);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(face[i] >= 0 && face[i] < mesh.NumPoints,
                    $"Face[{f}][{i}] = {face[i]} out of range [0, {mesh.NumPoints})");
            }
        }
    }

    // ---- cube_att edgebreaker cl4 ----

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL4_CorrectFaceCount()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl4.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.AreEqual(12, mesh.NumFaces);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttEdgebreaker_CL4_HasAttributes()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.edgebreaker.cl4.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position) >= 0);
        Assert.IsTrue(mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal) >= 0);
        Assert.IsTrue(mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.TexCoord) >= 0);
    }

    // ---- cube_att default (v1.1 edgebreaker) ----

    [TestMethod]
    public void DecodeMesh_CubeAttDefault_CorrectFaceCount()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.AreEqual(12, mesh.NumFaces);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttDefault_HasPositionAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }

    // ---- test_nm edgebreaker cl10 ----

    [TestMethod]
    public void DecodeMesh_TestNmEdgebreaker_CL10_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_TestNmEdgebreaker_CL10_HasNormals()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.cl10.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int nrmId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(nrmId >= 0, "Should have normal attribute");
    }

    // ---- test_nm edgebreaker cl4 ----

    [TestMethod]
    public void DecodeMesh_TestNmEdgebreaker_CL4_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "test_nm.obj.edgebreaker.cl4.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
    }

    // ---- car.drc (edgebreaker by default) ----

    [TestMethod]
    public void DecodeMesh_Car_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "car.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 100, "Car mesh should have many faces");
        Assert.IsTrue(mesh.NumPoints > 100, "Car mesh should have many points");
    }

    [TestMethod]
    public void DecodeMesh_Car_HasPosition()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "car.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
    }

    // ---- bunny_gltf.drc (edgebreaker by default, typically large mesh) ----

    [TestMethod]
    public void DecodeMesh_BunnyGltf_DecodesSuccessfully()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "bunny_gltf.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.IsTrue(mesh.NumFaces > 0, "Bunny mesh should have faces");
    }
}
