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
public class DecoderIntegrationTests
{
    [TestMethod]
    public void DracoDecoder_CanIdentifyDracoFile()
    {
        var buffer = new DecoderBuffer();
        var data = CreateMinimalDracoHeader();
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsTrue(result.Ok);
        Assert.AreEqual(EncodedGeometryType.TriangularMesh, result.Value);
    }

    [TestMethod]
    public void DracoDecoder_RejectsInvalidMagic()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsFalse(result.Ok);
        Assert.IsTrue(result.Status.ErrorMessage.ToLower().Contains("magic"));
    }

    [TestMethod]
    public void DracoDecoder_RejectsUnsupportedVersion()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[10];
        data[0] = (byte)'D';
        data[1] = (byte)'R';
        data[2] = (byte)'A';
        data[3] = (byte)'C';
        data[4] = (byte)'O';
        data[5] = 99;
        data[6] = 99;
        
        buffer.Init(data);

        var result = DracoDecoder.GetEncodedGeometryType(buffer);
        
        Assert.IsFalse(result.Ok);
        Assert.AreEqual(StatusCode.UnsupportedVersion, result.Status.Code);
    }

    private byte[] CreateMinimalDracoHeader()
    {
        var header = new List<byte>();
        
        header.AddRange(System.Text.Encoding.ASCII.GetBytes("DRACO"));
        header.Add(2);
        header.Add(2);
        header.Add(1);
        header.Add(1);
        header.Add(0);
        header.Add(0);
        
        return header.ToArray();
    }

    [TestMethod]
    public void Mesh_ToFromAttributes_RoundTrip()
    {
        var mesh = new Mesh();
        mesh.NumPoints = 3;
        mesh.AddFace(new Face(0, 1, 2));

        var posAttr = new PointAttribute();
        posAttr.Init(GeometryAttributeType.Position, DataType.Float32, 3, 3);
        
        Span<float> v0 = stackalloc float[] { 0.0f, 0.0f, 0.0f };
        Span<float> v1 = stackalloc float[] { 1.0f, 0.0f, 0.0f };
        Span<float> v2 = stackalloc float[] { 0.0f, 1.0f, 0.0f };
        
        posAttr.SetValue(0, System.Runtime.InteropServices.MemoryMarshal.AsBytes(v0));
        posAttr.SetValue(1, System.Runtime.InteropServices.MemoryMarshal.AsBytes(v1));
        posAttr.SetValue(2, System.Runtime.InteropServices.MemoryMarshal.AsBytes(v2));
        
        mesh.AddAttribute(posAttr);

        var retrievedAttr = mesh.GetNamedAttribute(GeometryAttributeType.Position);
        Assert.IsNotNull(retrievedAttr);
        Assert.AreEqual(3, retrievedAttr.NumComponents);
        Assert.AreEqual(DataType.Float32, retrievedAttr.DataType);
        
        var vertexData = retrievedAttr.GetValue(0);
        var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(vertexData);
        Assert.AreEqual(0.0f, floats[0]);
        Assert.AreEqual(0.0f, floats[1]);
        Assert.AreEqual(0.0f, floats[2]);
    }
}
