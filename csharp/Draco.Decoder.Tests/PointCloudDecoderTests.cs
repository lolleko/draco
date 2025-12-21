using Draco.Decoder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PointCloudTest
{
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
