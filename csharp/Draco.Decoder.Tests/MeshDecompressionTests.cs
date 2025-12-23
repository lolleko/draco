using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Draco.Decoder.Tests;

[TestClass]
public class MeshDecompressionTests
{
    [TestMethod]
    public void DecodeMesh_CubeAtt()
    {
        string testFile = "../../../../../testdata/cube_att.drc";
        if (!File.Exists(testFile))
        {
            Assert.Inconclusive($"Test file not found: {testFile}");
            return;
        }

        byte[] data = File.ReadAllBytes(testFile);
        Console.WriteLine($"File size: {data.Length} bytes");

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var decoder = new DracoDecoder();
        var result = decoder.DecodeMeshFromBuffer(buffer);

        Console.WriteLine($"Decode status: {result.Status}");
        Console.WriteLine($"Buffer position after decode: {buffer.DecodedSize}/{buffer.RemainingSize + buffer.DecodedSize}");

        Assert.IsTrue(result.Ok, "Decoding should succeed");

        var mesh = result.Value;
        Assert.IsNotNull(mesh);
        Assert.IsTrue(mesh.NumPoints > 0, "Should have points");
        Assert.IsTrue(mesh.NumFaces > 0, "Should have faces");
        Assert.IsTrue(mesh.NumAttributes > 0, "Should have attributes");

        Console.WriteLine($"Mesh has {mesh.NumPoints} points, {mesh.NumFaces} faces, {mesh.NumAttributes} attributes");

        // Get position attribute
        var posAttr = mesh.GetNamedAttribute(GeometryAttributeType.Position);
        Assert.IsNotNull(posAttr, "Should have position attribute");
        Assert.AreEqual(3, posAttr.NumComponents, "Position should have 3 components");

        // Get indices
        var indices = mesh.GetIndices();
        Assert.IsNotNull(indices);
        Assert.AreEqual(mesh.NumFaces * 3, indices.Length, "Should have 3 indices per face");

        Console.WriteLine($"Position attribute: {posAttr.NumComponents} components, {posAttr.Data.Length} bytes");
        Console.WriteLine($"Indices: {indices.Length} total ({mesh.NumFaces} faces)");
    }
}
