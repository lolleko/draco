using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace DracoSharp.Core;

public enum DataType
{
    Invalid = 0,
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float32,
    Float64,
    Bool,
    TypesCount
}

public static class DataTypeExtensions
{
    public static int ByteLength(this DataType dt) => dt switch
    {
        DataType.Int8 or DataType.UInt8 or DataType.Bool => 1,
        DataType.Int16 or DataType.UInt16 => 2,
        DataType.Int32 or DataType.UInt32 or DataType.Float32 => 4,
        DataType.Int64 or DataType.UInt64 or DataType.Float64 => 8,
        _ => -1
    };

    public static bool IsIntegral(this DataType dt) => dt switch
    {
        DataType.Int8 or DataType.UInt8 or
        DataType.Int16 or DataType.UInt16 or
        DataType.Int32 or DataType.UInt32 or
        DataType.Int64 or DataType.UInt64 or
        DataType.Bool => true,
        _ => false
    };
}

public struct DracoHeader
{
    public byte VersionMajor;
    public byte VersionMinor;
    public byte EncoderType;
    public byte EncoderMethod;
    public ushort Flags;

    public bool HasMetadata => (Flags & MetadataFlagMask) != 0;

    public const ushort MetadataFlagMask = 0x8000;
}

public static class BitstreamVersion
{
    public const byte PointCloudMajor = 2;
    public const byte PointCloudMinor = 3;
    public const byte MeshMajor = 2;
    public const byte MeshMinor = 2;

    public const ushort PointCloud = (PointCloudMajor << 8) | PointCloudMinor;
    public const ushort Mesh = (MeshMajor << 8) | MeshMinor;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Make(byte major, byte minor) => (ushort)((major << 8) | minor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Major(ushort version) => (byte)(version >> 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Minor(ushort version) => (byte)(version & 0xFF);
}
