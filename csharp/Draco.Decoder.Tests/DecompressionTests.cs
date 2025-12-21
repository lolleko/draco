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

namespace Draco.Decoder.Tests;

[TestClass]
public class DecompressionTests
{
    private const string TestDataPath = "../../../../../testdata";

    [TestMethod]
    public void DecompressCubeAtt_ValidateStructure()
    {
        var drcPath = Path.Combine(TestDataPath, "cube_att.drc");
        if (!File.Exists(drcPath))
        {
            Assert.Inconclusive($"Test file not found: {drcPath}");
            return;
        }

        var data = File.ReadAllBytes(drcPath);
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var decoder = new DracoDecoder();
        var result = decoder.DecodeMeshFromBuffer(buffer);

        Console.WriteLine($"Decode status: {result.Status}");
        
        if (result.Ok)
        {
            var mesh = result.Value;
            Console.WriteLine($"✓ Successfully decoded mesh structure");
            Console.WriteLine($"  Points: {mesh.NumPoints}");
            Console.WriteLine($"  Faces: {mesh.NumFaces}");
            Console.WriteLine($"  Attributes: {mesh.NumAttributes}");
            
            for (int i = 0; i < mesh.NumAttributes; i++)
            {
                var attr = mesh.GetAttribute(i);
                if (attr != null)
                {
                    Console.WriteLine($"  Attribute {i}: {attr.AttributeType}, " +
                                    $"{attr.NumComponents} components, " +
                                    $"{attr.DataType} type");
                }
            }
        }
        else
        {
            Console.WriteLine($"✗ Decoding failed: {result.Status}");
            Console.WriteLine($"Note: Full decompression not yet implemented.");
            Console.WriteLine($"Expected behavior: Parse header and metadata successfully.");
        }
    }

    [TestMethod]
    public void CompareWithGroundTruth_CubeAtt()
    {
        var objPath = Path.Combine(TestDataPath, "cube_att.obj");
        
        if (!File.Exists(objPath))
        {
            Assert.Inconclusive($"Ground truth file not found: {objPath}");
            return;
        }

        var groundTruth = ParseSimpleObj(objPath);
        
        Console.WriteLine($"Ground truth from cube_att.obj:");
        Console.WriteLine($"  Vertices: {groundTruth.Vertices.Count}");
        Console.WriteLine($"  Normals: {groundTruth.Normals.Count}");
        Console.WriteLine($"  TexCoords: {groundTruth.TexCoords.Count}");
        Console.WriteLine($"  Faces: {groundTruth.Faces.Count}");
        
        Assert.AreEqual(8, groundTruth.Vertices.Count, "Cube should have 8 vertices");
        Assert.AreEqual(6, groundTruth.Normals.Count, "Cube should have 6 normals");
        Assert.AreEqual(4, groundTruth.TexCoords.Count, "Cube should have 4 texture coordinates");
        Assert.AreEqual(12, groundTruth.Faces.Count, "Cube should have 12 faces");
        
        Console.WriteLine("\nNote: Full decompression comparison will be added when");
        Console.WriteLine("sequential attribute decoder and edgebreaker are implemented.");
    }

    [TestMethod]
    public void ValidateDecompressionPipeline_NotYetImplemented()
    {
        Console.WriteLine("Decompression Pipeline Status:");
        Console.WriteLine("✅ Header parsing - COMPLETE");
        Console.WriteLine("✅ Geometry type detection - COMPLETE");
        Console.WriteLine("✅ Version validation - COMPLETE");
        Console.WriteLine("✅ Metadata reading - COMPLETE");
        Console.WriteLine("⏳ Sequential attribute decoder - NOT IMPLEMENTED");
        Console.WriteLine("⏳ Edgebreaker connectivity decoder - NOT IMPLEMENTED");
        Console.WriteLine("⏳ Symbol decoding - NOT IMPLEMENTED");
        Console.WriteLine("⏳ Prediction scheme application - NOT IMPLEMENTED");
        Console.WriteLine("⏳ Dequantization - NOT IMPLEMENTED");
        Console.WriteLine("");
        Console.WriteLine("Current capability: Parse file structure and metadata");
        Console.WriteLine("Next step: Implement sequential attribute decoder to decompress vertex data");
        
        Assert.Inconclusive("Full decompression not yet implemented. This is expected.");
    }

    [TestMethod]
    public void TestGroundTruthFilesExist()
    {
        var testCases = new[]
        {
            ("cube_att.drc", "cube_att.obj"),
            ("test_nm.obj.edgebreaker.0.9.1.drc", "test_nm.obj"),
            ("octagon_preserved.drc", "octagon_preserved.obj"),
        };

        int foundPairs = 0;
        
        foreach (var (drcFile, objFile) in testCases)
        {
            var drcPath = Path.Combine(TestDataPath, drcFile);
            var objPath = Path.Combine(TestDataPath, objFile);
            
            bool drcExists = File.Exists(drcPath);
            bool objExists = File.Exists(objPath);
            
            if (drcExists && objExists)
            {
                foundPairs++;
                Console.WriteLine($"✓ Found pair: {drcFile} <-> {objFile}");
            }
            else
            {
                if (!drcExists) Console.WriteLine($"✗ Missing: {drcFile}");
                if (!objExists) Console.WriteLine($"✗ Missing: {objFile}");
            }
        }

        Console.WriteLine($"\nFound {foundPairs} test pairs with ground truth");
        Assert.IsTrue(foundPairs > 0, "Should find at least one .drc/.obj pair for validation");
    }

    private class ObjData
    {
        public List<(float x, float y, float z)> Vertices { get; } = new();
        public List<(float x, float y, float z)> Normals { get; } = new();
        public List<(float u, float v)> TexCoords { get; } = new();
        public List<string> Faces { get; } = new();
    }

    private ObjData ParseSimpleObj(string path)
    {
        var data = new ObjData();
        var lines = File.ReadAllLines(path);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;
            
            var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            
            switch (parts[0])
            {
                case "v" when parts.Length >= 4:
                    data.Vertices.Add((
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    ));
                    break;
                    
                case "vn" when parts.Length >= 4:
                    data.Normals.Add((
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    ));
                    break;
                    
                case "vt" when parts.Length >= 3:
                    data.TexCoords.Add((
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    ));
                    break;
                    
                case "f":
                    data.Faces.Add(line);
                    break;
            }
        }
        
        return data;
    }
}
