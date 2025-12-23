using Draco.Decoder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PointCloudTest
{
    [TestMethod]
    public void DecodePointCloud_PointCloudNoQp_Success()
    {
        var drcPath = "../../../../../testdata/point_cloud_no_qp.drc";
        var data = File.ReadAllBytes(drcPath);
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var decoder = new DracoDecoder();
        var result = decoder.DecodePointCloudFromBuffer(buffer);
        
        Assert.IsTrue(result.Ok, $"Decoding should succeed: {result.Status}");
        
        var pc = result.Value;
        Assert.AreEqual(21, pc.NumPoints, "Should have 21 points");
        Assert.AreEqual(2, pc.NumAttributes, "Should have 2 attributes");
        
        var posAttr = pc.GetNamedAttribute(GeometryAttributeType.Position);
        Assert.IsNotNull(posAttr, "Should have position attribute");
        Assert.AreEqual(3, posAttr.NumComponents, "Position should have 3 components");
        
        var colorAttr = pc.GetNamedAttribute(GeometryAttributeType.Color);
        Assert.IsNotNull(colorAttr, "Should have color attribute");
        Assert.AreEqual(3, colorAttr.NumComponents, "Color should have 3 components");
        
        Console.WriteLine($"✅ Successfully decoded point_cloud_no_qp.drc: {pc.NumPoints} points, {pc.NumAttributes} attributes");
    }

    [TestMethod]
    public void DecodePointCloud_PcColor_Success()
    {
        var drcPath = "../../../../../testdata/pc_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var decoder = new DracoDecoder();
        var result = decoder.DecodePointCloudFromBuffer(buffer);
        
        Assert.IsTrue(result.Ok, $"Decoding should succeed: {result.Status}");
        
        var pc = result.Value;
        Assert.IsTrue(pc.NumPoints > 0, "Should have points");
        Assert.IsTrue(pc.NumAttributes >= 1, "Should have at least 1 attribute");
        
        Console.WriteLine($"✅ Successfully decoded pc_color.drc: {pc.NumPoints} points, {pc.NumAttributes} attributes");
    }

    [TestMethod]
    public void DecodePointCloud_PcKdColor_Success()
    {
        var drcPath = "../../../../../testdata/pc_kd_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var decoder = new DracoDecoder();
        var result = decoder.DecodePointCloudFromBuffer(buffer);
        
        Assert.IsTrue(result.Ok, $"Decoding should succeed: {result.Status}");
        
        var pc = result.Value;
        Assert.IsTrue(pc.NumPoints > 0, "Should have points");
        Assert.IsTrue(pc.NumAttributes >= 1, "Should have at least 1 attribute");
        
        Console.WriteLine($"✅ Successfully decoded pc_kd_color.drc: {pc.NumPoints} points, {pc.NumAttributes} attributes");
    }

    [TestMethod]
    public void DecodePointCloud_CubePC()
    {
        var drcPath = "../../../../../testdata/cube_pc.drc";
        var data = File.ReadAllBytes(drcPath);
        Console.WriteLine($"File size: {data.Length} bytes");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var decoder = new DracoDecoder();
        var result = decoder.DecodePointCloudFromBuffer(buffer);

        Console.WriteLine($"Decode status: {result.Status}");
        Console.WriteLine($"Buffer position after decode: {buffer.DecodedSize}/{data.Length}");
        
        if (result.Ok)
        {
            var pc = result.Value;
            Console.WriteLine($"Points: {pc.NumPoints}");
            Console.WriteLine($"Attributes: {pc.NumAttributes}");
            
            for (int i = 0; i < pc.NumAttributes; i++)
            {
                var attr = pc.GetAttribute(i);
                if (attr != null)
                {
                    Console.WriteLine($"  Attr {i}: {attr.AttributeType}, {attr.NumComponents} components, {attr.DataType}");
                }
            }
        }
        
        Assert.IsTrue(result.Ok, "Decoding should succeed");
    }
}
