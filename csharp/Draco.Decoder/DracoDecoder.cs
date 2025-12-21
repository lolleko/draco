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

namespace Draco.Decoder;

public class DracoDecoder
{
    private const string DracoMagic = "DRACO";
    private const byte MajorVersion = 2;
    private const byte MinorVersion = 2;

    public static StatusOr<EncodedGeometryType> GetEncodedGeometryType(DecoderBuffer buffer)
    {
        if (buffer.RemainingSize < 5)
            return Status.IoError("Buffer too small to contain Draco magic");

        Span<byte> magic = stackalloc byte[5];
        if (!buffer.Decode(magic))
            return Status.IoError("Failed to read magic");

        if (!magic.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(DracoMagic)))
        {
            buffer.StartDecodingFrom(0);
            return Status.IoError("Invalid Draco magic");
        }

        if (!buffer.Decode(out byte major) || !buffer.Decode(out byte minor))
            return Status.IoError("Failed to read version");

        if (major > MajorVersion || (major == MajorVersion && minor > MinorVersion))
            return Status.UnsupportedVersion($"Unsupported version {major}.{minor}");

        buffer.set_bitstreamVersion((ushort)((major << 8) | minor));

        if (!buffer.Decode(out byte encoderType))
            return Status.IoError("Failed to read encoder type");

        if (!buffer.Decode(out byte encoderMethod))
            return Status.IoError("Failed to read encoder method");

        if (!buffer.Decode(out ushort flags))
            return Status.IoError("Failed to read flags");

        EncodedGeometryType geometryType = encoderType switch
        {
            0 => EncodedGeometryType.PointCloud,
            1 => EncodedGeometryType.TriangularMesh,
            _ => EncodedGeometryType.Invalid
        };

        return geometryType;
    }

    public StatusOr<PointCloud> DecodePointCloudFromBuffer(DecoderBuffer buffer)
    {
        var geometryTypeResult = GetEncodedGeometryType(buffer);
        if (!geometryTypeResult.Ok)
            return geometryTypeResult.Status;

        var geometryType = geometryTypeResult.Value;
        if (geometryType != EncodedGeometryType.PointCloud && 
            geometryType != EncodedGeometryType.TriangularMesh)
            return Status.DracoError($"Invalid geometry type: {geometryType}");

        var pointCloud = new PointCloud();
        var status = DecodePointCloudInternal(buffer, pointCloud);
        if (!status.Ok)
            return status;

        return pointCloud;
    }

    public StatusOr<Mesh> DecodeMeshFromBuffer(DecoderBuffer buffer)
    {
        var geometryTypeResult = GetEncodedGeometryType(buffer);
        if (!geometryTypeResult.Ok)
            return geometryTypeResult.Status;

        var geometryType = geometryTypeResult.Value;
        if (geometryType != EncodedGeometryType.TriangularMesh)
            return Status.DracoError("Input data is not a triangular mesh");

        var mesh = new Mesh();
        var status = DecodeMeshInternal(buffer, mesh);
        if (!status.Ok)
            return status;

        return mesh;
    }

    private Status DecodePointCloudInternal(DecoderBuffer buffer, PointCloud pointCloud)
    {
        if (!buffer.Decode(out uint numPoints))
            return Status.IoError("Failed to read number of points");

        pointCloud.NumPoints = (int)numPoints;

        if (!buffer.Decode(out byte numAttributes))
            return Status.IoError("Failed to read number of attributes");

        for (int i = 0; i < numAttributes; i++)
        {
            var attribute = new PointAttribute();
            
            if (!buffer.Decode(out byte attributeType))
                return Status.IoError($"Failed to read attribute type for attribute {i}");
            
            if (!buffer.Decode(out byte dataType))
                return Status.IoError($"Failed to read data type for attribute {i}");
            
            if (!buffer.Decode(out byte numComponents))
                return Status.IoError($"Failed to read num components for attribute {i}");

            attribute.AttributeType = (GeometryAttributeType)attributeType;
            attribute.DataType = (DataType)dataType;
            attribute.NumComponents = numComponents;
            attribute.UniqueId = i;

            attribute.Init(attribute.AttributeType, attribute.DataType, 
                          attribute.NumComponents, pointCloud.NumPoints);

            pointCloud.AddAttribute(attribute);
        }

        return DecodeAttributeData(buffer, pointCloud);
    }

    private Status DecodeMeshInternal(DecoderBuffer buffer, Mesh mesh)
    {
        var status = DecodePointCloudInternal(buffer, mesh);
        if (!status.Ok)
            return status;

        if (!buffer.Decode(out uint numFaces))
            return Status.IoError("Failed to read number of faces");

        mesh.SetNumFaces((int)numFaces);

        return DecodeFaceData(buffer, mesh);
    }

    private Status DecodeAttributeData(DecoderBuffer buffer, PointCloud pointCloud)
    {
        for (int attId = 0; attId < pointCloud.NumAttributes; attId++)
        {
            var attribute = pointCloud.GetAttribute(attId);
            if (attribute == null)
                continue;

            int valueSize = attribute.ValueSize;
            int numValues = pointCloud.NumPoints;
            Span<byte> data = attribute.Data;

            for (int i = 0; i < numValues; i++)
            {
                int offset = i * valueSize;
                if (!buffer.Decode(data.Slice(offset, valueSize)))
                    return Status.IoError($"Failed to read attribute {attId} value {i}");
            }
        }

        return Status.OkStatus();
    }

    private Status DecodeFaceData(DecoderBuffer buffer, Mesh mesh)
    {
        for (int i = 0; i < mesh.NumFaces; i++)
        {
            if (!buffer.Decode(out uint v0) || 
                !buffer.Decode(out uint v1) || 
                !buffer.Decode(out uint v2))
                return Status.IoError($"Failed to read face {i}");

            mesh.SetFace(i, new Face((int)v0, (int)v1, (int)v2));
        }

        return Status.OkStatus();
    }
}
