# Draco Decoder for C#

This is a C# port of the Draco 3D geometry decoder focusing on decompression of glTF assets.

## Overview

Draco is a library for compressing and decompressing 3D geometric meshes and point clouds. This C# implementation provides the decoding/decompression functionality needed to work with Draco-compressed glTF files.

## Project Structure

```
csharp/
├── Draco.Decoder/           # Core decoder library
│   ├── DataType.cs          # Data type enums
│   ├── DecoderBuffer.cs     # Binary data reading with bit-level access
│   ├── DracoDecoder.cs      # Main decoder class
│   ├── EncodedGeometryType.cs
│   ├── GeometryAttributeType.cs
│   ├── Mesh.cs              # Mesh data structure
│   ├── PointAttribute.cs    # Attribute data
│   ├── PointCloud.cs        # Point cloud data structure
│   └── Status.cs            # Error handling types
└── Draco.Decoder.Tests/     # Unit tests
    └── BasicTests.cs
```

## Features

### Implemented
- Core data structures (Mesh, PointCloud, Attributes)
- DecoderBuffer with bit-level decoding
- Status/StatusOr error handling pattern
- Modern C# 8.0+ with no nullable annotations
- Ready for System.Numerics vectorization

### In Progress
- Full Draco bitstream decoding
- Attribute decompression algorithms
- glTF integration for KHR_draco_mesh_compression

## Building

```bash
cd csharp
dotnet build
```

## Running Tests

```bash
cd csharp
dotnet test
```

## Usage Example

```csharp
using Draco.Decoder;

// Load compressed data
byte[] compressedData = File.ReadAllBytes("mesh.drc");

// Create decoder
var decoder = new DracoDecoder();
var buffer = new DecoderBuffer();
buffer.Init(compressedData);

// Decode mesh
var result = decoder.DecodeMeshFromBuffer(buffer);
if (result.Ok)
{
    var mesh = result.Value;
    Console.WriteLine($"Decoded mesh with {mesh.NumFaces} faces and {mesh.NumPoints} points");
    
    // Access position attribute
    var posAttr = mesh.GetNamedAttribute(GeometryAttributeType.Position);
    if (posAttr != null)
    {
        // Process vertex data...
    }
}
else
{
    Console.WriteLine($"Decoding failed: {result.Status}");
}
```

## Architecture Notes

This implementation follows modern C# practices:
- No `#region` directives
- Nullable reference types disabled for cleaner code
- Uses `System.Numerics` types where appropriate
- Span<T> and Memory<T> for efficient memory access
- ValueTask for async operations (future)

## Current State

The basic infrastructure is in place with:
- ✅ 17 passing unit tests
- ✅ Type system and data structures
- ✅ Bit-level decoding support
- ⏳ Full Draco compression scheme decoding (in progress)

## Next Steps

1. Implement core decompression algorithms:
   - Attribute quantization/dequantization
   - Prediction schemes (delta, parallelogram, etc.)
   - Entropy decoding (rANS)
   - Mesh connectivity decoding (edgebreaker)

2. glTF Integration:
   - Parse glTF/GLB files
   - Extract KHR_draco_mesh_compression data
   - Decompress and rebuild glTF meshes
   - Write uncompressed glTF output

3. Testing:
   - Add integration tests with real .drc files
   - Test with glTF files from testdata/
   - Performance benchmarks

## License

Licensed under the Apache License, Version 2.0. See the LICENSE file in the root directory.

## References

- [Draco GitHub](https://github.com/google/draco)
- [glTF KHR_draco_mesh_compression](https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_draco_mesh_compression)
