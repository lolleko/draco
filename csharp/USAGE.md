# Building and Using the Draco C# Decoder

## Prerequisites

- .NET 8.0 SDK or later
- (Optional) Visual Studio 2022 or VS Code with C# extension

## Building the Library

```bash
cd csharp
dotnet build
```

## Running Tests

```bash
cd csharp
dotnet test
```

Expected output:
```
Passed!  - Failed:     0, Passed:    21, Skipped:     0, Total:    21
```

## Using the Library

### Create a Console Application

```bash
dotnet new console -n DracoExample
cd DracoExample
dotnet add reference ../Draco.Decoder/Draco.Decoder.csproj
```

### Example: Creating and Reading a Simple Mesh

```csharp
using Draco.Decoder;
using System;

// Create a simple triangle mesh
var mesh = new Mesh();
mesh.NumPoints = 3;

// Add a triangle face
mesh.AddFace(new Face(0, 1, 2));

// Add position attribute (3 vertices, 3 components each, float32)
var positionAttr = new PointAttribute();
positionAttr.Init(
    GeometryAttributeType.Position,
    DataType.Float32,
    numComponents: 3,
    numValues: 3
);

// Set vertex positions
Span<float> v0 = stackalloc float[] { 0.0f, 0.0f, 0.0f };
Span<float> v1 = stackalloc float[] { 1.0f, 0.0f, 0.0f };
Span<float> v2 = stackalloc float[] { 0.0f, 1.0f, 0.0f };

positionAttr.SetValue(0, MemoryMarshal.AsBytes(v0));
positionAttr.SetValue(1, MemoryMarshal.AsBytes(v1));
positionAttr.SetValue(2, MemoryMarshal.AsBytes(v2));

mesh.AddAttribute(positionAttr);

// Read back the mesh data
Console.WriteLine($"Mesh has {mesh.NumFaces} faces and {mesh.NumPoints} points");

var position = mesh.GetNamedAttribute(GeometryAttributeType.Position);
if (position != null)
{
    Console.WriteLine($"Position attribute: {position.NumComponents} components");
    
    for (int i = 0; i < mesh.NumPoints; i++)
    {
        var vertexData = position.GetValue(i);
        var floats = MemoryMarshal.Cast<byte, float>(vertexData);
        Console.WriteLine($"Vertex {i}: ({floats[0]}, {floats[1]}, {floats[2]})");
    }
}

// Get face indices
var indices = mesh.GetIndices();
Console.WriteLine($"Indices: [{string.Join(", ", indices)}]");
```

### Example: Checking a Draco File Header

```csharp
using Draco.Decoder;
using System;
using System.IO;

// Read a Draco file
byte[] data = File.ReadAllBytes("mesh.drc");

// Initialize decoder buffer
var buffer = new DecoderBuffer();
buffer.Init(data);

// Check geometry type
var geometryTypeResult = DracoDecoder.GetEncodedGeometryType(buffer);

if (geometryTypeResult.Ok)
{
    Console.WriteLine($"Geometry type: {geometryTypeResult.Value}");
    Console.WriteLine($"Bitstream version: {buffer.BitstreamVersion:X4}");
}
else
{
    Console.WriteLine($"Error: {geometryTypeResult.Status}");
}
```

### Example: Working with Attributes

```csharp
using Draco.Decoder;
using System;

var mesh = new Mesh();
mesh.NumPoints = 4;

// Add multiple attributes
var position = new PointAttribute();
position.Init(GeometryAttributeType.Position, DataType.Float32, 3, 4);
mesh.AddAttribute(position);

var normal = new PointAttribute();
normal.Init(GeometryAttributeType.Normal, DataType.Float32, 3, 4);
mesh.AddAttribute(normal);

var texCoord = new PointAttribute();
texCoord.Init(GeometryAttributeType.TexCoord, DataType.Float32, 2, 4);
mesh.AddAttribute(texCoord);

// Query attributes
Console.WriteLine($"Total attributes: {mesh.NumAttributes}");
Console.WriteLine($"Position attributes: {mesh.NumNamedAttributes(GeometryAttributeType.Position)}");
Console.WriteLine($"Normal attributes: {mesh.NumNamedAttributes(GeometryAttributeType.Normal)}");

// Get specific attribute
var posAttr = mesh.GetNamedAttribute(GeometryAttributeType.Position);
if (posAttr != null)
{
    Console.WriteLine($"Position: {posAttr.NumComponents} components, " +
                     $"{posAttr.DataType} type, " +
                     $"{posAttr.NumValues} values");
}
```

## API Reference

### Core Types

#### `EncodedGeometryType`
- `PointCloud` - Point cloud geometry
- `TriangularMesh` - Triangular mesh
- `Invalid` - Invalid/unknown type

#### `GeometryAttributeType`
- `Position` - Vertex positions
- `Normal` - Vertex normals
- `Color` - Vertex colors
- `TexCoord` - Texture coordinates
- `Generic` - Generic attribute data

#### `DataType`
- `Float32`, `Float64` - Floating point
- `Int8`, `Int16`, `Int32`, `Int64` - Signed integers
- `UInt8`, `UInt16`, `UInt32`, `UInt64` - Unsigned integers
- `Bool` - Boolean

### Main Classes

#### `DracoDecoder`
Main decoder class for Draco-compressed data.

**Methods:**
- `GetEncodedGeometryType(DecoderBuffer)` - Identify geometry type
- `DecodeMeshFromBuffer(DecoderBuffer)` - Decode triangular mesh
- `DecodePointCloudFromBuffer(DecoderBuffer)` - Decode point cloud

#### `Mesh : PointCloud`
Represents a triangular mesh.

**Properties:**
- `NumFaces` - Number of triangular faces
- `NumPoints` - Number of vertices (inherited)
- `NumAttributes` - Number of attributes (inherited)

**Methods:**
- `AddFace(Face)` - Add a face
- `GetFace(int)` - Get face by index
- `GetIndices()` - Get all face indices as array
- `GetNamedAttribute(GeometryAttributeType)` - Get attribute by type

#### `PointCloud`
Represents a point cloud.

**Properties:**
- `NumPoints` - Number of points
- `NumAttributes` - Number of attributes

**Methods:**
- `AddAttribute(PointAttribute)` - Add an attribute
- `GetAttribute(int)` - Get attribute by ID
- `GetNamedAttribute(GeometryAttributeType)` - Get attribute by type
- `NumNamedAttributes(GeometryAttributeType)` - Count attributes of type

#### `PointAttribute`
Stores attribute data for vertices/points.

**Properties:**
- `AttributeType` - Type (Position, Normal, etc.)
- `DataType` - Data type (Float32, Int32, etc.)
- `NumComponents` - Components per value (3 for XYZ, 2 for UV, etc.)
- `NumValues` - Total number of values

**Methods:**
- `Init(...)` - Initialize attribute storage
- `GetValue(int)` - Get value as ReadOnlySpan<byte>
- `SetValue(int, ReadOnlySpan<byte>)` - Set value from bytes

#### `DecoderBuffer`
Binary data buffer with decoding capabilities.

**Methods:**
- `Init(byte[])` - Initialize with data
- `Decode<T>(out T)` - Decode typed value
- `Decode(Span<byte>)` - Decode bytes
- `Peek<T>(out T)` - Peek without advancing
- `StartBitDecoding(bool, out ulong)` - Start bit-level decoding
- `DecodeLeastSignificantBits32(int, out uint)` - Decode bits
- `EndBitDecoding()` - End bit-level decoding

## Current Limitations

1. **Compression Algorithms Not Implemented**: The decoder can read headers but cannot decompress actual Draco-encoded meshes yet. See `IMPLEMENTATION_STATUS.md` for details.

2. **No glTF Integration**: glTF parsing and KHR_draco_mesh_compression support not yet implemented.

3. **Basic Format Only**: Only supports uncompressed/simple encoded data in current state.

## Next Steps

To use with real Draco files, you need to either:
1. Complete the compression algorithm port (see `IMPLEMENTATION_STATUS.md`)
2. Use a hybrid approach with P/Invoke to the C++ library
3. Use the existing Unity plugin as a reference

## Getting Help

- See `README.md` for overview
- See `IMPLEMENTATION_STATUS.md` for technical details
- Check unit tests in `Draco.Decoder.Tests/` for examples
- Review C++ implementation in `src/draco/` for reference
