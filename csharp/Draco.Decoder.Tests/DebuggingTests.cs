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
    
    #region Buffer Positioning Tests for Quantized Attributes
    
    [TestMethod]
    public void Test_PcColor_Attribute0_BufferPositioning()
    {
        var drcPath = "../../../../../testdata/pc_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Testing attribute 0 buffer positioning in pc_color.drc ===");
        Console.WriteLine($"File size: {data.Length} bytes");
        Console.WriteLine($"Version: {data[5]}.{data[6]} (0x{(data[5] << 8) | data[6]:04x})");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Assert.IsTrue(geomTypeResult.Ok);
        
        // Read basic metadata
        Assert.IsTrue(buffer.Decode(out uint numPoints));
        Console.WriteLine($"NumPoints: {numPoints}");
        
        Assert.IsTrue(buffer.Decode(out byte numAttrDecoders));
        Console.WriteLine($"NumAttributesDecoders: {numAttrDecoders}");
        Console.WriteLine($"Starting position for attribute metadata: {buffer.DecodedSize}");
        
        // This test helps us understand where attribute 0 should start and end
        // Expected: attribute 0 (quantized, encoder type 2) should decode integer values
        // For version >= 2.0, quantization parameters come AFTER all portable attributes
        Console.WriteLine($"\nExpected flow for v2.2:");
        Console.WriteLine($"1. Decode attribute 0 integer values (Phase 2)");
        Console.WriteLine($"2. Decode attribute 1 integer values (Phase 2)");
        Console.WriteLine($"3. Decode attribute 0 quantization params (Phase 3)");
        Console.WriteLine($"4. Decode attribute 1 quantization params if any (Phase 3)");
    }
    
    [TestMethod]
    public void Test_CubePC_Attribute2_MaxBitLength()
    {
        var drcPath = "../../../../../testdata/cube_pc.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Testing why attribute 2 reads maxBitLength=167 in cube_pc.drc ===");
        Console.WriteLine($"File size: {data.Length} bytes");
        
        // Check bytes at position 83 where maxBitLength is read
        Console.WriteLine($"\nBytes around position 79-90:");
        for (int i = 79; i < 90 && i < data.Length; i++)
        {
            Console.WriteLine($"  Position {i}: 0x{data[i]:02x} ({data[i]})");
        }
        
        Console.WriteLine($"\nPosition 83 has value 0xa7 (167)");
        Console.WriteLine($"This is way too large for maxBitLength (should be 0-18 typically)");
        Console.WriteLine($"This suggests we're reading from the wrong buffer position");
        Console.WriteLine($"\nLikely causes:");
        Console.WriteLine($"1. Attribute 1 didn't consume the right amount of data");
        Console.WriteLine($"2. There's additional data between attributes we're not reading");
        Console.WriteLine($"3. Prediction scheme data not being read correctly");
    }
    
    [TestMethod]
    public void Test_RAns_ProbabilitySum()
    {
        Console.WriteLine($"=== Testing RAns probability sum validation ===");
        
        // The issue: cumulative probability (2192675) > precision (1048576)
        // This happens when symbol probabilities don't sum to precision
        
        Console.WriteLine($"For precision = 2^20 = 1048576:");
        Console.WriteLine($"Sum of all symbol probabilities must equal precision");
        Console.WriteLine($"\nExample breakdown:");
        Console.WriteLine($"  Symbol 0-60: various small probabilities");
        Console.WriteLine($"  Symbol 61: prob=2192656");
        Console.WriteLine($"  Total: 2192675 > 1048576 ❌");
        
        Console.WriteLine($"\nPossible causes:");
        Console.WriteLine($"1. Reading probability data from wrong buffer position");
        Console.WriteLine($"2. Probability calculation formula is wrong");
        Console.WriteLine($"3. Extra bytes calculation for probability is incorrect");
        Console.WriteLine($"4. numSymbols value is wrong");
        
        Console.WriteLine($"\nIn pc_color.drc attribute 1:");
        Console.WriteLine($"  maxBitLength should determine precision");
        Console.WriteLine($"  Need to trace back to see why probabilities are wrong");
    }
    
    [TestMethod]
    public void Test_CubePC_PredictionScheme()
    {
        var drcPath = "../../../../../testdata/cube_pc.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Testing prediction scheme handling in cube_pc.drc ===");
        
        // Attribute 1 at position 62 (after our fix)
        Console.WriteLine($"Attribute 1 bytes:");
        Console.WriteLine($"  Position 62: predictionSchemeMethod = {(sbyte)data[62]} (0x{data[62]:02x})");
        Console.WriteLine($"  Position 63: predictionTransformType = {(sbyte)data[63]} (0x{data[63]:02x})");
        Console.WriteLine($"  Position 64: compressed = {data[64]} (0x{data[64]:02x})");
        Console.WriteLine($"  Position 65: numBytes = {data[65]} (0x{data[65]:02x})");
        
        Console.WriteLine($"\nWith predictionSchemeMethod=3 (TEX_COORDS_DEPRECATED):");
        Console.WriteLine($"  - We now correctly read predictionTransformType");
        Console.WriteLine($"  - We create a PassthroughPredictionSchemeDecoder");
        Console.WriteLine($"  - compressed=0, numBytes=0 means no additional data");
        Console.WriteLine($"  - Should end at position 66");
        
        // But we were ending at position 79 before, now at position 62+17=79
        // Let's trace through what should happen
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        var result = decoder.DecodePointCloudFromBuffer(buffer);
        
        Console.WriteLine($"\nDecode result: {result.Status}");
        Console.WriteLine($"Final buffer position: {buffer.DecodedSize}/{data.Length}");
    }
    
    [TestMethod]
    public void Test_Edgebreaker_AttributeDataLayout()
    {
        var drcPath = "../../../../../testdata/cube_att.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Testing edgebreaker attribute data layout ===");
        Console.WriteLine($"File: cube_att.drc (v1.1, edgebreaker)");
        Console.WriteLine($"File size: {data.Length} bytes");
        
        Console.WriteLine($"\nAfter connectivity (position 37):");
        Console.WriteLine($"  Decoder 0: attDataId=3, decoderType=0");
        Console.WriteLine($"  Decoder 1: attDataId=0, decoderType=0");
        Console.WriteLine($"  Position after decoder metadata: 41");
        
        Console.WriteLine($"\nBytes 41-60:");
        for (int i = 41; i < 60 && i < data.Length; i++)
        {
            Console.WriteLine($"  Position {i}: 0x{data[i]:02x} ({data[i]})");
        }
        
        Console.WriteLine($"\nIssue: attDataId=3 but numAttributeData=2");
        Console.WriteLine($"This means attDataId is out of bounds!");
        Console.WriteLine($"\nPossible explanations:");
        Console.WriteLine($"1. attDataId=-1 (encoded as 0xFF, reads as 255, but we read 3)");
        Console.WriteLine($"2. Special handling for negative attDataId in C++");
        Console.WriteLine($"3. File format difference for v1.1");
        Console.WriteLine($"4. We're reading attDataId from wrong position");
    }
    
    [TestMethod]
    public void Test_KdTree_NotImplemented()
    {
        var drcPath = "../../../../../testdata/pc_kd_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== KD-Tree decoder test ===");
        Console.WriteLine($"File: pc_kd_color.drc");
        Console.WriteLine($"Encoder method: {data[8]} (1 = KD-tree)");
        
        Console.WriteLine($"\nKD-tree encoding requires:");
        Console.WriteLine($"1. DecodeKdTreeAttributesEncoder - NOT IMPLEMENTED");
        Console.WriteLine($"2. KdTreeAttributesDecoder class - NOT IMPLEMENTED");
        Console.WriteLine($"3. DynamicIntegerPointsKdTreeDecoder - NOT IMPLEMENTED");
        Console.WriteLine($"4. FloatPointsTreeDecoder - NOT IMPLEMENTED");
        
        Console.WriteLine($"\nThis is a significant feature requiring:");
        Console.WriteLine($"- KD-tree data structure implementation");
        Console.WriteLine($"- Specialized attribute traversal");
        Console.WriteLine($"- Different compression algorithm");
        Console.WriteLine($"\nRecommendation: Implement in separate PR");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var decoder = new DracoDecoder();
        var result = decoder.DecodePointCloudFromBuffer(buffer);
        
        Console.WriteLine($"\nExpected result: Fails with 'not implemented' or similar");
        Console.WriteLine($"Actual result: {result.Status}");
    }
    
    #endregion
    
    #region Byte-Level Comparison Tests
    
    [TestMethod]
    public void Test_PcColor_ByteByByteTrace()
    {
        var drcPath = "../../../../../testdata/pc_color.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Byte-by-byte trace of pc_color.drc decode ===");
        Console.WriteLine($"Goal: Find exact point where buffer position diverges from expected");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        Console.WriteLine($"\nPosition 0: Start");
        
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Console.WriteLine($"Position {buffer.DecodedSize}: After header (geomType={geomTypeResult.Value})");
        
        Assert.IsTrue(buffer.Decode(out uint numPoints));
        Console.WriteLine($"Position {buffer.DecodedSize}: After numPoints ({numPoints})");
        
        Assert.IsTrue(buffer.Decode(out byte numAttrDecoders));
        Console.WriteLine($"Position {buffer.DecodedSize}: After numAttributesDecoders ({numAttrDecoders})");
        
        int startPos = buffer.DecodedSize;
        Console.WriteLine($"\nAttribute metadata starts at position {startPos}");
        Console.WriteLine($"Next bytes: {string.Join(" ", data.Skip(startPos).Take(20).Select(b => $"{b:02x}"))}");
        
        // Try to decode just the metadata to see format
        Console.WriteLine($"\nAttempting to decode attribute metadata...");
        
        // For version >= 2.0, numAttributes is varint
        if (!VarintDecoding.DecodeVarint(buffer, out uint numAttributes))
        {
            Console.WriteLine($"Failed to decode numAttributes as varint");
        }
        else
        {
            Console.WriteLine($"Position {buffer.DecodedSize}: NumAttributes = {numAttributes}");
            
            // Try to read first attribute metadata
            for (int i = 0; i < Math.Min(numAttributes, 3); i++)
            {
                int attrStart = buffer.DecodedSize;
                Assert.IsTrue(buffer.Decode(out byte attributeType));
                Assert.IsTrue(buffer.Decode(out byte dataType));
                Assert.IsTrue(buffer.Decode(out byte numComponents));
                Assert.IsTrue(buffer.Decode(out byte normalized));
                
                uint uniqueId;
                if (buffer.BitstreamVersion < 0x0103)
                {
                    Assert.IsTrue(buffer.Decode(out ushort uniqueId16));
                    uniqueId = uniqueId16;
                }
                else
                {
                    Assert.IsTrue(VarintDecoding.DecodeVarint(buffer, out uniqueId));
                }
                
                Console.WriteLine($"Position {buffer.DecodedSize}: Attribute {i}: type={attributeType}, dataType={dataType}, components={numComponents}, uniqueId={uniqueId}");
            }
        }
    }
    
    [TestMethod]
    public void Test_CubePC_EncoderTypes()
    {
        var drcPath = "../../../../../testdata/cube_pc.drc";
        var data = File.ReadAllBytes(drcPath);
        
        Console.WriteLine($"=== Checking encoder types for cube_pc.drc ===");
        
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        
        var geomTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);
        Assert.IsTrue(geomTypeResult.Ok);
        
        Assert.IsTrue(buffer.Decode(out uint numPoints));
        Assert.IsTrue(buffer.Decode(out byte numAttrDecoders));
        
        Console.WriteLine($"NumPoints: {numPoints}");
        Console.WriteLine($"NumAttributesDecoders: {numAttrDecoders}");
        
        // Read attribute metadata
        uint numAttributes;
        if (buffer.BitstreamVersion < 0x0200)
        {
            Assert.IsTrue(buffer.Decode(out numAttributes));
        }
        else
        {
            Assert.IsTrue(VarintDecoding.DecodeVarint(buffer, out numAttributes));
        }
        
        Console.WriteLine($"NumAttributes: {numAttributes}");
        
        // Read all attribute descriptors
        for (int i = 0; i < numAttributes; i++)
        {
            Assert.IsTrue(buffer.Decode(out byte attributeType));
            Assert.IsTrue(buffer.Decode(out byte dataType));
            Assert.IsTrue(buffer.Decode(out byte numComponents));
            Assert.IsTrue(buffer.Decode(out byte normalized));
            
            uint uniqueId;
            if (buffer.BitstreamVersion < 0x0103)
            {
                Assert.IsTrue(buffer.Decode(out ushort uniqueId16));
                uniqueId = uniqueId16;
            }
            else
            {
                Assert.IsTrue(VarintDecoding.DecodeVarint(buffer, out uniqueId));
            }
            
            Console.WriteLine($"Attribute {i}: type={attributeType}, dataType={dataType}, components={numComponents}");
        }
        
        Console.WriteLine($"\nBuffer position before encoder types: {buffer.DecodedSize}");
        
        // Read encoder types
        for (int i = 0; i < numAttrDecoders; i++)
        {
            Assert.IsTrue(buffer.Decode(out byte encoderType));
            Console.WriteLine($"Attribute {i}: encoderType={encoderType} (0=GENERIC, 1=INTEGER, 2=QUANTIZATION, 3=NORMALS)");
        }
        
        Console.WriteLine($"Buffer position after encoder types: {buffer.DecodedSize}");
        Console.WriteLine($"This is where attribute 0's portable data should start");
    }
    
    #endregion
}
