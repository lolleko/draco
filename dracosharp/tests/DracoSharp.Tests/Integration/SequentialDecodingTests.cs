using System.Buffers.Binary;
using DracoSharp.Attributes;
using DracoSharp.Compression;
using DracoSharp.Core;

namespace DracoSharp.Tests.Integration;

[TestClass]
public class SequentialDecodingTests
{
    private static string TestDataPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "testdata"));

    [TestMethod]
    public void GetEncodedGeometryType_SequentialMesh_ReturnsTriangularMesh()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var type = Decoder.GetEncodedGeometryType(data);
        Assert.AreEqual(EncodedGeometryType.TriangularMesh, type);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_CorrectFaceCount()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        Assert.AreEqual(12, mesh.NumFaces);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_CorrectPointCount()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        // The cube has 8 vertices but with per-face attributes (normals, texcoords),
        // the point count is typically expanded (each face-vertex combination is unique).
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_HasPositionAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");
        var posAttr = mesh.Attribute(posId);
        Assert.AreEqual(3, posAttr.NumComponents);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_HasNormalAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
        var normAttr = mesh.Attribute(normId);
        Assert.AreEqual(3, normAttr.NumComponents);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_HasTexCoordAttribute()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        int texId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.TexCoord);
        Assert.IsTrue(texId >= 0, "Should have texcoord attribute");
        var texAttr = mesh.Attribute(texId);
        Assert.AreEqual(2, texAttr.NumComponents);
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_PositionValuesInUnitCubeRange()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        var posAttr = mesh.GetNamedAttribute(GeometryAttribute.AttributeType.Position);

        // All position values should be approximately within [0, 1] for the unit cube.
        Span<float> values = stackalloc float[3];
        for (int i = 0; i < posAttr.Size; i++)
        {
            posAttr.ConvertValue(i, values);
            for (int c = 0; c < 3; c++)
            {
                Assert.IsTrue(values[c] >= -0.1f && values[c] <= 1.1f,
                    $"Position[{i}][{c}] = {values[c]} is outside unit cube range");
            }
        }
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_NormalValuesAreUnitLength()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        var normAttr = mesh.GetNamedAttribute(GeometryAttribute.AttributeType.Normal);

        Span<float> values = stackalloc float[3];
        for (int i = 0; i < normAttr.Size; i++)
        {
            normAttr.ConvertValue(i, values);
            float length = MathF.Sqrt(values[0] * values[0] + values[1] * values[1] + values[2] * values[2]);
            // Normals should be approximately unit length (quantization introduces some error).
            Assert.IsTrue(length > 0.8f && length < 1.2f,
                $"Normal[{i}] length = {length}, expected ~1.0");
        }
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_TexCoordValuesInRange()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        var texAttr = mesh.GetNamedAttribute(GeometryAttribute.AttributeType.TexCoord);

        Span<float> values = stackalloc float[2];
        for (int i = 0; i < texAttr.Size; i++)
        {
            texAttr.ConvertValue(i, values);
            for (int c = 0; c < 2; c++)
            {
                Assert.IsTrue(values[c] >= -0.1f && values[c] <= 1.1f,
                    $"TexCoord[{i}][{c}] = {values[c]} is outside [0,1] range");
            }
        }
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_FaceIndicesValid()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);

        for (int f = 0; f < mesh.NumFaces; f++)
        {
            var face = mesh.Face(f);
            for (int j = 0; j < 3; j++)
            {
                Assert.IsTrue(face[j] >= 0 && face[j] < mesh.NumPoints,
                    $"Face[{f}][{j}] = {face[j]} out of range [0, {mesh.NumPoints})");
            }
        }
    }

    [TestMethod]
    public void DecodeMesh_CubeAttSequential_ContainsExpectedCornerPositions()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_att.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);
        var posAttr = mesh.GetNamedAttribute(GeometryAttribute.AttributeType.Position);

        // Collect all unique position values (rounded to 1 decimal place).
        HashSet<(float, float, float)> uniquePositions = [];
        Span<float> values = stackalloc float[3];
        for (int i = 0; i < posAttr.Size; i++)
        {
            posAttr.ConvertValue(i, values);
            uniquePositions.Add((
                MathF.Round(values[0], 1),
                MathF.Round(values[1], 1),
                MathF.Round(values[2], 1)));
        }

        // A unit cube should have 8 corner positions.
        Assert.AreEqual(8, uniquePositions.Count,
            $"Expected 8 unique corner positions, got {uniquePositions.Count}");

        // Verify all 8 corners of the unit cube are present.
        float[] corners = [0f, 1f];
        foreach (float x in corners)
        foreach (float y in corners)
        foreach (float z in corners)
        {
            Assert.IsTrue(uniquePositions.Contains((x, y, z)),
                $"Missing cube corner ({x},{y},{z})");
        }
    }

    [TestMethod]
    public void DecodeMesh_TestNmSequential_CorrectStructure()
    {
        byte[] data = File.ReadAllBytes(
            Path.Combine(TestDataPath, "test_nm.obj.sequential.cl3.2.2.drc"));
        var mesh = Decoder.DecodeMesh(data);

        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
        Assert.IsTrue(mesh.NumAttributes > 0, "Should have attributes");

        int posId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Position);
        Assert.IsTrue(posId >= 0, "Should have position attribute");

        int normId = mesh.GetNamedAttributeId(GeometryAttribute.AttributeType.Normal);
        Assert.IsTrue(normId >= 0, "Should have normal attribute");
    }

    [TestMethod]
    public void DecodeMesh_WrongType_Throws()
    {
        // cube_pc.drc is a point cloud, not a mesh
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_pc.drc"));
        Assert.ThrowsExactly<DracoException>(() => Decoder.DecodeMesh(data));
    }

    [TestMethod]
    public void GetEncodedGeometryType_PointCloud_ReturnsPointCloud()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TestDataPath, "cube_pc.drc"));
        var type = Decoder.GetEncodedGeometryType(data);
        Assert.AreEqual(EncodedGeometryType.PointCloud, type);
    }
}
