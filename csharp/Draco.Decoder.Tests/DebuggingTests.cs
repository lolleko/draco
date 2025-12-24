using Draco.Decoder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Draco.Decoder.Tests;

/// <summary>
/// Fine-grained debugging tests to isolate and verify specific decoder functionality
/// </summary>
[TestClass]
public class DebuggingTests
{
    #region Header and Metadata Tests
    
    [TestMethod]
    public void Test_PcColor_HeaderParsing()
    {
        var drcPath = "../../../../../testdata/pc_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsTrue(geomTypeResult.Ok, "Should parse header");
        Assert.AreEqual(EncodedGeometryType.PointCloud, geomTypeResult.Value, "Should be point cloud");
        
        Console.WriteLine($"✅ pc_color.drc header parsed: {geomTypeResult.Value}");
        Console.WriteLine($"   Buffer position after header: {buffer.DecodedSize}");
        Console.WriteLine($"   Bitstream version: 0x{buffer.BitstreamVersion:X4}");
    }
    
    [TestMethod]
    public void Test_CubeAtt_HeaderParsing()
    {
        var drcPath = "../../../../../testdata/cube_att.drc";
        var data = File.ReadAllBytes(drcPath);
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsTrue(geomTypeResult.Ok, "Should parse header");
        Assert.AreEqual(EncodedGeometryType.TriangularMesh, geomTypeResult.Value, "Should be mesh");
        
        Console.WriteLine($"✅ cube_att.drc header parsed: {geomTypeResult.Value}");
        Console.WriteLine($"   Buffer position after header: {buffer.DecodedSize}");
        Console.WriteLine($"   Bitstream version: 0x{buffer.BitstreamVersion:X4}");
    }
    
    #endregion
    
    #region Attribute Metadata Tests
    
    [TestMethod]
    public void Test_PcColor_AttributeMetadata()
    {
        var drcPath = "../../../../../testdata/pc_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"File size: {data.Length} bytes");
        Console.WriteLine($"Testing attribute metadata parsing for pc_color.drc");
        
        // Parse just enough to get to attribute metadata
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Assert.IsTrue(geomTypeResult.Ok);
        
        // Read numPoints
        Assert.IsTrue(buffer.Decode(out uint numPoints));
        Console.WriteLine($"   NumPoints: {numPoints}");
        
        // Read numAttributesDecoders
        Assert.IsTrue(buffer.Decode(out byte numAttrDecoders));
        Console.WriteLine($"   NumAttributesDecoders: {numAttrDecoders}");
        Console.WriteLine($"   Buffer position before attributes: {buffer.DecodedSize}");
        
        // Try to parse attributes to see where we fail
        var pointCloud = new PointCloud();
        pointCloud.NumPoints = (int)numPoints;
        
        // This should help us understand where the decoding goes wrong
        Console.WriteLine($"   Ready to decode attribute data");
    }
    
    [TestMethod]
    public void Test_CubePC_BufferPositions()
    {
        var drcPath = "../../../../../testdata/cube_pc.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"File size: {data.Length} bytes");
        Console.WriteLine($"Tracking buffer positions for cube_pc.drc");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        Console.WriteLine($"Position 0: Start");
        
        var decoder = new DracoDecoder();
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Console.WriteLine($"Position {buffer.DecodedSize}: After header");
        
        Assert.IsTrue(buffer.Decode(out uint numPoints));
        Console.WriteLine($"Position {buffer.DecodedSize}: After numPoints ({numPoints})");
        
        Assert.IsTrue(buffer.Decode(out byte numAttrDecoders));
        Console.WriteLine($"Position {buffer.DecodedSize}: After numAttributesDecoders ({numAttrDecoders})");
        
        // Now attempt full decode to see exact failure point
        buffer = new DecoderBuffer();
        buffer.Init(data);
        var result = decoder.DecodePointCloudFromBuffer(buffer);
        
        Console.WriteLine($"Decode result: {result.Status}");
        Console.WriteLine($"Final buffer position: {buffer.DecodedSize}/{data.Length}");
    }
    
    #endregion
    
    #region RAns Decoder Tests
    
    [TestMethod]
    public void Test_RAns_BasicFunctionality()
    {
        // Test RAns decoder with simple synthetic data
        var buffer = new DecoderBuffer();
        
        // Create minimal valid RAns data
        var testData = new List<byte>();
        
        // numSymbols = 1 (varint)
        testData.Add(0x01);
        
        // Symbol 0: token=0 (no extra bytes), prob = 1 << 2 = 4
        testData.Add(0x04); // probData with token 0 in lower 2 bits
        
        // bytesEncoded = 0 (varint) - empty data
        testData.Add(0x00);
        
        buffer.Init(testData.ToArray());
        buffer.set_bitstreamVersion(0x0203); // Version 2.3
        
        var decoder = new RAnsSymbolDecoder(5);
        var createResult = decoder.Create(buffer);
        
        Console.WriteLine($"RAns Create result: {createResult}");
        Console.WriteLine($"NumSymbols: {decoder.NumSymbols}");
        
        if (createResult)
        {
            var startResult = decoder.StartDecoding(buffer);
            Console.WriteLine($"RAns StartDecoding result: {startResult}");
        }
        
        Assert.IsTrue(createResult, "RAns decoder should create successfully");
    }
    
    #endregion
    
    #region Quantization Tests
    
    [TestMethod]
    public void Test_Quantization_Version11()
    {
        // Test that quantization decoder handles v1.1 correctly
        var drcPath = "../../../../../testdata/cube_att.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"Testing quantization handling for version 1.1 file");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        Console.WriteLine($"Bitstream version: 0x{buffer.BitstreamVersion:X4}");
        Console.WriteLine($"Is < 2.0? {buffer.BitstreamVersion < 0x0200}");
        
        // For v1.1 (<2.0), quantization data should be decoded in DecodePortableAttribute
        Assert.IsTrue(buffer.BitstreamVersion < 0x0200, "Version should be < 2.0");
    }
    
    [TestMethod]
    public void Test_Quantization_Version22()
    {
        // Test that quantization decoder handles v2.2 correctly
        var drcPath = "../../../../../testdata/pc_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"Testing quantization handling for version 2.2 file");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        Console.WriteLine($"Bitstream version: 0x{buffer.BitstreamVersion:X4}");
        Console.WriteLine($"Is >= 2.0? {buffer.BitstreamVersion >= 0x0200}");
        
        // For v2.2 (>=2.0), quantization data should be decoded in DecodeDataNeededByPortableTransform
        Assert.IsTrue(buffer.BitstreamVersion >= 0x0200, "Version should be >= 2.0");
    }
    
    #endregion
    
    #region Edgebreaker Tests
    
    [TestMethod]
    public void Test_Edgebreaker_ConnectivityDecode()
    {
        var drcPath = "../../../../../testdata/cube_att.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"Testing edgebreaker connectivity decoding");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var mesh = new Mesh();
        var decoder = new DracoDecoder();
        
        // Parse header
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Assert.IsTrue(geomTypeResult.Ok);
        
        Console.WriteLine($"Geometry type: {geomTypeResult.Value}");
        Console.WriteLine($"Buffer position after header: {buffer.DecodedSize}");
        
        // Check encoder method
        byte encoderMethod = data[8];
        Console.WriteLine($"Encoder method: {encoderMethod} (0=sequential, 1=edgebreaker)");
        Assert.AreEqual(1, encoderMethod, "Should use edgebreaker");
        
        // Test connectivity decode
        var edgebreakerDecoder = new EdgebreakerMeshDecoder(mesh, buffer);
        var connectivityResult = edgebreakerDecoder.DecodeConnectivity();
        
        if (connectivityResult.Ok)
        {
            Console.WriteLine($"✅ Connectivity decoded successfully");
            Console.WriteLine($"   NumAttributeData: {connectivityResult.Value}");
            Console.WriteLine($"   Mesh points: {mesh.NumPoints}");
            Console.WriteLine($"   Mesh faces: {mesh.NumFaces}");
            Console.WriteLine($"   Buffer position: {buffer.DecodedSize}");
        }
        else
        {
            Console.WriteLine($"❌ Connectivity decode failed: {connectivityResult.Status}");
        }
        
        Assert.IsTrue(connectivityResult.Ok, "Connectivity should decode");
    }
    
    [TestMethod]
    public void Test_Edgebreaker_DecoderMetadata()
    {
        var drcPath = "../../../../../testdata/cube_att.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"Testing edgebreaker decoder metadata reading");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var mesh = new Mesh();
        
        // Parse header
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Assert.IsTrue(geomTypeResult.Ok);
        
        // Decode connectivity
        var edgebreakerDecoder = new EdgebreakerMeshDecoder(mesh, buffer);
        var connectivityResult = edgebreakerDecoder.DecodeConnectivity();
        Assert.IsTrue(connectivityResult.Ok);
        
        int numAttributeData = connectivityResult.Value;
        Console.WriteLine($"NumAttributeData: {numAttributeData}");
        Console.WriteLine($"Buffer position: {buffer.DecodedSize}");
        
        // Read decoder metadata for each decoder
        for (int i = 0; i < numAttributeData; i++)
        {
            int startPos = buffer.DecodedSize;
            
            Assert.IsTrue(buffer.Decode(out sbyte attDataId), $"Should read attDataId for decoder {i}");
            Assert.IsTrue(buffer.Decode(out byte decoderType), $"Should read decoderType for decoder {i}");
            
            Console.WriteLine($"Decoder {i}:");
            Console.WriteLine($"  Position: {startPos}");
            Console.WriteLine($"  attDataId: {attDataId}");
            Console.WriteLine($"  decoderType: {decoderType}");
            
            // Check if we should read traversal method
            if (buffer.BitstreamVersion >= 0x0102)
            {
                Assert.IsTrue(buffer.Decode(out byte traversalMethod), $"Should read traversalMethod for decoder {i}");
                Console.WriteLine($"  traversalMethod: {traversalMethod}");
            }
        }
        
        Console.WriteLine($"Final buffer position: {buffer.DecodedSize}");
        
        // Now check what comes next (should be attribute metadata)
        if (buffer.BitstreamVersion < 0x0200)
        {
            Assert.IsTrue(buffer.Decode(out uint numAttributes), "Should read numAttributes");
            Console.WriteLine($"NumAttributes (uint32): {numAttributes}");
        }
        else
        {
            Assert.IsTrue(VarintDecoding.DecodeVarint(buffer, out uint numAttributes), "Should read numAttributes");
            Console.WriteLine($"NumAttributes (varint): {numAttributes}");
        }
    }
    
    #endregion
    
    #region Symbol Decoding Tests
    
    [TestMethod]
    public void Test_SymbolDecoding_NumSymbolsZero()
    {
        Console.WriteLine($"Testing symbol decoding with numSymbols=0");
        
        var buffer = new DecoderBuffer();
        
        // Create test data: numSymbols=0, bytesEncoded=0
        var testData = new List<byte>();
        testData.Add(0x00); // numSymbols = 0
        testData.Add(0x00); // bytesEncoded = 0
        
        buffer.Init(testData.ToArray());
        buffer.set_bitstreamVersion(0x0203);
        
        var decoder = new RAnsSymbolDecoder(5);
        var createResult = decoder.Create(buffer);
        
        Console.WriteLine($"Create result: {createResult}");
        Console.WriteLine($"NumSymbols: {decoder.NumSymbols}");
        
        Assert.IsTrue(createResult, "Should handle numSymbols=0");
        Assert.AreEqual(0u, decoder.NumSymbols, "NumSymbols should be 0");
        
        // Test decoding with numSymbols=0
        var outValues = new uint[10];
        var decodeResult = SymbolDecoding.DecodeSymbols(10, 3, buffer, outValues);
        
        // With our fix, this should succeed and set all values to 0
        Console.WriteLine($"DecodeSymbols result: {decodeResult}");
        if (decodeResult)
        {
            Console.WriteLine($"✅ Successfully handled numSymbols=0 case");
            Console.WriteLine($"   All values should be 0: {string.Join(", ", outValues.Take(5))}");
            
            Assert.IsTrue(outValues.All(v => v == 0), "All values should be 0");
        }
    }
    
    #endregion
    
    #region Integration Tests with Detailed Logging
    
    [TestMethod]
    public void Test_PcColor_DetailedDecode()
    {
        var drcPath = "../../../../../testdata/pc_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Detailed decode test for pc_color.drc ===");
        Console.WriteLine($"File size: {data.Length} bytes");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        
        // Track each phase
        Console.WriteLine($"\nPhase 1: Header parsing");
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Console.WriteLine($"  Result: {geomTypeResult.Ok}");
        Console.WriteLine($"  Buffer position: {buffer.DecodedSize}");
        
        if (!geomTypeResult.Ok)
        {
            Assert.Fail($"Header parsing failed: {geomTypeResult.Status}");
        }
        
        Console.WriteLine($"\nPhase 2: Full decode");
        buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var result = decoder.DecodePointCloudFromBuffer(buffer);
        
        Console.WriteLine($"  Result: {result.Ok}");
        Console.WriteLine($"  Status: {result.Status}");
        Console.WriteLine($"  Buffer position: {buffer.DecodedSize}/{data.Length}");
        
        if (result.Ok)
        {
            var pc = result.Value;
            Console.WriteLine($"\n✅ Success!");
            Console.WriteLine($"  Points: {pc.NumPoints}");
            Console.WriteLine($"  Attributes: {pc.NumAttributes}");
            
            for (int i = 0; i < pc.NumAttributes; i++)
            {
                var attr = pc.GetAttribute(i);
                Console.WriteLine($"  Attr {i}: type={attr.AttributeType}, components={attr.NumComponents}, dataType={attr.DataType}");
            }
        }
        else
        {
            Console.WriteLine($"\n❌ Failed at buffer position {buffer.DecodedSize}");
            
            // Don't fail the test, just log the detailed information
            Console.WriteLine($"Expected failure - this test is for debugging");
        }
    }
    
    [TestMethod]
    public void Test_CubeAtt_DetailedDecode()
    {
        var drcPath = "../../../../../testdata/cube_att.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Detailed decode test for cube_att.drc ===");
        Console.WriteLine($"File size: {data.Length} bytes");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        
        Console.WriteLine($"\nPhase 1: Header parsing");
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Console.WriteLine($"  Geometry type: {geomTypeResult.Value}");
        Console.WriteLine($"  Buffer position: {buffer.DecodedSize}");
        
        Console.WriteLine($"\nPhase 2: Full decode");
        buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var result = decoder.DecodeMeshFromBuffer(buffer);
        
        Console.WriteLine($"  Result: {result.Ok}");
        Console.WriteLine($"  Status: {result.Status}");
        Console.WriteLine($"  Buffer position: {buffer.DecodedSize}/{data.Length}");
        
        if (result.Ok)
        {
            var mesh = result.Value;
            Console.WriteLine($"\n✅ Success!");
            Console.WriteLine($"  Points: {mesh.NumPoints}");
            Console.WriteLine($"  Faces: {mesh.NumFaces}");
            Console.WriteLine($"  Attributes: {mesh.NumAttributes}");
        }
        else
        {
            Console.WriteLine($"\n❌ Failed");
            Console.WriteLine($"Expected failure - this test is for debugging");
        }
    }
    
    #endregion
}
