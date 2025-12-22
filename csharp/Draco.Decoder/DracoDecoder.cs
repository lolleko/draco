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
    private const byte MinorVersion = 3;

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
        // Check geometry type on a copy of the buffer
        var tempBuffer = new DecoderBuffer();
        tempBuffer.Init(buffer.GetData(), buffer.BitstreamVersion);
        
        var geometryTypeResult = GetEncodedGeometryType(tempBuffer);
        if (!geometryTypeResult.Ok)
            return geometryTypeResult.Status;

        var geometryType = geometryTypeResult.Value;
        if (geometryType != EncodedGeometryType.PointCloud && 
            geometryType != EncodedGeometryType.TriangularMesh)
            return Status.DracoError($"Invalid geometry type: {geometryType}");

        // Now decode with the original buffer (header will be read again)
        var pointCloud = new PointCloud();
        var status = DecodePointCloudInternal(buffer, pointCloud);
        if (!status.Ok)
            return status;

        return pointCloud;
    }

    public StatusOr<Mesh> DecodeMeshFromBuffer(DecoderBuffer buffer)
    {
        // Check geometry type on a copy of the buffer
        var tempBuffer = new DecoderBuffer();
        tempBuffer.Init(buffer.GetData(), buffer.BitstreamVersion);
        
        var geometryTypeResult = GetEncodedGeometryType(tempBuffer);
        if (!geometryTypeResult.Ok)
            return geometryTypeResult.Status;

        var geometryType = geometryTypeResult.Value;
        if (geometryType != EncodedGeometryType.TriangularMesh)
            return Status.DracoError("Input data is not a triangular mesh");

        // Now decode with the original buffer (header will be read again)
        var mesh = new Mesh();
        var status = DecodeMeshInternal(buffer, mesh);
        if (!status.Ok)
            return status;

        return mesh;
    }

    private Status DecodePointCloudInternal(DecoderBuffer buffer, PointCloud pointCloud)
    {
        // Read header first
        var geometryTypeResult = GetEncodedGeometryType(buffer);
        if (!geometryTypeResult.Ok)
            return geometryTypeResult.Status;
        
        // For point clouds, read number of points (DecodeGeometryData)
        if (!buffer.Decode(out uint numPoints))
            return Status.IoError("Failed to read number of points");

        pointCloud.NumPoints = (int)numPoints;
        
        Console.WriteLine($"[DecodePointCloudInternal] numPoints={numPoints}, buffer position: {buffer.DecodedSize}");
        
        // Decode attributes (point cloud sequential decoder creates one attributes decoder)
        var status = DecodeAttributeData(buffer, pointCloud);
        if (!status.Ok)
            return status;
        
        return Status.OkStatus();
    }

    private Status DecodeMeshInternal(DecoderBuffer buffer, Mesh mesh)
    {
        // Read header first
        var geometryTypeResult = GetEncodedGeometryType(buffer);
        if (!geometryTypeResult.Ok)
            return geometryTypeResult.Status;
        
        // Check encoder method:
        // 0 = MESH_SEQUENTIAL_ENCODING (supported)
        // 1 = MESH_EDGEBREAKER_ENCODING (not yet implemented)
        byte encoderMethod = buffer.GetData()[8]; // encoder_method is at byte 8 in header
        if (encoderMethod != 0)
        {
            return Status.DracoError($"Unsupported mesh encoder method: {encoderMethod}. Only sequential encoding (0) is currently supported. Edgebreaker encoding (1) requires additional implementation.");
        }
        
        // For meshes with sequential encoding, decode connectivity first (reads numFaces, numPoints and face indices)
        // This matches C++ mesh_sequential_decoder.cc DecodeConnectivity()
        var meshDecoder = new SequentialMeshDecoder(mesh, buffer, buffer.BitstreamVersion);
        var connectivityResult = meshDecoder.DecodeConnectivity();
        if (!connectivityResult.Ok)
            return connectivityResult.Status;

        Console.WriteLine($"[DecodeMeshInternal] After connectivity: numPoints={mesh.NumPoints}, numFaces={mesh.NumFaces}, buffer position: {buffer.DecodedSize}");

        // For mesh, PointCloudDecoder::DecodeGeometryData() is a no-op (just returns true)
        // So we go straight to DecodePointAttributes which reads num_attributes_decoders
        
        if (!buffer.Decode(out byte numAttributesDecoders))
            return Status.IoError("Failed to read number of attributes decoders");
        
        Console.WriteLine($"[DecodeMeshInternal] numAttributesDecoders={numAttributesDecoders}, buffer position: {buffer.DecodedSize}");
        
        // Support multiple attribute decoders
        // Each decoder handles a set of attributes
        for (int i = 0; i < numAttributesDecoders; i++)
        {
            var status = DecodeAttributeData(buffer, mesh);
            if (!status.Ok)
                return status;
        }
        
        return Status.OkStatus();
    }

    private Status DecodeAttributeData(DecoderBuffer buffer, PointCloud pointCloud)
    {
        Console.WriteLine($"[DecodeAttributeData] Starting, buffer position: {buffer.DecodedSize}");
        
        // First, read attribute metadata (this is DecodeAttributesDecoderData in C++)
        // For version < 2.0, this is a byte, not uint32
        uint numAttributes;
        if (buffer.BitstreamVersion < 0x0200)
        {
            if (!buffer.Decode(out byte numAttrByte))
                return Status.IoError("Failed to read number of attributes (byte)");
            numAttributes = numAttrByte;
        }
        else
        {
            if (!VarintDecoding.DecodeVarint(buffer, out numAttributes))
                return Status.IoError("Failed to read number of attributes (varint)");
        }
        
        Console.WriteLine($"[DecodeAttributeData] numAttributes={numAttributes}, buffer position: {buffer.DecodedSize}");
        
        if (numAttributes == 0 || numAttributes > 100)
            return Status.IoError($"Invalid number of attributes: {numAttributes}");

        // Read attribute descriptors and create attributes
        for (int i = 0; i < numAttributes; i++)
        {
            var attribute = new PointAttribute();
            
            if (!buffer.Decode(out byte attributeType))
                return Status.IoError($"Failed to read attribute type for attribute {i}");
            
            if (!buffer.Decode(out byte dataType))
                return Status.IoError($"Failed to read data type for attribute {i}");
            
            if (!buffer.Decode(out byte numComponents))
                return Status.IoError($"Failed to read num components for attribute {i}");
            
            if (!buffer.Decode(out byte normalized))
                return Status.IoError($"Failed to read normalized for attribute {i}");
            
            // Read custom_id/unique_id based on version
            uint customId = (uint)i;
            if (buffer.BitstreamVersion < 0x0103)
            {
                // For bitstream versions < 1.3, read custom_id as uint16
                if (!buffer.Decode(out ushort customId16))
                    return Status.IoError($"Failed to read custom_id for attribute {i}");
                customId = customId16;
            }
            else
            {
                // For versions >= 1.3, read unique_id as varint
                if (!VarintDecoding.DecodeVarint(buffer, out customId))
                    return Status.IoError($"Failed to read unique_id for attribute {i}");
            }

            Console.WriteLine($"[DecodeAttributeData] Attribute {i}: type={attributeType}, dataType={dataType}, numComponents={numComponents}, normalized={normalized}, customId={customId}, buffer position: {buffer.DecodedSize}");

            attribute.AttributeType = (GeometryAttributeType)attributeType;
            attribute.DataType = (DataType)dataType;
            attribute.NumComponents = numComponents;
            attribute.UniqueId = (int)customId;

            attribute.Init(attribute.AttributeType, attribute.DataType, 
                          attribute.NumComponents, pointCloud.NumPoints);

            pointCloud.AddAttribute(attribute);
        }
        
        // Phase 1: Read all encoder types and create decoders
        var decoders = new SequentialAttributeDecoder[pointCloud.NumAttributes];
        for (int attId = 0; attId < pointCloud.NumAttributes; attId++)
        {
            var attribute = pointCloud.GetAttribute(attId);
            if (attribute == null)
                return Status.IoError($"Attribute {attId} is null");

            if (!buffer.Decode(out byte encoderType))
                return Status.IoError($"Failed to read encoder type for attribute {attId}");
            
            Console.WriteLine($"[DecodeAttributeData] Attribute {attId}, encoderType={encoderType}, buffer position: {buffer.DecodedSize}");

            SequentialAttributeDecoder decoder;
            
            switch (encoderType)
            {
                case 0: // SEQUENTIAL_ATTRIBUTE_ENCODER_GENERIC
                    decoder = new SequentialAttributeDecoder();
                    break;
                case 1: // SEQUENTIAL_ATTRIBUTE_ENCODER_INTEGER
                    decoder = new SequentialIntegerAttributeDecoder();
                    break;
                case 2: // SEQUENTIAL_ATTRIBUTE_ENCODER_QUANTIZATION
                    decoder = new SequentialQuantizationAttributeDecoder();
                    break;
                case 3: // SEQUENTIAL_ATTRIBUTE_ENCODER_NORMALS
                    decoder = new SequentialNormalAttributeDecoder();
                    break;
                case 6: // KD_TREE (uses quantization decoder for sequential decoding)
                    decoder = new SequentialQuantizationAttributeDecoder();
                    break;
                case 14: // Another KD-Tree variant  
                    decoder = new SequentialQuantizationAttributeDecoder();
                    break;
                default:
                    return Status.DracoError($"Unsupported encoder type: {encoderType}");
            }

            decoder.SetAttribute(attribute);
            decoders[attId] = decoder;
        }
        
        // Phase 2: Decode portable attributes for all decoders
        for (int attId = 0; attId < pointCloud.NumAttributes; attId++)
        {
            Console.WriteLine($"[DecodeAttributeData] Decoding portable attribute {attId}, buffer position: {buffer.DecodedSize}");
            
            if (!decoders[attId].DecodePortableAttribute(pointCloud.NumPoints, buffer))
            {
                Console.WriteLine($"[DecodeAttributeData] DecodePortableAttribute failed at buffer position {buffer.DecodedSize}");
                return Status.IoError($"Failed to decode portable attribute {attId}");
            }
        }
        
        // Phase 3: Decode transform data for all decoders
        for (int attId = 0; attId < pointCloud.NumAttributes; attId++)
        {
            Console.WriteLine($"[DecodeAttributeData] Decoding transform data for attribute {attId}, buffer position: {buffer.DecodedSize}");
            
            if (!decoders[attId].DecodeDataNeededByPortableTransform(buffer))
            {
                Console.WriteLine($"[DecodeAttributeData] DecodeDataNeededByPortableTransform failed");
                return Status.IoError($"Failed to decode transform data for attribute {attId}");
            }
        }
        
        // Phase 4: Transform all attributes to original format
        for (int attId = 0; attId < pointCloud.NumAttributes; attId++)
        {
            Console.WriteLine($"[DecodeAttributeData] Transforming attribute {attId}");
            
            if (!decoders[attId].TransformAttributeToOriginalFormat(pointCloud.NumPoints))
            {
                Console.WriteLine($"[DecodeAttributeData] TransformAttributeToOriginalFormat failed");
                return Status.IoError($"Failed to transform attribute {attId}");
            }
            
            Console.WriteLine($"[DecodeAttributeData] Successfully decoded attribute {attId}, buffer position: {buffer.DecodedSize}");
        }

        return Status.OkStatus();
    }
}
