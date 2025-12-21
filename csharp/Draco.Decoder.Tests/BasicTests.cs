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
public class DataTypeTests
{
    [TestMethod]
    public void DataType_GetSize_ReturnsCorrectSizes()
    {
        Assert.AreEqual(1, DataType.Int8.GetSize());
        Assert.AreEqual(1, DataType.UInt8.GetSize());
        Assert.AreEqual(2, DataType.Int16.GetSize());
        Assert.AreEqual(2, DataType.UInt16.GetSize());
        Assert.AreEqual(4, DataType.Int32.GetSize());
        Assert.AreEqual(4, DataType.UInt32.GetSize());
        Assert.AreEqual(4, DataType.Float32.GetSize());
        Assert.AreEqual(8, DataType.Int64.GetSize());
        Assert.AreEqual(8, DataType.UInt64.GetSize());
        Assert.AreEqual(8, DataType.Float64.GetSize());
        Assert.AreEqual(1, DataType.Bool.GetSize());
        Assert.AreEqual(0, DataType.Invalid.GetSize());
    }
}

[TestClass]
public class StatusTests
{
    [TestMethod]
    public void Status_OkStatus_IsOk()
    {
        var status = Status.OkStatus();
        Assert.IsTrue(status.Ok);
        Assert.AreEqual(StatusCode.Ok, status.Code);
    }

    [TestMethod]
    public void Status_Error_IsNotOk()
    {
        var status = Status.DracoError("Test error");
        Assert.IsFalse(status.Ok);
        Assert.AreEqual(StatusCode.DracoError, status.Code);
        Assert.AreEqual("Test error", status.ErrorMessage);
    }

    [TestMethod]
    public void StatusOr_WithValue_CanGetValue()
    {
        var statusOr = StatusOr<int>.FromValue(42);
        Assert.IsTrue(statusOr.Ok);
        Assert.AreEqual(42, statusOr.Value);
    }

    [TestMethod]
    public void StatusOr_WithError_ThrowsOnValueAccess()
    {
        var statusOr = StatusOr<int>.FromStatus(Status.DracoError("Test"));
        Assert.IsFalse(statusOr.Ok);
        try
        {
            var _ = statusOr.Value;
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }
}

[TestClass]
public class DecoderBufferTests
{
    [TestMethod]
    public void DecoderBuffer_Init_SetsDataCorrectly()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Init(data);

        Assert.AreEqual(5, buffer.RemainingSize);
        Assert.AreEqual(0, buffer.DecodedSize);
    }

    [TestMethod]
    public void DecoderBuffer_DecodeUInt32_DecodesCorrectly()
    {
        var buffer = new DecoderBuffer();
        var data = BitConverter.GetBytes(0x12345678u);
        buffer.Init(data);

        Assert.IsTrue(buffer.Decode(out uint value));
        Assert.AreEqual(0x12345678u, value);
        Assert.AreEqual(4, buffer.DecodedSize);
        Assert.AreEqual(0, buffer.RemainingSize);
    }

    [TestMethod]
    public void DecoderBuffer_DecodeBytes_DecodesCorrectly()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Init(data);

        Span<byte> output = stackalloc byte[3];
        Assert.IsTrue(buffer.Decode(output));
        Assert.AreEqual((byte)1, output[0]);
        Assert.AreEqual((byte)2, output[1]);
        Assert.AreEqual((byte)3, output[2]);
        Assert.AreEqual(3, buffer.DecodedSize);
    }

    [TestMethod]
    public void DecoderBuffer_Peek_DoesNotAdvancePosition()
    {
        var buffer = new DecoderBuffer();
        var data = BitConverter.GetBytes(0x12345678u);
        buffer.Init(data);

        Assert.IsTrue(buffer.Peek(out uint value));
        Assert.AreEqual(0x12345678u, value);
        Assert.AreEqual(0, buffer.DecodedSize);
        Assert.AreEqual(4, buffer.RemainingSize);
    }

    [TestMethod]
    public void DecoderBuffer_Advance_MovesPosition()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[10];
        buffer.Init(data);

        buffer.Advance(5);
        Assert.AreEqual(5, buffer.DecodedSize);
        Assert.AreEqual(5, buffer.RemainingSize);
    }

    [TestMethod]
    public void DecoderBuffer_BitDecoding_Works()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 0b10110101, 0b11001010 };
        buffer.Init(data);

        Assert.IsTrue(buffer.StartBitDecoding(false, out _));
        Assert.IsTrue(buffer.BitDecoderActive);

        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(4, out uint value1));
        Assert.AreEqual(0b0101u, value1);

        Assert.IsTrue(buffer.DecodeLeastSignificantBits32(4, out uint value2));
        Assert.AreEqual(0b1011u, value2);

        buffer.EndBitDecoding();
        Assert.IsFalse(buffer.BitDecoderActive);
        Assert.AreEqual(1, buffer.DecodedSize);
    }
}

[TestClass]
public class PointCloudTests
{
    [TestMethod]
    public void PointCloud_AddAttribute_IncreasesCount()
    {
        var pc = new PointCloud();
        Assert.AreEqual(0, pc.NumAttributes);

        var attr = new PointAttribute
        {
            AttributeType = GeometryAttributeType.Position,
            DataType = DataType.Float32,
            NumComponents = 3
        };
        pc.AddAttribute(attr);

        Assert.AreEqual(1, pc.NumAttributes);
    }

    [TestMethod]
    public void PointCloud_GetAttribute_ReturnsCorrectAttribute()
    {
        var pc = new PointCloud();
        var attr = new PointAttribute
        {
            AttributeType = GeometryAttributeType.Position,
            DataType = DataType.Float32,
            NumComponents = 3
        };
        int id = pc.AddAttribute(attr);

        var retrieved = pc.GetAttribute(id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(GeometryAttributeType.Position, retrieved.AttributeType);
    }

    [TestMethod]
    public void PointCloud_GetNamedAttribute_ReturnsCorrectAttribute()
    {
        var pc = new PointCloud();
        var posAttr = new PointAttribute
        {
            AttributeType = GeometryAttributeType.Position,
            DataType = DataType.Float32,
            NumComponents = 3
        };
        pc.AddAttribute(posAttr);

        var retrieved = pc.GetNamedAttribute(GeometryAttributeType.Position);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(GeometryAttributeType.Position, retrieved.AttributeType);
    }
}

[TestClass]
public class MeshTests
{
    [TestMethod]
    public void Mesh_AddFace_IncreasesCount()
    {
        var mesh = new Mesh();
        Assert.AreEqual(0, mesh.NumFaces);

        mesh.AddFace(new Face(0, 1, 2));
        Assert.AreEqual(1, mesh.NumFaces);
    }

    [TestMethod]
    public void Mesh_GetFace_ReturnsCorrectFace()
    {
        var mesh = new Mesh();
        var face = new Face(10, 20, 30);
        mesh.AddFace(face);

        var retrieved = mesh.GetFace(0);
        Assert.AreEqual(10, retrieved.V0);
        Assert.AreEqual(20, retrieved.V1);
        Assert.AreEqual(30, retrieved.V2);
    }

    [TestMethod]
    public void Mesh_GetIndices_ReturnsCorrectArray()
    {
        var mesh = new Mesh();
        mesh.AddFace(new Face(0, 1, 2));
        mesh.AddFace(new Face(2, 3, 4));

        var indices = mesh.GetIndices();
        Assert.AreEqual(6, indices.Length);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 4 }, indices);
    }
}
