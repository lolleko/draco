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

public class DataTypeTests
{
    [Fact]
    public void DataType_GetSize_ReturnsCorrectSizes()
    {
        Assert.Equal(1, DataType.Int8.GetSize());
        Assert.Equal(1, DataType.UInt8.GetSize());
        Assert.Equal(2, DataType.Int16.GetSize());
        Assert.Equal(2, DataType.UInt16.GetSize());
        Assert.Equal(4, DataType.Int32.GetSize());
        Assert.Equal(4, DataType.UInt32.GetSize());
        Assert.Equal(4, DataType.Float32.GetSize());
        Assert.Equal(8, DataType.Int64.GetSize());
        Assert.Equal(8, DataType.UInt64.GetSize());
        Assert.Equal(8, DataType.Float64.GetSize());
        Assert.Equal(1, DataType.Bool.GetSize());
        Assert.Equal(0, DataType.Invalid.GetSize());
    }
}

public class StatusTests
{
    [Fact]
    public void Status_OkStatus_IsOk()
    {
        var status = Status.OkStatus();
        Assert.True(status.Ok);
        Assert.Equal(StatusCode.Ok, status.Code);
    }

    [Fact]
    public void Status_Error_IsNotOk()
    {
        var status = Status.DracoError("Test error");
        Assert.False(status.Ok);
        Assert.Equal(StatusCode.DracoError, status.Code);
        Assert.Equal("Test error", status.ErrorMessage);
    }

    [Fact]
    public void StatusOr_WithValue_CanGetValue()
    {
        var statusOr = StatusOr<int>.FromValue(42);
        Assert.True(statusOr.Ok);
        Assert.Equal(42, statusOr.Value);
    }

    [Fact]
    public void StatusOr_WithError_ThrowsOnValueAccess()
    {
        var statusOr = StatusOr<int>.FromStatus(Status.DracoError("Test"));
        Assert.False(statusOr.Ok);
        Assert.Throws<InvalidOperationException>(() => statusOr.Value);
    }
}

public class DecoderBufferTests
{
    [Fact]
    public void DecoderBuffer_Init_SetsDataCorrectly()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Init(data);

        Assert.Equal(5, buffer.RemainingSize);
        Assert.Equal(0, buffer.DecodedSize);
    }

    [Fact]
    public void DecoderBuffer_DecodeUInt32_DecodesCorrectly()
    {
        var buffer = new DecoderBuffer();
        var data = BitConverter.GetBytes(0x12345678u);
        buffer.Init(data);

        Assert.True(buffer.Decode(out uint value));
        Assert.Equal(0x12345678u, value);
        Assert.Equal(4, buffer.DecodedSize);
        Assert.Equal(0, buffer.RemainingSize);
    }

    [Fact]
    public void DecoderBuffer_DecodeBytes_DecodesCorrectly()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        buffer.Init(data);

        Span<byte> output = stackalloc byte[3];
        Assert.True(buffer.Decode(output));
        Assert.Equal(1, output[0]);
        Assert.Equal(2, output[1]);
        Assert.Equal(3, output[2]);
        Assert.Equal(3, buffer.DecodedSize);
    }

    [Fact]
    public void DecoderBuffer_Peek_DoesNotAdvancePosition()
    {
        var buffer = new DecoderBuffer();
        var data = BitConverter.GetBytes(0x12345678u);
        buffer.Init(data);

        Assert.True(buffer.Peek(out uint value));
        Assert.Equal(0x12345678u, value);
        Assert.Equal(0, buffer.DecodedSize);
        Assert.Equal(4, buffer.RemainingSize);
    }

    [Fact]
    public void DecoderBuffer_Advance_MovesPosition()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[10];
        buffer.Init(data);

        buffer.Advance(5);
        Assert.Equal(5, buffer.DecodedSize);
        Assert.Equal(5, buffer.RemainingSize);
    }

    [Fact]
    public void DecoderBuffer_BitDecoding_Works()
    {
        var buffer = new DecoderBuffer();
        var data = new byte[] { 0b10110101, 0b11001010 };
        buffer.Init(data);

        Assert.True(buffer.StartBitDecoding(false, out _));
        Assert.True(buffer.BitDecoderActive);

        Assert.True(buffer.DecodeLeastSignificantBits32(4, out uint value1));
        Assert.Equal(0b0101u, value1);

        Assert.True(buffer.DecodeLeastSignificantBits32(4, out uint value2));
        Assert.Equal(0b1011u, value2);

        buffer.EndBitDecoding();
        Assert.False(buffer.BitDecoderActive);
        Assert.Equal(1, buffer.DecodedSize);
    }
}

public class PointCloudTests
{
    [Fact]
    public void PointCloud_AddAttribute_IncreasesCount()
    {
        var pc = new PointCloud();
        Assert.Equal(0, pc.NumAttributes);

        var attr = new PointAttribute
        {
            AttributeType = GeometryAttributeType.Position,
            DataType = DataType.Float32,
            NumComponents = 3
        };
        pc.AddAttribute(attr);

        Assert.Equal(1, pc.NumAttributes);
    }

    [Fact]
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
        Assert.NotNull(retrieved);
        Assert.Equal(GeometryAttributeType.Position, retrieved.AttributeType);
    }

    [Fact]
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
        Assert.NotNull(retrieved);
        Assert.Equal(GeometryAttributeType.Position, retrieved.AttributeType);
    }
}

public class MeshTests
{
    [Fact]
    public void Mesh_AddFace_IncreasesCount()
    {
        var mesh = new Mesh();
        Assert.Equal(0, mesh.NumFaces);

        mesh.AddFace(new Face(0, 1, 2));
        Assert.Equal(1, mesh.NumFaces);
    }

    [Fact]
    public void Mesh_GetFace_ReturnsCorrectFace()
    {
        var mesh = new Mesh();
        var face = new Face(10, 20, 30);
        mesh.AddFace(face);

        var retrieved = mesh.GetFace(0);
        Assert.Equal(10, retrieved.V0);
        Assert.Equal(20, retrieved.V1);
        Assert.Equal(30, retrieved.V2);
    }

    [Fact]
    public void Mesh_GetIndices_ReturnsCorrectArray()
    {
        var mesh = new Mesh();
        mesh.AddFace(new Face(0, 1, 2));
        mesh.AddFace(new Face(2, 3, 4));

        var indices = mesh.GetIndices();
        Assert.Equal(6, indices.Length);
        Assert.Equal(new[] { 0, 1, 2, 2, 3, 4 }, indices);
    }
}
