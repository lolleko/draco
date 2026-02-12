using System.Buffers.Binary;
using DracoSharp.Attributes;
using DracoSharp.Core;
using static DracoSharp.Attributes.GeometryAttribute;

namespace DracoSharp.Tests.Core;

[TestClass]
public class PointCloudAndMeshTests
{
    [TestMethod]
    public void PointCloud_AddAttribute_TracksCorrectly()
    {
        var pc = new PointCloud.PointCloud();
        var att = new PointAttribute();
        att.Init(AttributeType.Position, 3, DataType.Float32, false, 10);
        att.UniqueId = 0;

        int id = pc.AddAttribute(att);
        Assert.AreEqual(0, id);
        Assert.AreEqual(1, pc.NumAttributes);
    }

    [TestMethod]
    public void PointCloud_NamedAttribute_Lookup()
    {
        var pc = new PointCloud.PointCloud();

        var pos = new PointAttribute();
        pos.Init(AttributeType.Position, 3, DataType.Float32, false, 4);
        pos.UniqueId = 0;

        var norm = new PointAttribute();
        norm.Init(AttributeType.Normal, 3, DataType.Float32, false, 4);
        norm.UniqueId = 1;

        var color = new PointAttribute();
        color.Init(AttributeType.Color, 4, DataType.UInt8, true, 4);
        color.UniqueId = 2;

        pc.AddAttribute(pos);
        pc.AddAttribute(norm);
        pc.AddAttribute(color);

        Assert.AreEqual(3, pc.NumAttributes);
        Assert.AreEqual(1, pc.NumNamedAttributes(AttributeType.Position));
        Assert.AreEqual(1, pc.NumNamedAttributes(AttributeType.Normal));
        Assert.AreEqual(1, pc.NumNamedAttributes(AttributeType.Color));
        Assert.AreEqual(0, pc.NumNamedAttributes(AttributeType.TexCoord));

        Assert.AreEqual(0, pc.GetNamedAttributeId(AttributeType.Position));
        Assert.AreEqual(1, pc.GetNamedAttributeId(AttributeType.Normal));
        Assert.AreEqual(2, pc.GetNamedAttributeId(AttributeType.Color));
        Assert.AreEqual(-1, pc.GetNamedAttributeId(AttributeType.TexCoord));

        var foundPos = pc.GetNamedAttribute(AttributeType.Position);
        Assert.AreEqual(3, foundPos.NumComponents);
        Assert.AreEqual(DataType.Float32, foundPos.DataType);
    }

    [TestMethod]
    public void PointCloud_GetAttributeByUniqueId()
    {
        var pc = new PointCloud.PointCloud();
        var att = new PointAttribute();
        att.Init(AttributeType.Generic, 2, DataType.Float32, false, 5);
        att.UniqueId = 42;
        pc.AddAttribute(att);

        var found = pc.GetAttributeByUniqueId(42);
        Assert.AreEqual(42u, found.UniqueId);

        var notFound = pc.GetAttributeByUniqueId(999);
        Assert.IsTrue(notFound == null! || notFound is null);
    }

    [TestMethod]
    public void PointCloud_NumPoints_GetSet()
    {
        var pc = new PointCloud.PointCloud();
        pc.NumPoints = 100;
        Assert.AreEqual(100, pc.NumPoints);
    }

    [TestMethod]
    public void Mesh_AddFaces_TracksCorrectly()
    {
        var mesh = new Mesh.Mesh();
        mesh.AddFace(0, 1, 2);
        mesh.AddFace(2, 1, 3);

        Assert.AreEqual(2, mesh.NumFaces);

        var face0 = mesh.Face(0);
        Assert.AreEqual(0, face0[0]);
        Assert.AreEqual(1, face0[1]);
        Assert.AreEqual(2, face0[2]);

        var face1 = mesh.Face(1);
        Assert.AreEqual(2, face1[0]);
        Assert.AreEqual(1, face1[1]);
        Assert.AreEqual(3, face1[2]);
    }

    [TestMethod]
    public void Mesh_SetFace_ExpandsAndSets()
    {
        var mesh = new Mesh.Mesh();
        mesh.SetFace(2, [10, 11, 12]);

        Assert.AreEqual(3, mesh.NumFaces);
        var face = mesh.Face(2);
        Assert.AreEqual(10, face[0]);
        Assert.AreEqual(11, face[1]);
        Assert.AreEqual(12, face[2]);
    }

    [TestMethod]
    public void Mesh_SetNumFaces_ExpandsAndShrinks()
    {
        var mesh = new Mesh.Mesh();
        mesh.SetNumFaces(5);
        Assert.AreEqual(5, mesh.NumFaces);

        mesh.SetNumFaces(2);
        Assert.AreEqual(2, mesh.NumFaces);
    }

    [TestMethod]
    public void Mesh_CornerToPointId()
    {
        var mesh = new Mesh.Mesh();
        mesh.AddFace(10, 20, 30);
        mesh.AddFace(40, 50, 60);

        // Face 0: corners 0,1,2 -> points 10,20,30
        Assert.AreEqual(10, mesh.CornerToPointId(0));
        Assert.AreEqual(20, mesh.CornerToPointId(1));
        Assert.AreEqual(30, mesh.CornerToPointId(2));

        // Face 1: corners 3,4,5 -> points 40,50,60
        Assert.AreEqual(40, mesh.CornerToPointId(3));
        Assert.AreEqual(50, mesh.CornerToPointId(4));
        Assert.AreEqual(60, mesh.CornerToPointId(5));

        // Out of bounds
        Assert.AreEqual(-1, mesh.CornerToPointId(-1));
        Assert.AreEqual(-1, mesh.CornerToPointId(6));
    }

    [TestMethod]
    public void Mesh_InheritsPointCloudFunctionality()
    {
        var mesh = new Mesh.Mesh();
        mesh.NumPoints = 8;

        var att = new PointAttribute();
        att.Init(AttributeType.Position, 3, DataType.Float32, false, 8);
        att.UniqueId = 0;
        mesh.AddAttribute(att);

        mesh.AddFace(0, 1, 2);

        Assert.AreEqual(8, mesh.NumPoints);
        Assert.AreEqual(1, mesh.NumAttributes);
        Assert.AreEqual(1, mesh.NumFaces);
    }

    [TestMethod]
    public void PointAttribute_IdentityMapping()
    {
        var att = new PointAttribute();
        att.Init(AttributeType.Position, 3, DataType.Float32, false, 4);

        Assert.IsTrue(att.IsMappingIdentity);
        Assert.AreEqual(0, att.IndicesMapSize);
        Assert.AreEqual(2, att.MappedIndex(2));
    }

    [TestMethod]
    public void PointAttribute_ExplicitMapping()
    {
        var att = new PointAttribute();
        att.Init(AttributeType.Position, 3, DataType.Float32, false, 3);
        att.SetExplicitMapping(5);

        Assert.IsFalse(att.IsMappingIdentity);
        Assert.AreEqual(5, att.IndicesMapSize);

        att.SetPointMapEntry(0, 2);
        att.SetPointMapEntry(1, 0);
        att.SetPointMapEntry(2, 1);

        Assert.AreEqual(2, att.MappedIndex(0));
        Assert.AreEqual(0, att.MappedIndex(1));
        Assert.AreEqual(1, att.MappedIndex(2));
    }

    [TestMethod]
    public void PointAttribute_WriteAndReadValues_RoundTrips()
    {
        var att = new PointAttribute();
        att.Init(AttributeType.Position, 3, DataType.Float32, false, 2);

        // Write vertex 0: (1.0, 2.0, 3.0)
        byte[] vertex0 = new byte[12];
        BinaryPrimitives.WriteSingleLittleEndian(vertex0.AsSpan(0), 1.0f);
        BinaryPrimitives.WriteSingleLittleEndian(vertex0.AsSpan(4), 2.0f);
        BinaryPrimitives.WriteSingleLittleEndian(vertex0.AsSpan(8), 3.0f);
        att.SetAttributeValue(0, vertex0);

        // Write vertex 1: (4.0, 5.0, 6.0)
        byte[] vertex1 = new byte[12];
        BinaryPrimitives.WriteSingleLittleEndian(vertex1.AsSpan(0), 4.0f);
        BinaryPrimitives.WriteSingleLittleEndian(vertex1.AsSpan(4), 5.0f);
        BinaryPrimitives.WriteSingleLittleEndian(vertex1.AsSpan(8), 6.0f);
        att.SetAttributeValue(1, vertex1);

        // Read back
        Span<float> values = stackalloc float[3];
        att.ConvertValue(0, values);
        Assert.AreEqual(1.0f, values[0]);
        Assert.AreEqual(2.0f, values[1]);
        Assert.AreEqual(3.0f, values[2]);

        att.ConvertValue(1, values);
        Assert.AreEqual(4.0f, values[0]);
        Assert.AreEqual(5.0f, values[1]);
        Assert.AreEqual(6.0f, values[2]);
    }

    [TestMethod]
    public void PointAttribute_MappedValueAccess()
    {
        var att = new PointAttribute();
        att.Init(AttributeType.Position, 1, DataType.UInt8, false, 3);

        att.SetAttributeValue(0, [10]);
        att.SetAttributeValue(1, [20]);
        att.SetAttributeValue(2, [30]);

        att.SetExplicitMapping(4);
        att.SetPointMapEntry(0, 2);
        att.SetPointMapEntry(1, 0);
        att.SetPointMapEntry(2, 1);
        att.SetPointMapEntry(3, 2);

        Span<byte> val = stackalloc byte[1];

        att.GetMappedValue(0, val);
        Assert.AreEqual(30, val[0]);

        att.GetMappedValue(1, val);
        Assert.AreEqual(10, val[0]);

        att.GetMappedValue(3, val);
        Assert.AreEqual(30, val[0]);
    }

    [TestMethod]
    public void PointAttribute_Resize_ChangesSize()
    {
        var att = new PointAttribute();
        att.Init(AttributeType.Position, 3, DataType.Float32, false, 4);
        Assert.AreEqual(4, att.Size);

        att.Resize(10);
        Assert.AreEqual(10, att.Size);
    }

    [TestMethod]
    public void AttributeTransformData_StoresAndRetrieves()
    {
        var data = new AttributeTransformData();
        data.TransformType = AttributeTransformType.QuantizationTransform;

        data.AppendParameterValue(3.14f);
        data.AppendParameterValue(42);

        Assert.AreEqual(AttributeTransformType.QuantizationTransform, data.TransformType);
        Assert.AreEqual(3.14f, data.GetParameterValue<float>(0), 1e-6);
        Assert.AreEqual(42, data.GetParameterValue<int>(4));
    }

    [TestMethod]
    public void GeometryAttribute_ConvertValue_UInt8Normalized()
    {
        var att = new PointAttribute();
        att.Init(AttributeType.Color, 3, DataType.UInt8, true, 1);
        att.SetAttributeValue(0, [255, 128, 0]);

        Span<float> values = stackalloc float[3];
        att.ConvertValue(0, values);

        Assert.AreEqual(1.0f, values[0], 0.01f);
        Assert.AreEqual(128.0f / 255.0f, values[1], 0.01f);
        Assert.AreEqual(0.0f, values[2], 0.01f);
    }
}
