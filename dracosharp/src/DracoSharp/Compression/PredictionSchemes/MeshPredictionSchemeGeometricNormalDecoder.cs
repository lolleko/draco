using DracoSharp.Attributes;
using DracoSharp.Compression.BitCoders;
using DracoSharp.Core;

namespace DracoSharp.Compression.PredictionSchemes;

public class MeshPredictionSchemeGeometricNormalDecoder : IPredictionSchemeDecoder
{
    private readonly PredictionSchemeDecodingTransform _transform;
    private readonly MeshPredictionSchemeData _meshData;
    private PointAttribute _posAttribute;
    private int[] _entryToPointIdMap;
    private readonly OctahedronToolBox _octahedronToolBox = new();
    private readonly RAnsBitDecoder _flipNormalBitDecoder = new();

    public PredictionSchemeMethod PredictionMethod =>
        PredictionSchemeMethod.GeometricNormal;

    public PredictionSchemeTransformType TransformType =>
        _transform.TransformType;

    public bool AreCorrectionsPositive => _transform.AreCorrectionsPositive;

    public bool IsInitialized =>
        _posAttribute != null && _meshData.IsInitialized && _octahedronToolBox.IsInitialized;

    public int NumParentAttributes => 1;

    public MeshPredictionSchemeGeometricNormalDecoder(
        PredictionSchemeDecodingTransform transform,
        MeshPredictionSchemeData meshData)
    {
        _transform = transform;
        _meshData = meshData;
    }

    public GeometryAttribute.AttributeType GetParentAttributeType(int i) =>
        GeometryAttribute.AttributeType.Position;

    public bool SetParentAttribute(PointAttribute att)
    {
        if (att == null || att.Type != GeometryAttribute.AttributeType.Position)
            return false;
        if (att.NumComponents != 3)
            return false;
        _posAttribute = att;
        return true;
    }

    public bool DecodePredictionData(DecoderBuffer buffer)
    {
        // Decode transform data first (contains quantization bits).
        if (!_transform.DecodeTransformData(buffer))
            return false;

        // For v < 2.2, decode the prediction mode byte.
        if (buffer.BitstreamVersion < BitstreamVersion.Make(2, 2))
        {
            if (!buffer.Decode(out byte predictionMode))
                return false;
            if (predictionMode > 1) // TRIANGLE_AREA = 1
                return false;
        }

        // Init normal flips.
        if (!_flipNormalBitDecoder.StartDecoding(buffer))
            return false;

        return true;
    }

    public bool ComputeOriginalValues(
        Span<int> data, int size, int numComponents, int[] entryToPointIdMap)
    {
        if (numComponents != 2)
            return false;

        _entryToPointIdMap = entryToPointIdMap;

        // Get quantization bits from the transform.
        int quantBits = GetQuantizationBitsFromTransform();
        if (!_octahedronToolBox.SetQuantizationBits(quantBits))
            return false;

        _transform.Init(numComponents);

        int cornerMapSize = _meshData.DataToCornerMap.Count;
        Span<int> predNormal3D = stackalloc int[3];
        Span<int> predNormalOct = stackalloc int[2];

        for (int dataId = 0; dataId < cornerMapSize; dataId++)
        {
            int cornerId = _meshData.DataToCornerMap[dataId];
            ComputePredictedNormal(cornerId, predNormal3D);

            // Canonicalize and convert to octahedral coords.
            _octahedronToolBox.CanonicalizeIntegerVector(predNormal3D);

            if (_flipNormalBitDecoder.DecodeNextBit())
            {
                predNormal3D[0] = -predNormal3D[0];
                predNormal3D[1] = -predNormal3D[1];
                predNormal3D[2] = -predNormal3D[2];
            }

            _octahedronToolBox.IntegerVectorToQuantizedOctahedralCoords(
                predNormal3D, out predNormalOct[0], out predNormalOct[1]);

            int dataOffset = dataId * 2;
            _transform.ComputeOriginalValue(
                predNormalOct,
                data.Slice(dataOffset, 2),
                data.Slice(dataOffset, 2));
        }

        _flipNormalBitDecoder.EndDecoding();
        return true;
    }

    private void ComputePredictedNormal(int cornerId, Span<int> prediction)
    {
        var table = _meshData.CornerTable;
        var vertexToDataMap = _meshData.VertexToDataMap;

        // Get position of central vertex.
        GetPositionForCorner(cornerId, out long centX, out long centY, out long centZ);

        // Iterate over all corners sharing this vertex to compute area-weighted normal.
        long normalX = 0, normalY = 0, normalZ = 0;

        int startCorner = cornerId;
        int currentCorner = cornerId;
        bool firstPass = true;

        while (firstPass || currentCorner != startCorner)
        {
            firstPass = false;

            int nextCorner = table.Next(currentCorner);
            int prevCorner = table.Previous(currentCorner);

            GetPositionForCorner(nextCorner, out long nextX, out long nextY, out long nextZ);
            GetPositionForCorner(prevCorner, out long prevX, out long prevY, out long prevZ);

            // Delta vectors.
            long dnX = nextX - centX, dnY = nextY - centY, dnZ = nextZ - centZ;
            long dpX = prevX - centX, dpY = prevY - centY, dpZ = prevZ - centZ;

            // Cross product (unsigned addition to prevent signed overflow, matching C++).
            normalX = (long)((ulong)normalX + (ulong)(dnY * dpZ - dnZ * dpY));
            normalY = (long)((ulong)normalY + (ulong)(dnZ * dpX - dnX * dpZ));
            normalZ = (long)((ulong)normalZ + (ulong)(dnX * dpY - dnY * dpX));

            // Swing right to the next corner around the same vertex.
            currentCorner = table.SwingRight(currentCorner);
            if (currentCorner < 0)
                break; // Boundary reached.
        }

        // If we hit a boundary on the right side, also walk left from the start.
        if (currentCorner < 0)
        {
            currentCorner = table.SwingLeft(startCorner);
            while (currentCorner >= 0)
            {
                int nextCorner = table.Next(currentCorner);
                int prevCorner = table.Previous(currentCorner);

                GetPositionForCorner(nextCorner, out long nextX, out long nextY, out long nextZ);
                GetPositionForCorner(prevCorner, out long prevX, out long prevY, out long prevZ);

                long dnX = nextX - centX, dnY = nextY - centY, dnZ = nextZ - centZ;
                long dpX = prevX - centX, dpY = prevY - centY, dpZ = prevZ - centZ;

                normalX = (long)((ulong)normalX + (ulong)(dnY * dpZ - dnZ * dpY));
                normalY = (long)((ulong)normalY + (ulong)(dnZ * dpX - dnX * dpZ));
                normalZ = (long)((ulong)normalZ + (ulong)(dnX * dpY - dnY * dpX));

                currentCorner = table.SwingLeft(currentCorner);
            }
        }

        // Scale down to prevent overflow in subsequent operations.
        const long upperBound = 1 << 29;
        long absSum = Math.Abs(normalX) + Math.Abs(normalY) + Math.Abs(normalZ);
        if (absSum > upperBound)
        {
            long quotient = absSum / upperBound;
            normalX /= quotient;
            normalY /= quotient;
            normalZ /= quotient;
        }

        prediction[0] = (int)normalX;
        prediction[1] = (int)normalY;
        prediction[2] = (int)normalZ;
    }

    private void GetPositionForCorner(int cornerId, out long x, out long y, out long z)
    {
        var table = _meshData.CornerTable;
        int vertId = table.Vertex(cornerId);
        int dataId = _meshData.VertexToDataMap[vertId];
        GetPositionForDataId(dataId, out x, out y, out z);
    }

    private void GetPositionForDataId(int dataId, out long x, out long y, out long z)
    {
        int pointId = _entryToPointIdMap[dataId];
        int mappedIndex = (int)_posAttribute.MappedIndex(pointId);
        int compSize = _posAttribute.DataType.ByteLength();
        int totalBytes = _posAttribute.NumComponents * compSize;
        Span<byte> posBuf = stackalloc byte[totalBytes];
        _posAttribute.GetValue(mappedIndex, posBuf);

        if (_posAttribute.DataType == DataType.Int32)
        {
            var ints = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(posBuf);
            x = ints[0];
            y = ints[1];
            z = ints[2];
        }
        else
        {
            var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(posBuf);
            x = (long)floats[0];
            y = (long)floats[1];
            z = (long)floats[2];
        }
    }

    private int GetQuantizationBitsFromTransform()
    {
        // The NormalOctahedronCanonicalized transform stores quantization bits.
        if (_transform is PredictionSchemeNormalOctahedronCanonicalizedDecodingTransform normTransform)
            return normTransform.QuantizationBits;
        return -1;
    }
}
