using System.Buffers.Binary;
using DracoSharp.Attributes;
using DracoSharp.Compression.Entropy;
using DracoSharp.Compression.PredictionSchemes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class SequentialIntegerAttributeDecoder : SequentialAttributeDecoder
{
    private IPredictionSchemeDecoder _predictionScheme;

    public override bool Init(PointCloudDecoder decoder, int attributeId)
    {
        if (!base.Init(decoder, attributeId))
            return false;
        return true;
    }

    public override bool TransformAttributeToOriginalFormat(int[] pointIds)
    {
        if (Decoder != null &&
            Decoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
        {
            return true; // For v < 2.0, StoreValues was already called in DecodeValues.
        }
        return StoreValues((uint)pointIds.Length);
    }

    public override bool DecodeValues(int[] pointIds, DecoderBuffer buffer)
    {
        // Decode prediction scheme method and transform type.
        if (!buffer.Decode(out byte predictionSchemeMethodByte))
            return false;
        var predictionSchemeMethod = (PredictionSchemeMethod)(sbyte)predictionSchemeMethodByte;

        if (predictionSchemeMethod < PredictionSchemeMethod.None ||
            predictionSchemeMethod >= PredictionSchemeMethod.NumSchemes)
            return false;

        if (predictionSchemeMethod != PredictionSchemeMethod.None)
        {
            if (!buffer.Decode(out byte predictionTransformTypeByte))
                return false;
            var transformType = (PredictionSchemeTransformType)(sbyte)predictionTransformTypeByte;

            if (transformType < PredictionSchemeTransformType.None ||
                transformType >= PredictionSchemeTransformType.NumTypes)
                return false;

            _predictionScheme = CreatePredictionSchemeOverride(predictionSchemeMethod, transformType)
                ?? CreatePredictionScheme(predictionSchemeMethod, transformType);
            if (_predictionScheme != null && !InitPredictionScheme(_predictionScheme))
                _predictionScheme = null;
        }

        if (!DecodeIntegerValues(pointIds, buffer))
            return false;

        // For v < 2.0, revert the transform right after we decode the data.
        if (Decoder != null &&
            Decoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
        {
            if (!StoreValues((uint)pointIds.Length))
                return false;
        }

        return true;
    }

    protected virtual int GetNumValueComponents() => Attribute.NumComponents;

    protected virtual bool DecodeIntegerValues(int[] pointIds, DecoderBuffer buffer)
    {
        int numComponents = GetNumValueComponents();
        if (numComponents <= 0)
            return false;
        int numEntries = pointIds.Length;
        int numValues = numEntries * numComponents;

        PreparePortableAttribute(numEntries, numComponents);
        var portableData = GetPortableAttributeIntSpan();
        if (portableData.Length == 0)
            return false;

        // Read the compressed flag byte.
        if (!buffer.Decode(out byte compressed))
            return false;

        if (compressed > 0)
        {
            // Decode compressed values using symbol decoding.
            if (!SymbolDecoding.DecodeSymbols(
                    (uint)numValues, numComponents, buffer,
                    System.Runtime.InteropServices.MemoryMarshal.Cast<int, uint>(portableData)))
                return false;
        }
        else
        {
            // Decode the integer data directly (uncompressed).
            if (!buffer.Decode(out byte numBytes))
                return false;
            if (numBytes == 4)
            {
                // Read raw int32 data.
                var rawBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(portableData.Slice(0, numValues));
                if (!buffer.Decode(rawBytes))
                    return false;
            }
            else
            {
                // Read smaller data types and zero-extend to int32.
                for (int i = 0; i < numValues; i++)
                {
                    portableData[i] = 0;
                    Span<byte> dest = System.Runtime.InteropServices.MemoryMarshal.AsBytes(portableData.Slice(i, 1));
                    if (!buffer.Decode(dest.Slice(0, numBytes)))
                        return false;
                }
            }
        }

        if (numValues > 0 && (_predictionScheme == null || !_predictionScheme.AreCorrectionsPositive))
        {
            // Convert unsigned symbols to signed ints.
            BitUtils.ConvertSymbolsToSignedInts(
                System.Runtime.InteropServices.MemoryMarshal.Cast<int, uint>(portableData.Slice(0, numValues)),
                portableData.Slice(0, numValues));
        }

        // If the data was encoded with a prediction scheme, revert it.
        if (_predictionScheme != null)
        {
            if (!_predictionScheme.DecodePredictionData(buffer))
                return false;

            if (numValues > 0)
            {
                if (!_predictionScheme.ComputeOriginalValues(
                        portableData, numValues, numComponents, pointIds))
                    return false;
            }
        }

        return true;
    }

    protected void PreparePortableAttribute(int numValues, int numComponents)
    {
        if (PortableAttribute != null && PortableAttribute.Size == numValues &&
            PortableAttribute.NumComponents == numComponents)
            return;
        var pa = new PointAttribute();
        pa.Init(Attribute.Type, (byte)numComponents, DataType.Int32, false, numValues);
        pa.SetIdentityMapping();
        pa.UniqueId = Attribute.UniqueId;
        PortableAttribute = pa;
    }

    protected virtual bool StoreValues(uint numValues)
    {
        switch (Attribute.DataType)
        {
            case DataType.UInt8:
                StoreTypedValues<byte>(numValues);
                break;
            case DataType.Int8:
                StoreTypedValues<sbyte>(numValues);
                break;
            case DataType.UInt16:
                StoreTypedValues<ushort>(numValues);
                break;
            case DataType.Int16:
                StoreTypedValues<short>(numValues);
                break;
            case DataType.UInt32:
                StoreTypedValues<uint>(numValues);
                break;
            case DataType.Int32:
                StoreTypedValues<int>(numValues);
                break;
            default:
                return false;
        }
        return true;
    }

    private void StoreTypedValues<T>(uint numValues) where T : struct
    {
        int numComponents = Attribute.NumComponents;
        var intSpan = GetPortableAttributeIntSpan();
        int typeSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        int entrySize = numComponents * typeSize;
        int outBytePos = 0;
        Span<byte> valueBytes = stackalloc byte[entrySize];

        for (uint i = 0; i < numValues; i++)
        {
            for (int c = 0; c < numComponents; c++)
            {
                int intVal = intSpan[(int)(i * numComponents + c)];
                WriteConvertedValue(valueBytes, c, intVal, typeSize);
            }
            Attribute.Buffer.Write(outBytePos, valueBytes);
            outBytePos += entrySize;
        }
    }

    private static void WriteConvertedValue(Span<byte> dest, int componentIndex, int intVal, int typeSize)
    {
        switch (typeSize)
        {
            case 1:
                dest[componentIndex] = (byte)intVal;
                break;
            case 2:
                BinaryPrimitives.WriteInt16LittleEndian(dest.Slice(componentIndex * 2), (short)intVal);
                break;
            case 4:
                BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(componentIndex * 4), intVal);
                break;
        }
    }

    protected Span<int> GetPortableAttributeIntSpan()
    {
        var buffer = PortableAttribute.Buffer;
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(buffer.MutableData);
    }

    protected virtual IPredictionSchemeDecoder CreatePredictionSchemeOverride(
        PredictionSchemeMethod method, PredictionSchemeTransformType transformType) => null;

    private IPredictionSchemeDecoder CreatePredictionScheme(
        PredictionSchemeMethod method, PredictionSchemeTransformType transformType)
    {
        if (transformType != PredictionSchemeTransformType.Wrap)
            return null; // Only Wrap transform is supported for integer attributes.

        var transform = new PredictionSchemeWrapDecodingTransform();

        // Try to create a mesh prediction scheme if we're decoding a mesh.
        if (Decoder is MeshDecoder meshDecoder)
        {
            var meshScheme = CreateMeshPredictionScheme(method, transform, meshDecoder);
            if (meshScheme != null)
                return meshScheme;
        }

        return method switch
        {
            PredictionSchemeMethod.Difference => new PredictionSchemeDeltaDecoder(transform),
            _ => new PredictionSchemeDeltaDecoder(transform)
        };
    }

    private IPredictionSchemeDecoder CreateMeshPredictionScheme(
        PredictionSchemeMethod method, PredictionSchemeDecodingTransform transform,
        MeshDecoder meshDecoder)
    {
        if (method != PredictionSchemeMethod.Parallelogram &&
            method != PredictionSchemeMethod.ConstrainedMultiParallelogram &&
            method != PredictionSchemeMethod.TexCoordsPortable &&
            method != PredictionSchemeMethod.GeometricNormal)
            return null;

        var cornerTable = meshDecoder.GetCornerTable();
        var encodingData = meshDecoder.GetAttributeEncodingData(AttributeId);
        if (cornerTable == null || encodingData == null)
            return null;

        var meshData = new MeshPredictionSchemeData();
        var attCornerTable = meshDecoder.GetAttributeCornerTable(AttributeId);
        if (attCornerTable != null)
        {
            meshData.Set(attCornerTable,
                encodingData.EncodedAttributeValueIndexToCornerMap,
                encodingData.VertexToEncodedAttributeValueIndexMap);
        }
        else
        {
            meshData.Set(cornerTable,
                encodingData.EncodedAttributeValueIndexToCornerMap,
                encodingData.VertexToEncodedAttributeValueIndexMap);
        }

        return method switch
        {
            PredictionSchemeMethod.Parallelogram =>
                new MeshPredictionSchemeParallelogramDecoder(transform, meshData),
            PredictionSchemeMethod.ConstrainedMultiParallelogram =>
                new MeshPredictionSchemeConstrainedMultiParallelogramDecoder(transform, meshData),
            PredictionSchemeMethod.TexCoordsPortable =>
                new MeshPredictionSchemeTexCoordsPortableDecoder(transform, meshData),
            _ => null
        };
    }

    private bool InitPredictionScheme(IPredictionSchemeDecoder ps)
    {
        for (int i = 0; i < ps.NumParentAttributes; i++)
        {
            int attId = Decoder.PointCloud.GetNamedAttributeId(
                ps.GetParentAttributeType(i));
            if (attId < 0)
                return false;

            if (Decoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
            {
                if (!ps.SetParentAttribute(Decoder.PointCloud.Attribute(attId)))
                    return false;
            }
            else
            {
                var pa = Decoder.GetPortableAttribute(attId);
                if (pa == null || !ps.SetParentAttribute(pa))
                    return false;
            }
        }
        return true;
    }
}
