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

namespace Draco.Decoder.Tests;

public class DecoderIntegrationTests
{
    [Fact]
    public void DracoDecoder_CanIdentifyDracoFile()
    {
        // Create a minimal valid Draco header
        var buffer = new DecoderBuffer();
        var data = CreateMinimalDracoHeader();
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.True(result.Ok);
        Assert.Equal(EncodedGeometryType.TriangularMesh, result.Value);
    }

    [Fact]
    public void DracoDecoder_RejectsInvalidMagic()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.False(result.Ok);
        Assert.Contains("magic", result.Status.ErrorMessage.ToLower());
    }

    [Fact]
    public void DracoDecoder_RejectsUnsupportedVersion()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[10];
        // "DRACO"
        data[0] = (byte)'D';
        data[1] = (byte)'R';
        data[2] = (byte)'A';
        data[3] = (byte)'C';
        data[4] = (byte)'O';
        // Version 99.99 (unsupported)
        data[5] = 99;
        data[6] = 99;
        
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.False(result.Ok);
        Assert.Equal(StatusCode.UnsupportedVersion, result.Status.Code);
    }

    private byte[] CreateMinimalDracoHeader()
    {
        var header = new List<byte>();
        
        // Magic: "DRACO"
        header.AddRange(System.Text.Encoding.ASCII.GetBytes("DRACO"));
        
        // Version: 2.2
        header.Add(2);  // Major
        header.Add(2);  // Minor
        
        // Encoder type: 1 (mesh)
        header.Add(1);
        
        // Encoder method: 1 (edgebreaker)
        header.Add(1);
        
        // Flags: 0x0000
        header.Add(0);
        header.Add(0);
        
        return header.ToArray();
    }

    [Fact]
    public void Mesh_ToFromAttributes_RoundTrip()
    {
        // Create a simple triangle mesh
        var mesh = new Mesh();
        mesh.NumPoints = 3;
        mesh.AddFace(new Face(0, 1, 2));

        // Add position attribute
        var posAttr = new PointAttribute();
        posAttr.Init(GeometryAttributeType.Position, DataType.Float32, 3, 3);
        
        // Set some vertex positions
        Span<float> v0 = stackalloc float[] { 0.0f, 0.0f, 0.0f };
        Span<float> v1 = stackalloc float[] { 1.0f, 0.0f, 0.0f };
        Span<float> v2 = stackalloc float[] { 0.0f, 1.0f, 0.0f };
        
        posAttr.SetValue(0, System.Runtime.InteropServices.MemoryMarshal.AsBytes(v0));
        posAttr.SetValue(1, System.Runtime.InteropServices.MemoryMarshal.AsBytes(v1));
        posAttr.SetValue(2, System.Runtime.InteropServices.MemoryMarshal.AsBytes(v2));
        
        mesh.AddAttribute(posAttr);

        // Verify we can read back the data
        var retrievedAttr = mesh.GetNamedAttribute(GeometryAttributeType.Position);
        Assert.NotNull(retrievedAttr);
        Assert.Equal(3, retrievedAttr.NumComponents);
        Assert.Equal(DataType.Float32, retrievedAttr.DataType);
        
        // Read back first vertex
        var vertexData = retrievedAttr.GetValue(0);
        var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(vertexData);
        Assert.Equal(0.0f, floats[0]);
        Assert.Equal(0.0f, floats[1]);
        Assert.Equal(0.0f, floats[2]);
    }
}
