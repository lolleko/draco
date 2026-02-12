using DracoSharp.Attributes;
using DracoSharp.Compression.BitCoders;
using DracoSharp.Core;

namespace DracoSharp.Compression.PredictionSchemes;

public class MeshPredictionSchemeTexCoordsPortableDecoder : IPredictionSchemeDecoder
{
    private const int NumTexComponents = 2;

    private readonly PredictionSchemeDecodingTransform _transform;
    private readonly MeshPredictionSchemeData _meshData;
    private PointAttribute _posAttribute;
    private readonly int[] _predictedValue = new int[NumTexComponents];
    private List<bool> _orientations = [];

    public PredictionSchemeMethod PredictionMethod =>
        PredictionSchemeMethod.TexCoordsPortable;

    public PredictionSchemeTransformType TransformType =>
        _transform.TransformType;

    public bool AreCorrectionsPositive => _transform.AreCorrectionsPositive;
    public bool IsInitialized => _posAttribute != null && _meshData.IsInitialized;
    public int NumParentAttributes => 1;

    public MeshPredictionSchemeTexCoordsPortableDecoder(
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
        // Decode the delta coded orientations.
        // The portable decoder always uses raw int32, no version branching.
        if (!buffer.Decode(out int numOrientationsInt) || numOrientationsInt < 0)
            return false;
        uint numOrientations = (uint)numOrientationsInt;

        _orientations = new List<bool>((int)numOrientations);
        for (uint i = 0; i < numOrientations; i++)
            _orientations.Add(false);

        bool lastOrientation = true;
        var decoder = new RAnsBitDecoder();
        if (!decoder.StartDecoding(buffer))
            return false;
        for (uint i = 0; i < numOrientations; i++)
        {
            if (!decoder.DecodeNextBit())
                lastOrientation = !lastOrientation;
            _orientations[(int)i] = lastOrientation;
        }
        decoder.EndDecoding();

        // Call base transform data decoding.
        return _transform.DecodeTransformData(buffer);
    }

    public bool ComputeOriginalValues(
        Span<int> data, int size, int numComponents, int[] entryToPointIdMap)
    {
        if (numComponents != NumTexComponents)
            return false;

        _transform.Init(numComponents);

        // Reverse orientations so we can pop from the back.
        _orientations.Reverse();

        int cornerMapSize = _meshData.DataToCornerMap.Count;
        for (int p = 0; p < cornerMapSize; p++)
        {
            int cornerId = _meshData.DataToCornerMap[p];
            if (!ComputePredictedValue(cornerId, data, p, entryToPointIdMap))
                return false;

            int dstOffset = p * numComponents;
            _transform.ComputeOriginalValue(
                _predictedValue,
                data.Slice(dstOffset, numComponents),
                data.Slice(dstOffset, numComponents));
        }
        return true;
    }

    private bool ComputePredictedValue(
        int cornerId, Span<int> data, int dataId, int[] entryToPointIdMap)
    {
        var table = _meshData.CornerTable;
        var vertexToDataMap = _meshData.VertexToDataMap;

        int nextCornerId = table.Next(cornerId);
        int prevCornerId = table.Previous(cornerId);

        int nextVertId = table.Vertex(nextCornerId);
        int prevVertId = table.Vertex(prevCornerId);

        int nextDataId = vertexToDataMap[nextVertId];
        int prevDataId = vertexToDataMap[prevVertId];

        if (prevDataId < dataId && nextDataId < dataId)
        {
            // Both corners have available UV coordinates.
            long nU = data[nextDataId * NumTexComponents];
            long nV = data[nextDataId * NumTexComponents + 1];
            long pU = data[prevDataId * NumTexComponents];
            long pV = data[prevDataId * NumTexComponents + 1];

            if (pU == nU && pV == nV)
            {
                // Degenerate UV triangle.
                _predictedValue[0] = (int)pU;
                _predictedValue[1] = (int)pV;
                return true;
            }

            // Get positions at all corners.
            GetPositionForEntryId(dataId, entryToPointIdMap,
                out long tipX, out long tipY, out long tipZ);
            GetPositionForEntryId(nextDataId, entryToPointIdMap,
                out long nextX, out long nextY, out long nextZ);
            GetPositionForEntryId(prevDataId, entryToPointIdMap,
                out long prevX, out long prevY, out long prevZ);

            // pn = prev_pos - next_pos
            long pnX = prevX - nextX;
            long pnY = prevY - nextY;
            long pnZ = prevZ - nextZ;

            ulong pnNorm2Squared = (ulong)(pnX * pnX + pnY * pnY + pnZ * pnZ);
            if (pnNorm2Squared != 0)
            {
                // cn = tip_pos - next_pos
                long cnX = tipX - nextX;
                long cnY = tipY - nextY;
                long cnZ = tipZ - nextZ;

                long cnDotPn = cnX * pnX + cnY * pnY + cnZ * pnZ;

                long pnUvU = pU - nU;
                long pnUvV = pV - nV;

                // Overflow checks
                long nUvAbsMaxElement = Math.Max(Math.Abs(nU), Math.Abs(nV));
                if (nUvAbsMaxElement >
                    long.MaxValue / (long)pnNorm2Squared)
                    return false;

                long pnUvAbsMaxElement = Math.Max(Math.Abs(pnUvU), Math.Abs(pnUvV));
                if (pnUvAbsMaxElement != 0 &&
                    Math.Abs(cnDotPn) > long.MaxValue / pnUvAbsMaxElement)
                    return false;

                long xUvU = nU * (long)pnNorm2Squared + cnDotPn * pnUvU;
                long xUvV = nV * (long)pnNorm2Squared + cnDotPn * pnUvV;

                long pnAbsMaxElement = Math.Max(
                    Math.Max(Math.Abs(pnX), Math.Abs(pnY)), Math.Abs(pnZ));
                if (pnAbsMaxElement != 0 &&
                    Math.Abs(cnDotPn) > long.MaxValue / pnAbsMaxElement)
                    return false;

                // x_pos = next_pos + (cn_dot_pn * pn) / pn_norm2_squared
                long xPosX = nextX + cnDotPn * pnX / (long)pnNorm2Squared;
                long xPosY = nextY + cnDotPn * pnY / (long)pnNorm2Squared;
                long xPosZ = nextZ + cnDotPn * pnZ / (long)pnNorm2Squared;

                long cxX = tipX - xPosX;
                long cxY = tipY - xPosY;
                long cxZ = tipZ - xPosZ;
                ulong cxNorm2Squared = (ulong)(cxX * cxX + cxY * cxY + cxZ * cxZ);

                // Rotated pn_uv by 90 degrees.
                long cxUvU = pnUvV;
                long cxUvV = -pnUvU;

                ulong normSquared = IntSqrt(cxNorm2Squared * pnNorm2Squared);
                cxUvU = cxUvU * (long)normSquared;
                cxUvV = cxUvV * (long)normSquared;

                if (_orientations.Count == 0)
                    return false;

                bool orientation = _orientations[^1];
                _orientations.RemoveAt(_orientations.Count - 1);

                // Perform operations in unsigned type to match C++ behavior.
                long predU, predV;
                if (orientation)
                {
                    predU = (long)((ulong)xUvU + (ulong)cxUvU) / (long)pnNorm2Squared;
                    predV = (long)((ulong)xUvV + (ulong)cxUvV) / (long)pnNorm2Squared;
                }
                else
                {
                    predU = (long)((ulong)xUvU - (ulong)cxUvU) / (long)pnNorm2Squared;
                    predV = (long)((ulong)xUvV - (ulong)cxUvV) / (long)pnNorm2Squared;
                }

                _predictedValue[0] = (int)predU;
                _predictedValue[1] = (int)predV;
                return true;
            }
        }

        // Fallback to delta coding.
        int dataOffset = 0;
        if (prevDataId < dataId)
        {
            dataOffset = prevDataId * NumTexComponents;
        }
        if (nextDataId < dataId)
        {
            dataOffset = nextDataId * NumTexComponents;
        }
        else
        {
            if (dataId > 0)
            {
                dataOffset = (dataId - 1) * NumTexComponents;
            }
            else
            {
                _predictedValue[0] = 0;
                _predictedValue[1] = 0;
                return true;
            }
        }
        _predictedValue[0] = data[dataOffset];
        _predictedValue[1] = data[dataOffset + 1];
        return true;
    }

    private void GetPositionForEntryId(int entryId, int[] entryToPointIdMap,
        out long x, out long y, out long z)
    {
        int pointId = entryToPointIdMap[entryId];
        int mappedIndex = (int)_posAttribute.MappedIndex(pointId);
        int compSize = _posAttribute.DataType.ByteLength();
        int totalBytes = _posAttribute.NumComponents * compSize;
        Span<byte> posBuf = stackalloc byte[totalBytes];
        _posAttribute.GetValue(mappedIndex, posBuf);

        if (_posAttribute.DataType == DracoSharp.Core.DataType.Int32)
        {
            var ints = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(posBuf);
            x = ints[0];
            y = ints[1];
            z = ints[2];
        }
        else
        {
            // Float - convert to int64 for integer arithmetic.
            var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(posBuf);
            x = (long)floats[0];
            y = (long)floats[1];
            z = (long)floats[2];
        }
    }

    private static ulong IntSqrt(ulong number)
    {
        if (number == 0)
            return 0;
        // Initial estimate: find power-of-two approximation via log2(number).
        ulong actNumber = number;
        ulong squareRoot = 1;
        while (actNumber >= 2)
        {
            squareRoot *= 2;
            actNumber /= 4;
        }
        // Newton's method to find floor(sqrt(number)).
        do
        {
            squareRoot = (squareRoot + number / squareRoot) / 2;
        } while (squareRoot * squareRoot > number);
        return squareRoot;
    }
}
