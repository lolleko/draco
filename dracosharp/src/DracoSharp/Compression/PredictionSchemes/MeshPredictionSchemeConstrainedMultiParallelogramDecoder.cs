using DracoSharp.Attributes;
using DracoSharp.Compression.BitCoders;
using DracoSharp.Core;
using DracoSharp.Mesh;

namespace DracoSharp.Compression.PredictionSchemes;

public class MeshPredictionSchemeConstrainedMultiParallelogramDecoder : IPredictionSchemeDecoder
{
    private const int MaxNumParallelograms = 4;

    private readonly PredictionSchemeDecodingTransform _transform;
    private readonly MeshPredictionSchemeData _meshData;
    private readonly List<bool>[] _isCreaseEdge = new List<bool>[MaxNumParallelograms];

    public PredictionSchemeMethod PredictionMethod =>
        PredictionSchemeMethod.ConstrainedMultiParallelogram;

    public PredictionSchemeTransformType TransformType => _transform.TransformType;
    public bool AreCorrectionsPositive => _transform.AreCorrectionsPositive;
    public bool IsInitialized => _meshData.IsInitialized;
    public int NumParentAttributes => 0;

    public MeshPredictionSchemeConstrainedMultiParallelogramDecoder(
        PredictionSchemeDecodingTransform transform,
        MeshPredictionSchemeData meshData)
    {
        _transform = transform;
        _meshData = meshData;
        for (int i = 0; i < MaxNumParallelograms; i++)
            _isCreaseEdge[i] = [];
    }

    public GeometryAttribute.AttributeType GetParentAttributeType(int i) =>
        GeometryAttribute.AttributeType.Invalid;

    public bool SetParentAttribute(PointAttribute att) => false;

    public bool DecodePredictionData(DecoderBuffer buffer)
    {
        // For v < 2.2, decode and validate the prediction mode byte.
        if (buffer.BitstreamVersion < BitstreamVersion.Make(2, 2))
        {
            if (!buffer.Decode(out byte mode))
                return false;
            if (mode != 1) // OPTIMAL_MULTI_PARALLELOGRAM
                return false;
        }

        // Decode crease edge flags using separate rANS bit coder for each context.
        for (int i = 0; i < MaxNumParallelograms; i++)
        {
            if (!buffer.DecodeVarint(out uint numFlags))
                return false;
            if (numFlags > (uint)_meshData.CornerTable.NumCorners)
                return false;
            if (numFlags > 0)
            {
                _isCreaseEdge[i] = new List<bool>((int)numFlags);
                for (int j = 0; j < (int)numFlags; j++)
                    _isCreaseEdge[i].Add(false);

                var decoder = new RAnsBitDecoder();
                if (!decoder.StartDecoding(buffer))
                    return false;
                for (int j = 0; j < (int)numFlags; j++)
                    _isCreaseEdge[i][j] = decoder.DecodeNextBit();
                decoder.EndDecoding();
            }
            else
            {
                _isCreaseEdge[i] = [];
            }
        }

        return _transform.DecodeTransformData(buffer);
    }

    public bool ComputeOriginalValues(
        Span<int> data, int size, int numComponents, int[] entryToPointIdMap)
    {
        _transform.Init(numComponents);

        var table = _meshData.CornerTable;
        var vertexToDataMap = _meshData.VertexToDataMap;

        // Predicted values for all simple parallelograms at any given vertex.
        var predVals = new int[MaxNumParallelograms][];
        for (int i = 0; i < MaxNumParallelograms; i++)
            predVals[i] = new int[numComponents];

        // Restore the first value.
        Span<int> zeroPred = stackalloc int[numComponents];
        zeroPred.Clear();
        _transform.ComputeOriginalValue(zeroPred, data[..numComponents], data);

        // Current position in each crease edge context.
        int[] isCreaseEdgePos = new int[MaxNumParallelograms];

        // Used to store predicted value for multi-parallelogram prediction.
        int[] multiPredVals = new int[numComponents];

        int cornerMapSize = _meshData.DataToCornerMap.Count;
        for (int p = 1; p < cornerMapSize; p++)
        {
            int startCornerId = _meshData.DataToCornerMap[p];
            int cornerId = startCornerId;
            int numParallelograms = 0;
            bool firstPass = true;

            while (cornerId != CornerTable.kInvalidCornerIndex)
            {
                bool found = ComputeParallelogramPrediction(
                        p, cornerId, table, vertexToDataMap, data,
                        numComponents, predVals[numParallelograms]);
                if (found)
                {
                    numParallelograms++;
                    if (numParallelograms == MaxNumParallelograms)
                        break;
                }

                // Proceed to the next corner. Swing left first, then right.
                if (firstPass)
                {
                    cornerId = table.SwingLeft(cornerId);
                }
                else
                {
                    cornerId = table.SwingRight(cornerId);
                }
                if (cornerId == startCornerId)
                    break;
                if (cornerId == CornerTable.kInvalidCornerIndex && firstPass)
                {
                    firstPass = false;
                    cornerId = table.SwingRight(startCornerId);
                }
            }

            // Check which parallelograms are used via crease edge flags.
            int numUsedParallelograms = 0;
            if (numParallelograms > 0)
            {
                for (int i = 0; i < numComponents; i++)
                    multiPredVals[i] = 0;

                for (int i = 0; i < numParallelograms; i++)
                {
                    int context = numParallelograms - 1;
                    int pos = isCreaseEdgePos[context]++;
                    if (pos >= _isCreaseEdge[context].Count)
                        return false;
                    bool isCrease = _isCreaseEdge[context][pos];
                    if (!isCrease)
                    {
                        numUsedParallelograms++;
                        for (int j = 0; j < numComponents; j++)
                            multiPredVals[j] = (int)((uint)multiPredVals[j] + (uint)predVals[i][j]);
                    }
                }
            }

            int dstOffset = p * numComponents;
            if (numUsedParallelograms == 0)
            {
                // No parallelogram valid. Use previous decoded point.
                int srcOffset = (p - 1) * numComponents;
                _transform.ComputeOriginalValue(
                    data.Slice(srcOffset, numComponents),
                    data.Slice(dstOffset, numComponents),
                    data.Slice(dstOffset, numComponents));
            }
            else
            {
                for (int c = 0; c < numComponents; c++)
                    multiPredVals[c] /= numUsedParallelograms;
                _transform.ComputeOriginalValue(
                    multiPredVals,
                    data.Slice(dstOffset, numComponents),
                    data.Slice(dstOffset, numComponents));
            }
        }
        return true;
    }

    private static bool ComputeParallelogramPrediction(
        int dataEntryId, int cornerId, ICornerTable table,
        int[] vertexToDataMap, Span<int> data,
        int numComponents, int[] outPrediction)
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
