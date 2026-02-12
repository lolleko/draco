namespace DracoSharp.Compression.Mesh.Traverser;

public class DepthFirstTraverser : ITraverser
{
    private const int kInvalidCornerIndex = -1;
    private const int kInvalidVertexIndex = -1;
    private const int kInvalidFaceIndex = -1;

    private global::DracoSharp.Mesh.ICornerTable _cornerTable;
    private MeshAttributeIndicesEncodingObserver _observer;
    private bool[] _isFaceVisited = [];
    private bool[] _isVertexVisited = [];
    private readonly List<int> _cornerTraversalStack = [];

    public void Init(global::DracoSharp.Mesh.ICornerTable cornerTable, MeshAttributeIndicesEncodingObserver observer)
    {
        _cornerTable = cornerTable;
        _observer = observer;
        _isFaceVisited = new bool[cornerTable.NumFaces];
        _isVertexVisited = new bool[cornerTable.NumVertices];
    }

    public global::DracoSharp.Mesh.ICornerTable CornerTable => _cornerTable;

    public void OnTraversalStart() { }
    public void OnTraversalEnd() { }

    public bool TraverseFromCorner(int cornerId)
    {
        if (IsFaceVisited(cornerId / 3))
            return true;

        _cornerTraversalStack.Clear();
        _cornerTraversalStack.Add(cornerId);

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

        while (_cornerTraversalStack.Count > 0)
        {
            cornerId = _cornerTraversalStack[^1];
            int faceId = cornerId / 3;

            if (cornerId == kInvalidCornerIndex || (faceId >= 0 && faceId < _isFaceVisited.Length && _isFaceVisited[faceId]))
            {
                _cornerTraversalStack.RemoveAt(_cornerTraversalStack.Count - 1);
                continue;
            }

            while (true)
            {
                _isFaceVisited[faceId] = true;
                _observer.OnNewFaceVisited(faceId);

                int vertId = _cornerTable.Vertex(cornerId);
                if (vertId == kInvalidVertexIndex)
                    return false;

                if (!_isVertexVisited[vertId])
                {
                    bool onBoundary = _cornerTable.IsOnBoundary(vertId);
                    _isVertexVisited[vertId] = true;
                    _observer.OnNewVertexVisited(vertId, cornerId);
                    if (!onBoundary)
                    {
                        cornerId = _cornerTable.GetRightCorner(cornerId);
                        faceId = cornerId / 3;
                        continue;
                    }
                }

                int rightCornerId = _cornerTable.GetRightCorner(cornerId);
                int leftCornerId = _cornerTable.GetLeftCorner(cornerId);
                int rightFaceId = rightCornerId == kInvalidCornerIndex
                    ? kInvalidFaceIndex : rightCornerId / 3;
                int leftFaceId = leftCornerId == kInvalidCornerIndex
                    ? kInvalidFaceIndex : leftCornerId / 3;

                bool rightVisited = IsFaceVisited(rightFaceId);
                bool leftVisited = IsFaceVisited(leftFaceId);

                if (rightVisited)
                {
                    if (leftVisited)
                    {
                        _cornerTraversalStack.RemoveAt(_cornerTraversalStack.Count - 1);
                        break;
                    }
                    cornerId = leftCornerId;
                    faceId = leftFaceId;
                }
                else
                {
                    if (leftVisited)
                    {
                        cornerId = rightCornerId;
                        faceId = rightFaceId;
                    }
                    else
                    {
                        _cornerTraversalStack[^1] = leftCornerId;
                        _cornerTraversalStack.Add(rightCornerId);
                        break;
                    }
                }
            }
        }
        return true;
    }

    private bool IsFaceVisited(int faceId)
    {
        if (faceId < 0 || faceId >= _isFaceVisited.Length)
            return true;
        return _isFaceVisited[faceId];
    }
}
