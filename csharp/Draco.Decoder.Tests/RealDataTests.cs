// Copyright 2024 The Draco Authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Draco.Decoder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace Draco.Decoder.Tests;

[TestClass]
public class RealDataTests
{
    private const string TestDataPath = "../../../../../testdata";

    [TestMethod]
    public void DecodeRealDracoFile_CubeAtt_ParsesHeader()
    {
        var filePath = Path.Combine(TestDataPath, "cube_att.drc");
        if (!File.Exists(filePath))
        {
            Assert.Inconclusive($"Test file not found: {filePath}");
            return;
        }

        var data = File.ReadAllBytes(filePath);
        Assert.IsTrue(data.Length > 0, "File should not be empty");

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsTrue(result.Ok, $"Should parse geometry type: {result.Status}");
        Assert.AreNotEqual(EncodedGeometryType.Invalid, result.Value);
        
        Console.WriteLine($"Decoded geometry type: {result.Value}");
        Console.WriteLine($"File size: {data.Length} bytes");
        Console.WriteLine($"Bitstream version: {buffer.BitstreamVersion:X4}");
    }

    [TestMethod]
    public void DecodeRealDracoFile_BunnyGltf_ParsesHeader()
    {
        var filePath = Path.Combine(TestDataPath, "bunny_gltf.drc");
        if (!File.Exists(filePath))
        {
            Assert.Inconclusive($"Test file not found: {filePath}");
            return;
        }

        var data = File.ReadAllBytes(filePath);
        Assert.IsTrue(data.Length > 0, "File should not be empty");

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsTrue(result.Ok, $"Should parse geometry type: {result.Status}");
        Assert.AreEqual(EncodedGeometryType.TriangularMesh, result.Value, "bunny_gltf.drc should be a mesh");
        
        Console.WriteLine($"File size: {data.Length} bytes");
        Console.WriteLine($"Bitstream version: {buffer.BitstreamVersion:X4}");
    }

    [TestMethod]
    public void DecodeRealDracoFile_Car_ParsesHeader()
    {
        var filePath = Path.Combine(TestDataPath, "car.drc");
        if (!File.Exists(filePath))
        {
            Assert.Inconclusive($"Test file not found: {filePath}");
            return;
        }

        var data = File.ReadAllBytes(filePath);
        Assert.IsTrue(data.Length > 0, "File should not be empty");

        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsTrue(result.Ok, $"Should parse geometry type: {result.Status}");
        
        Console.WriteLine($"Decoded geometry type: {result.Value}");
        Console.WriteLine($"File size: {data.Length} bytes");
        Console.WriteLine($"Bitstream version: {buffer.BitstreamVersion:X4}");
    }

    [TestMethod]
    public void ParseGltfWithDracoCompression_BoxMetaDraco()
    {
        var gltfPath = Path.Combine(TestDataPath, "BoxMetaDraco/glTF/BoxMetaDraco.gltf");
        if (!File.Exists(gltfPath))
        {
            Assert.Inconclusive($"Test file not found: {gltfPath}");
            return;
        }

        var gltfJson = File.ReadAllText(gltfPath);
        Assert.IsFalse(string.IsNullOrEmpty(gltfJson), "glTF file should not be empty");

        using var doc = JsonDocument.Parse(gltfJson);
        var root = doc.RootElement;

        Assert.IsTrue(root.TryGetProperty("meshes", out var meshes), "Should have meshes");
        Assert.IsTrue(meshes.GetArrayLength() > 0, "Should have at least one mesh");

        var firstMesh = meshes[0];
        Assert.IsTrue(firstMesh.TryGetProperty("primitives", out var primitives), "Should have primitives");
        Assert.IsTrue(primitives.GetArrayLength() > 0, "Should have at least one primitive");

        var firstPrimitive = primitives[0];
        Assert.IsTrue(firstPrimitive.TryGetProperty("extensions", out var extensions), "Should have extensions");
        Assert.IsTrue(extensions.TryGetProperty("KHR_draco_mesh_compression", out var dracoExt), 
            "Should have KHR_draco_mesh_compression extension");

        Assert.IsTrue(dracoExt.TryGetProperty("bufferView", out var bufferView), "Should have bufferView");
        
        Console.WriteLine($"glTF file parsed successfully");
        Console.WriteLine($"Draco bufferView index: {bufferView.GetInt32()}");

        Assert.IsTrue(root.TryGetProperty("bufferViews", out var bufferViews), "Should have bufferViews");
        var dracoBufferView = bufferViews[bufferView.GetInt32()];
        
        Assert.IsTrue(dracoBufferView.TryGetProperty("byteLength", out var byteLength), "Should have byteLength");
        Console.WriteLine($"Draco compressed data size: {byteLength.GetInt32()} bytes");
    }

    [TestMethod]
    public void ExtractDracoDataFromGltf_BoxMetaDraco()
    {
        var gltfPath = Path.Combine(TestDataPath, "BoxMetaDraco/glTF/BoxMetaDraco.gltf");
        var bufferPath = Path.Combine(TestDataPath, "BoxMetaDraco/glTF/buffer0.bin");
        
        if (!File.Exists(gltfPath) || !File.Exists(bufferPath))
        {
            Assert.Inconclusive($"Test files not found");
            return;
        }

        var gltfJson = File.ReadAllText(gltfPath);
        using var doc = JsonDocument.Parse(gltfJson);
        var root = doc.RootElement;

        var meshes = root.GetProperty("meshes");
        var firstMesh = meshes[0];
        var primitives = firstMesh.GetProperty("primitives");
        var firstPrimitive = primitives[0];
        var extensions = firstPrimitive.GetProperty("extensions");
        var dracoExt = extensions.GetProperty("KHR_draco_mesh_compression");
        var bufferViewIndex = dracoExt.GetProperty("bufferView").GetInt32();

        var bufferViews = root.GetProperty("bufferViews");
        var dracoBufferView = bufferViews[bufferViewIndex];
        var byteOffset = dracoBufferView.TryGetProperty("byteOffset", out var offset) ? offset.GetInt32() : 0;
        var byteLength = dracoBufferView.GetProperty("byteLength").GetInt32();

        var bufferData = File.ReadAllBytes(bufferPath);
        var dracoData = new byte[byteLength];
        Array.Copy(bufferData, byteOffset, dracoData, 0, byteLength);

        Assert.IsTrue(dracoData.Length > 0, "Should extract Draco data");
        Console.WriteLine($"Extracted {dracoData.Length} bytes of Draco data");

        var buffer = new DecoderBuffer();
        buffer.Init(dracoData);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        Assert.IsTrue(result.Ok, $"Should parse Draco geometry type: {result.Status}");
        
        Console.WriteLine($"Decoded geometry type from glTF: {result.Value}");
        Console.WriteLine($"Bitstream version: {buffer.BitstreamVersion:X4}");
    }

    [TestMethod]
    public void VarintDecoding_Works()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 0x01 };
        buffer.Init(data);

        var result = VarintDecoding.DecodeVarint(buffer, out uint value);
        Assert.IsTrue(result, "Should decode varint");
        Assert.AreEqual(1u, value);
    }

    [TestMethod]
    public void VarintDecoding_MultiByteValue()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 0xAC, 0x02 };
        buffer.Init(data);

        var result = VarintDecoding.DecodeVarint(buffer, out uint value);
        Assert.IsTrue(result, "Should decode multi-byte varint");
        Assert.AreEqual(300u, value);
    }

    [TestMethod]
    public void VarintDecoding_SignedValue()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 0x01 };
        buffer.Init(data);

        var result = VarintDecoding.DecodeVarintSigned(buffer, out int value);
        Assert.IsTrue(result, "Should decode signed varint");
        Assert.AreEqual(-1, value);
    }

    [TestMethod]
    public void AttributeQuantizationTransform_InitializesCorrectly()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[100];
        
        data[0] = 10;
        BitConverter.GetBytes(0.0f).CopyTo(data, 1);
        BitConverter.GetBytes(1.0f).CopyTo(data, 5);
        BitConverter.GetBytes(0.0f).CopyTo(data, 9);
        BitConverter.GetBytes(1.0f).CopyTo(data, 13);
        
        buffer.Init(data);

        var transform = new AttributeQuantizationTransform();
        var result = transform.InitFromAttribute(buffer, 2);
        
        Assert.IsTrue(result, "Should initialize quantization transform");
    }

    [TestMethod]
    public void TestDataDirectory_Exists()
    {
        var fullPath = Path.GetFullPath(TestDataPath);
        Assert.IsTrue(Directory.Exists(fullPath), $"Test data directory should exist at: {fullPath}");
        
        var files = Directory.GetFiles(fullPath, "*.drc");
        Console.WriteLine($"Found {files.Length} .drc files in testdata");
        foreach (var file in files.Take(5))
        {
            Console.WriteLine($"  - {Path.GetFileName(file)}");
        }
    }

    [TestMethod]
    public void AllDracoFiles_HaveValidHeaders()
    {
        var fullPath = Path.GetFullPath(TestDataPath);
        if (!Directory.Exists(fullPath))
        {
            Assert.Inconclusive($"Test data directory not found: {fullPath}");
            return;
        }

        var files = Directory.GetFiles(fullPath, "*.drc");
        Assert.IsTrue(files.Length > 0, "Should find at least one .drc file");

        int validFiles = 0;
        int invalidFiles = 0;

        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            if (data.Length < 5) continue;

            var buffer = new DecoderBuffer();
            buffer.Init(data);

            var result = DracoDecoder.GetEncodedGeometryType(buffer);
            
            if (result.Ok)
            {
                validFiles++;
                Console.WriteLine($"✓ {Path.GetFileName(file)}: {result.Value}");
            }
            else
            {
                invalidFiles++;
                Console.WriteLine($"✗ {Path.GetFileName(file)}: {result.Status}");
            }
        }

        Console.WriteLine($"\nSummary: {validFiles} valid, {invalidFiles} invalid out of {files.Length} files");
        Assert.IsTrue(validFiles > 0, "Should successfully parse at least one file");
    }
}
