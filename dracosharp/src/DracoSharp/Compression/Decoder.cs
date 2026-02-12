using DracoSharp.Compression.Mesh;
using DracoSharp.Core;
using MeshType = DracoSharp.Mesh.Mesh;

namespace DracoSharp.Compression;

public static class Decoder
{
    public static EncodedGeometryType GetEncodedGeometryType(ReadOnlySpan<byte> data)
    {
        var buffer = new DecoderBuffer();
        buffer.Init(data);
        if (!PointCloudDecoder.DecodeHeader(buffer, out var header))
            throw new DracoException("Failed to parse Draco header.");
        if (header.EncoderType >= (byte)EncodedGeometryType.NumEncodedGeometryTypes)
            throw new DracoException("Unsupported geometry type.");
        return (EncodedGeometryType)header.EncoderType;
    }

    public static MeshType DecodeMesh(ReadOnlySpan<byte> data)
    {
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        // Peek at header to determine encoder method.
        var tempBuffer = buffer.Clone();
        if (!PointCloudDecoder.DecodeHeader(tempBuffer, out var header))
            throw new DracoException("Failed to parse Draco header.");

        if (header.EncoderType != (byte)EncodedGeometryType.TriangularMesh)
            throw new DracoException("Input is not a mesh.");

        MeshDecoder decoder = CreateMeshDecoder(header.EncoderMethod);
        var mesh = new MeshType();
        if (!decoder.Decode(buffer, mesh))
            throw new DracoException("Failed to decode mesh.");
        return mesh;
    }

    public static PointCloud.PointCloud DecodePointCloud(ReadOnlySpan<byte> data)
    {
        var buffer = new DecoderBuffer();
        buffer.Init(data);

        var tempBuffer = buffer.Clone();
        if (!PointCloudDecoder.DecodeHeader(tempBuffer, out var header))
            throw new DracoException("Failed to parse Draco header.");

        var geometryType = (EncodedGeometryType)header.EncoderType;

        if (geometryType == EncodedGeometryType.TriangularMesh)
        {
            // Meshes can be decoded as point clouds, just use the mesh decoder.
            return DecodeMesh(data);
        }

        PointCloudDecoder decoder = CreatePointCloudDecoder(header.EncoderMethod);
        var pointCloud = new PointCloud.PointCloud();
        if (!decoder.Decode(buffer, pointCloud))
            throw new DracoException("Failed to decode point cloud.");
        return pointCloud;
    }

    private static MeshDecoder CreateMeshDecoder(byte method) =>
        (MeshEncoderMethod)method switch
        {
            MeshEncoderMethod.SequentialEncoding => new MeshSequentialDecoder(),
            MeshEncoderMethod.EdgebreakerEncoding => new MeshEdgebreakerDecoder(),
            _ => throw new DracoException($"Unsupported mesh encoding method: {method}")
        };

    private static PointCloudDecoder CreatePointCloudDecoder(byte method) =>
        (PointCloudEncodingMethod)method switch
        {
            PointCloudEncodingMethod.SequentialEncoding => new PointCloudSequentialDecoder(),
            _ => throw new DracoException($"Unsupported point cloud encoding method: {method}")
        };
}
