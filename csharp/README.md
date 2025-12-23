# Draco Decoder for C#

This is a pure C# port of the Draco 3D geometry decoder focusing on decompression of glTF assets. **No native dependencies or P/Invoke wrappers** - this is a complete C# implementation.

## Overview

Draco is a library for compressing and decompressing 3D geometric meshes and point clouds. This C# implementation provides the decoding/decompression functionality needed to work with Draco-compressed glTF files, implemented entirely in modern C# with SIMD support.

## Project Structure

```
csharp/
├── Draco.Decoder/                      # Core decoder library
│   ├── DataType.cs                     # Data type enums
│   ├── DecoderBuffer.cs                # Binary data reading with bit-level access
│   ├── DracoDecoder.cs                 # Main decoder class
│   ├── EncodedGeometryType.cs          # Geometry type enums
│   ├── GeometryAttributeType.cs        # Attribute type enums
│   ├── Mesh.cs                         # Mesh data structure
│   ├── PointAttribute.cs               # Attribute data
│   ├── PointCloud.cs                   # Point cloud data structure
│   ├── Status.cs                       # Error handling types
│   ├── VarintDecoding.cs              # Variable-length integer decoding
│   ├── RAnsDecoder.cs                  # rANS entropy decoder
│   ├── PredictionScheme.cs             # Delta prediction schemes
│   └── AttributeTransform.cs           # Quantization and normal encoding
└── Draco.Decoder.Tests/                # Unit tests with MSTest
    ├── BasicTests.cs                   # Core functionality tests
    └── DecoderIntegrationTests.cs      # Integration tests
```

## Features

### Implemented ✅
- **Core data structures** (Mesh, PointCloud, Attributes)
- **DecoderBuffer** with bit-level decoding
- **Status/StatusOr** error handling pattern
- **Varint decoding** for variable-length integers
- **rANS decoder** for entropy coding
- **Delta prediction** for attribute compression
- **Quantization/Dequantization** for attribute values
- **Octahedron encoding** for normal vectors
- **Modern C# 8.0+** with no nullable annotations
- **System.Numerics.Vector3** for SIMD operations
- **21 passing unit tests** with MSTest

### In Progress ⏳
- Sequential attribute decoder integration
- Edgebreaker mesh connectivity decoder
- Integration of decompression into main decoder
- glTF file parsing and integration

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

All 21 tests pass successfully.

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
- **No `#region` directives** - clean, flat code organization
- **Nullable reference types disabled** for cleaner code
- **`System.Numerics` types** for vectorized operations (Vector3, etc.)
- **`Span<T>` and `Memory<T>`** for efficient zero-copy memory access
- **`stackalloc`** for small temporary buffers
- **Pure C# implementation** - no P/Invoke, no native dependencies

## Compression Algorithms Ported

### Entropy Coding
- ✅ **Variable-length integer (varint)** encoding/decoding
- ✅ **rANS (range Asymmetric Numeral Systems)** entropy decoder
- ⏳ Symbol decoder integration

### Attribute Compression
- ✅ **Quantization/Dequantization** - float to integer conversion
- ✅ **Delta prediction** - differential encoding
- ✅ **Octahedron encoding** - normal vector compression
- ⏳ Parallelogram prediction
- ⏳ Multi-parallelogram prediction
- ⏳ Wrap transforms

### Mesh Connectivity
- ⏳ Edgebreaker decoder (in progress)
- ⏳ Sequential decoder

## Current State

A foundational C# decoder with core compression algorithms:
- ✅ 21 passing unit tests
- ✅ Type system and data structures
- ✅ Bit-level decoding support
- ✅ Entropy coding (rANS)
- ✅ Attribute transforms
- ✅ Prediction schemes
- ⏳ Full integration (in progress)

## Next Steps

1. **Complete Attribute Decoder Integration**
   - Wire up sequential attribute decoder
   - Integrate quantization and prediction
   - Handle all attribute types

2. **Implement Mesh Connectivity Decoding**
   - Port edgebreaker traversal
   - Implement topology decoding
   - Add valence cache

3. **glTF Integration**
   - Parse glTF/GLB files
   - Extract KHR_draco_mesh_compression data
   - Decompress and rebuild glTF meshes
   - Write uncompressed glTF output

4. **Testing & Validation**
   - Test with cube_att.drc (301 bytes)
   - Test with larger meshes
   - Validate against C++ implementation output
   - Performance benchmarks

## Performance Optimization

The code is structured for modern C# optimization:
- Uses `Span<T>` for zero-copy operations
- `System.Numerics.Vector3` enables SIMD operations
- Aggressive inlining opportunities
- Stack allocation for small buffers
- Ready for `ArrayPool<T>` integration

## License

Licensed under the Apache License, Version 2.0. See the LICENSE file in the root directory.

## References

- [Draco GitHub](https://github.com/google/draco)
- [glTF KHR_draco_mesh_compression](https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_draco_mesh_compression)
- [rANS Paper](https://arxiv.org/abs/1311.2540)
