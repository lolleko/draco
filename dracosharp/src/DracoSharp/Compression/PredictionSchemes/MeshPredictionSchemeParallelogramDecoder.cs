using DracoSharp.Attributes;
using DracoSharp.Core;
using DracoSharp.Mesh;

namespace DracoSharp.Compression.PredictionSchemes;

public class MeshPredictionSchemeParallelogramDecoder : IPredictionSchemeDecoder
{
    private readonly PredictionSchemeDecodingTransform _transform;
    private readonly MeshPredictionSchemeData _meshData;

    public PredictionSchemeMethod PredictionMethod => PredictionSchemeMethod.Parallelogram;
    public PredictionSchemeTransformType TransformType => _transform.TransformType;
    public bool AreCorrectionsPositive => _transform.AreCorrectionsPositive;
    public bool IsInitialized => _meshData.IsInitialized;
    public int NumParentAttributes => 0;

    public MeshPredictionSchemeParallelogramDecoder(
        PredictionSchemeDecodingTransform transform,
        MeshPredictionSchemeData meshData)
    {
        _transform = transform;
        _meshData = meshData;
    }

    public GeometryAttribute.AttributeType GetParentAttributeType(int i) =>
        GeometryAttribute.AttributeType.Invalid;

    public bool SetParentAttribute(PointAttribute att) => false;

    public bool DecodePredictionData(DecoderBuffer buffer) =>
        _transform.DecodeTransformData(buffer);

    public bool ComputeOriginalValues(
        Span<int> data, int size, int numComponents, int[] entryToPointIdMap)
    {
        _transform.Init(numComponents);

        var table = _meshData.CornerTable;
        var vertexToDataMap = _meshData.VertexToDataMap;

        Span<int> predVals = stackalloc int[numComponents];
        predVals.Clear();

        // Restore the first value.
        _transform.ComputeOriginalValue(predVals, data[..numComponents], data);

        int cornerMapSize = _meshData.DataToCornerMap.Count;
        for (int p = 1; p < cornerMapSize; p++)
        {
            int cornerId = _meshData.DataToCornerMap[p];
            int dstOffset = p * numComponents;

            if (!ComputeParallelogramPrediction(
                    p, cornerId, table, vertexToDataMap, data,
                    numComponents, predVals))
            {
                // Parallelogram could not be computed. Fall back to delta coding.
                int srcOffset = (p - 1) * numComponents;
                _transform.ComputeOriginalValue(
                    data.Slice(srcOffset, numComponents),
                    data.Slice(dstOffset, numComponents),
                    data.Slice(dstOffset, numComponents));
            }
            else
            {
                _transform.ComputeOriginalValue(
                    predVals,
                    data.Slice(dstOffset, numComponents),
                    data.Slice(dstOffset, numComponents));
            }
        }
        return true;
    }

    private static bool ComputeParallelogramPrediction(
        int dataEntryId, int cornerId, ICornerTable table,
        int[] vertexToDataMap, Span<int> data,
        int numComponents, Span<int> outPrediction)
    {
        int oppCornerId = table.Opposite(cornerId);
        if (oppCornerId < 0)
            return false;

        int vertOpp = vertexToDataMap[table.Vertex(oppCornerId)];
        int vertNext = vertexToDataMap[table.Vertex(table.Next(oppCornerId))];
        int vertPrev = vertexToDataMap[table.Vertex(table.Previous(oppCornerId))];

        if (vertOpp < dataEntryId && vertNext < dataEntryId &&
            vertPrev < dataEntryId)
        {
            int vOppOff = vertOpp * numComponents;
            int vNextOff = vertNext * numComponents;
            int vPrevOff = vertPrev * numComponents;
            for (int c = 0; c < numComponents; c++)
            {
                long next = data[vNextOff + c];
                long prev = data[vPrevOff + c];
                long opp = data[vOppOff + c];
                outPrediction[c] = (int)(next + prev - opp);
            }
            return true;
        }
        return false;
    }
}
