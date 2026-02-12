namespace DracoSharp.Compression.Mesh.Traverser;

public class MaxPredictionDegreeTraverser : ITraverser
{
    private const int kInvalidCornerIndex = -1;
    private const int kInvalidVertexIndex = -1;
    private const int kInvalidFaceIndex = -1;
    private const int kMaxPriority = 3;

    private global::DracoSharp.Mesh.ICornerTable _cornerTable;
    private MeshAttributeIndicesEncodingObserver _observer;
    private bool[] _isFaceVisited = [];
    private bool[] _isVertexVisited = [];
    private int[] _predictionDegree = [];
    private readonly List<int>[] _traversalStacks = new List<int>[kMaxPriority];
    private int _bestPriority;

    public MaxPredictionDegreeTraverser()
    {
        for (int i = 0; i < kMaxPriority; i++)
            _traversalStacks[i] = [];
    }

    public void Init(global::DracoSharp.Mesh.ICornerTable cornerTable,
        MeshAttributeIndicesEncodingObserver observer)
    {
        _cornerTable = cornerTable;
        _observer = observer;
        _isFaceVisited = new bool[cornerTable.NumFaces];
        _isVertexVisited = new bool[cornerTable.NumVertices];
    }

    public global::DracoSharp.Mesh.ICornerTable CornerTable => _cornerTable;

    public void OnTraversalStart()
    {
        _predictionDegree = new int[_cornerTable.NumVertices];
    }

    public void OnTraversalEnd() { }

    public bool TraverseFromCorner(int cornerId)
    {
        if (_predictionDegree.Length == 0)
            return true;

        if (IsFaceVisited(cornerId / 3))
            return true;

        _traversalStacks[0].Add(cornerId);
        _bestPriority = 0;

        // For the first face, check the remaining corners.
        int nextVert = _cornerTable.Vertex(_cornerTable.Next(cornerId));
        int prevVert = _cornerTable.Vertex(_cornerTable.Previous(cornerId));
        if (nextVert == kInvalidVertexIndex || prevVert == kInvalidVertexIndex)
            return false;

        if (!_isVertexVisited[nextVert])
        {
            _isVertexVisited[nextVert] = true;
            _observer.OnNewVertexVisited(nextVert, _cornerTable.Next(cornerId));
        }
        if (!_isVertexVisited[prevVert])
        {
            _isVertexVisited[prevVert] = true;
            _observer.OnNewVertexVisited(prevVert, _cornerTable.Previous(cornerId));
        }
        int tipVertex = _cornerTable.Vertex(cornerId);
        if (tipVertex != kInvalidVertexIndex && !_isVertexVisited[tipVertex])
        {
            _isVertexVisited[tipVertex] = true;
            _observer.OnNewVertexVisited(tipVertex, cornerId);
        }

        // Start the actual traversal.
        while ((cornerId = PopNextCornerToTraverse()) != kInvalidCornerIndex)
        {
            int faceId = cornerId / 3;
            if (IsFaceVisited(faceId))
                continue;

            while (true)
            {
                faceId = cornerId / 3;
                _isFaceVisited[faceId] = true;
                _observer.OnNewFaceVisited(faceId);

                int vertId = _cornerTable.Vertex(cornerId);
                if (vertId == kInvalidVertexIndex)
                    return false;

                if (!_isVertexVisited[vertId])
                {
                    _isVertexVisited[vertId] = true;
                    _observer.OnNewVertexVisited(vertId, cornerId);
                }

                int rightCornerId = _cornerTable.GetRightCorner(cornerId);
                int leftCornerId = _cornerTable.GetLeftCorner(cornerId);
                int rightFaceId = rightCornerId == kInvalidCornerIndex
                    ? kInvalidFaceIndex : rightCornerId / 3;
                int leftFaceId = leftCornerId == kInvalidCornerIndex
                    ? kInvalidFaceIndex : leftCornerId / 3;
                bool isRightFaceVisited = IsFaceVisited(rightFaceId);
                bool isLeftFaceVisited = IsFaceVisited(leftFaceId);

                if (!isLeftFaceVisited)
                {
                    int priority = ComputePriority(leftCornerId);
                    if (isRightFaceVisited && priority <= _bestPriority)
                    {
                        cornerId = leftCornerId;
                        continue;
                    }
                    else
                    {
                        AddCornerToTraversalStack(leftCornerId, priority);
                    }
                }
                if (!isRightFaceVisited)
                {
                    int priority = ComputePriority(rightCornerId);
                    if (priority <= _bestPriority)
                    {
                        cornerId = rightCornerId;
                        continue;
                    }
                    else
                    {
                        AddCornerToTraversalStack(rightCornerId, priority);
                    }
                }

                // Couldn't proceed directly to the next corner.
                break;
            }
        }
        return true;
    }

    private int PopNextCornerToTraverse()
    {
        for (int i = _bestPriority; i < kMaxPriority; i++)
        {
            if (_traversalStacks[i].Count > 0)
            {
                int ret = _traversalStacks[i][^1];
                _traversalStacks[i].RemoveAt(_traversalStacks[i].Count - 1);
                _bestPriority = i;
                return ret;
            }
        }
        return kInvalidCornerIndex;
    }

    private void AddCornerToTraversalStack(int cornerId, int priority)
    {
        _traversalStacks[priority].Add(cornerId);
        if (priority < _bestPriority)
            _bestPriority = priority;
    }

    private int ComputePriority(int cornerId)
    {
        int vTip = _cornerTable.Vertex(cornerId);
        int priority = 0;
        if (!_isVertexVisited[vTip])
        {
            int degree = ++_predictionDegree[vTip];
            priority = degree > 1 ? 1 : 2;
        }
        if (priority >= kMaxPriority)
            priority = kMaxPriority - 1;
        return priority;
    }

    private bool IsFaceVisited(int faceId)
    {
        if (faceId < 0 || faceId >= _isFaceVisited.Length)
            return true;
        return _isFaceVisited[faceId];
    }
}
