namespace DracoSharp.Mesh;

public class CornerTable : ICornerTable
{
    public const int kInvalidCornerIndex = -1;
    public const int kInvalidVertexIndex = -1;
    public const int kInvalidFaceIndex = -1;

    private List<int> _cornerToVertexMap = [];
    private List<int> _oppositeCorners = [];
    private List<int> _vertexCorners = [];
    private int _numOriginalVertices;

    public int NumVertices => _vertexCorners.Count;
    public int NumCorners => _cornerToVertexMap.Count;
    public int NumFaces => _cornerToVertexMap.Count / 3;

    public bool Reset(int numFaces, int numVertices)
    {
        if (numFaces < 0 || numVertices < 0)
            return false;
        _cornerToVertexMap = new List<int>(numFaces * 3);
        for (int i = 0; i < numFaces * 3; i++)
            _cornerToVertexMap.Add(kInvalidVertexIndex);
        _oppositeCorners = new List<int>(numFaces * 3);
        for (int i = 0; i < numFaces * 3; i++)
            _oppositeCorners.Add(kInvalidCornerIndex);
        // Only reserve capacity; vertices are added via AddNewVertex().
        _vertexCorners = new List<int>(numVertices);
        _numOriginalVertices = 0;
        return true;
    }

    public int Opposite(int corner)
    {
        if (corner == kInvalidCornerIndex)
            return kInvalidCornerIndex;
        return _oppositeCorners[corner];
    }

    public int Next(int corner)
    {
        if (corner == kInvalidCornerIndex)
            return kInvalidCornerIndex;
        return LocalIndex(corner + 1) != 0 ? corner + 1 : corner - 2;
    }

    public int Previous(int corner)
    {
        if (corner == kInvalidCornerIndex)
            return kInvalidCornerIndex;
        return LocalIndex(corner) != 0 ? corner - 1 : corner + 2;
    }

    public int Vertex(int corner)
    {
        if (corner == kInvalidCornerIndex)
            return kInvalidVertexIndex;
        return _cornerToVertexMap[corner];
    }

    public int Face(int corner)
    {
        if (corner == kInvalidCornerIndex)
            return kInvalidFaceIndex;
        return corner / 3;
    }

    public int FirstCorner(int face)
    {
        if (face == kInvalidFaceIndex)
            return kInvalidCornerIndex;
        return face * 3;
    }

    public int LocalIndex(int corner) => corner % 3;

    public int LeftMostCorner(int vertex) => _vertexCorners[vertex];

    public int SwingRight(int corner) => Previous(Opposite(Previous(corner)));

    public int SwingLeft(int corner) => Next(Opposite(Next(corner)));

    public int GetLeftCorner(int corner)
    {
        if (corner == kInvalidCornerIndex)
            return kInvalidCornerIndex;
        return Opposite(Previous(corner));
    }

    public int GetRightCorner(int corner)
    {
        if (corner == kInvalidCornerIndex)
            return kInvalidCornerIndex;
        return Opposite(Next(corner));
    }

    public void SetOppositeCorner(int corner, int oppCorner)
    {
        _oppositeCorners[corner] = oppCorner;
    }

    public void SetOppositeCorners(int corner0, int corner1)
    {
        if (corner0 != kInvalidCornerIndex)
            SetOppositeCorner(corner0, corner1);
        if (corner1 != kInvalidCornerIndex)
            SetOppositeCorner(corner1, corner0);
    }

    public void MapCornerToVertex(int corner, int vertex)
    {
        _cornerToVertexMap[corner] = vertex;
    }

    public int AddNewVertex()
    {
        _vertexCorners.Add(kInvalidCornerIndex);
        return _vertexCorners.Count - 1;
    }

    public void SetLeftMostCorner(int vertex, int corner)
    {
        if (vertex != kInvalidVertexIndex)
            _vertexCorners[vertex] = corner;
    }

    public void SetNumVertices(int numVertices)
    {
        while (_vertexCorners.Count < numVertices)
            _vertexCorners.Add(kInvalidCornerIndex);
        if (_vertexCorners.Count > numVertices)
            _vertexCorners.RemoveRange(numVertices, _vertexCorners.Count - numVertices);
    }

    public void MakeVertexIsolated(int vertex)
    {
        _vertexCorners[vertex] = kInvalidCornerIndex;
    }

    public bool IsVertexIsolated(int vertex) => LeftMostCorner(vertex) == kInvalidCornerIndex;

    public bool IsOnBoundary(int vertex)
    {
        int corner = LeftMostCorner(vertex);
        return SwingLeft(corner) == kInvalidCornerIndex;
    }

    public bool IsDegenerated(int face)
    {
        if (face == kInvalidFaceIndex)
            return true;
        int firstCorner = FirstCorner(face);
        int v0 = Vertex(firstCorner);
        int v1 = Vertex(firstCorner + 1);
        int v2 = Vertex(firstCorner + 2);
        return v0 == kInvalidVertexIndex || v1 == kInvalidVertexIndex ||
               v2 == kInvalidVertexIndex || v0 == v1 || v0 == v2 || v1 == v2;
    }

    public int Valence(int vertex)
    {
        if (vertex < 0 || vertex >= NumVertices)
            return -1;
        int corner = LeftMostCorner(vertex);
        if (corner == kInvalidCornerIndex)
            return 0;
        return ConfidentValence(vertex);
    }

    public int ConfidentValence(int vertex)
    {
        int corner = LeftMostCorner(vertex);
        int startCorner = corner;
        int valence = 0;
        while (corner != kInvalidCornerIndex)
        {
            valence++;
            corner = SwingRight(corner);
            if (corner == startCorner)
                return valence;
        }
        // Boundary vertex - also count from the other direction.
        corner = SwingLeft(startCorner);
        while (corner != kInvalidCornerIndex)
        {
            valence++;
            corner = SwingLeft(corner);
        }
        return valence + 1; // +1 for the boundary edge.
    }

    public void UpdateVertexToCornerMap(int vertex)
    {
        int firstC = _vertexCorners[vertex];
        if (firstC == kInvalidCornerIndex)
            return;
        int actC = SwingLeft(firstC);
        int c = firstC;
        while (actC != kInvalidCornerIndex && actC != firstC)
        {
            c = actC;
            actC = SwingLeft(actC);
        }
        if (actC != firstC)
            _vertexCorners[vertex] = c;
    }

    public int NumOriginalVertices => _numOriginalVertices;
}
