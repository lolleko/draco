namespace DracoSharp.Mesh;

public class MeshAttributeCornerTable : ICornerTable
{
    private bool[] _isEdgeOnSeam = [];
    private bool[] _isVertexOnSeam = [];
    private bool _noInteriorSeams = true;
    private int[] _cornerToVertexMap = [];
    private List<int> _vertexToLeftMostCornerMap = [];
    private List<int> _vertexToAttributeEntryIdMap = [];
    private CornerTable _cornerTable;

    public bool InitEmpty(CornerTable table)
    {
        _isEdgeOnSeam = new bool[table.NumCorners];
        _isVertexOnSeam = new bool[table.NumVertices];
        _cornerToVertexMap = new int[table.NumCorners];
        Array.Fill(_cornerToVertexMap, CornerTable.kInvalidVertexIndex);
        _vertexToAttributeEntryIdMap = [];
        _vertexToLeftMostCornerMap = [];
        _cornerTable = table;
        _noInteriorSeams = true;
        return true;
    }

    public void AddSeamEdge(int corner)
    {
        _isEdgeOnSeam[corner] = true;
        int v1 = _cornerTable.Vertex(_cornerTable.Next(corner));
        if (v1 != CornerTable.kInvalidVertexIndex)
            _isVertexOnSeam[v1] = true;
        int v2 = _cornerTable.Vertex(_cornerTable.Previous(corner));
        if (v2 != CornerTable.kInvalidVertexIndex)
            _isVertexOnSeam[v2] = true;

        int oppCorner = _cornerTable.Opposite(corner);
        if (oppCorner != CornerTable.kInvalidCornerIndex)
        {
            _noInteriorSeams = false;
            _isEdgeOnSeam[oppCorner] = true;
            int v3 = _cornerTable.Vertex(_cornerTable.Next(oppCorner));
            if (v3 != CornerTable.kInvalidVertexIndex)
                _isVertexOnSeam[v3] = true;
            int v4 = _cornerTable.Vertex(_cornerTable.Previous(oppCorner));
            if (v4 != CornerTable.kInvalidVertexIndex)
                _isVertexOnSeam[v4] = true;
        }
    }

    public bool RecomputeVertices()
    {
        _vertexToAttributeEntryIdMap.Clear();
        _vertexToLeftMostCornerMap.Clear();
        int numNewVertices = 0;

        for (int v = 0; v < _cornerTable.NumVertices; v++)
        {
            int c = _cornerTable.LeftMostCorner(v);
            if (c == CornerTable.kInvalidCornerIndex)
                continue;

            int firstVertId = numNewVertices++;
            _vertexToAttributeEntryIdMap.Add(firstVertId);
            int firstC = c;

            if (_isVertexOnSeam[v])
            {
                int actC = SwingLeft(firstC);
                while (actC != CornerTable.kInvalidCornerIndex)
                {
                    firstC = actC;
                    actC = SwingLeft(actC);
                    if (actC == c)
                        return false;
                }
            }

            _cornerToVertexMap[firstC] = firstVertId;
            _vertexToLeftMostCornerMap.Add(firstC);

            int swingC = _cornerTable.SwingRight(firstC);
            while (swingC != CornerTable.kInvalidCornerIndex && swingC != firstC)
            {
                if (IsCornerOppositeToSeamEdge(_cornerTable.Next(swingC)))
                {
                    firstVertId = numNewVertices++;
                    _vertexToAttributeEntryIdMap.Add(firstVertId);
                    _vertexToLeftMostCornerMap.Add(swingC);
                }
                _cornerToVertexMap[swingC] = firstVertId;
                swingC = _cornerTable.SwingRight(swingC);
            }
        }
        return true;
    }

    public bool IsCornerOppositeToSeamEdge(int corner) =>
        corner >= 0 && corner < _isEdgeOnSeam.Length && _isEdgeOnSeam[corner];

    public int Opposite(int corner)
    {
        if (corner == CornerTable.kInvalidCornerIndex || IsCornerOppositeToSeamEdge(corner))
            return CornerTable.kInvalidCornerIndex;
        return _cornerTable.Opposite(corner);
    }

    public int Next(int corner) => _cornerTable.Next(corner);
    public int Previous(int corner) => _cornerTable.Previous(corner);

    public bool IsCornerOnSeam(int corner)
    {
        int v = _cornerTable.Vertex(corner);
        return v != CornerTable.kInvalidVertexIndex && _isVertexOnSeam[v];
    }

    public int GetLeftCorner(int corner) => Opposite(Previous(corner));
    public int GetRightCorner(int corner) => Opposite(Next(corner));
    public int SwingRight(int corner) => Previous(Opposite(Previous(corner)));
    public int SwingLeft(int corner) => Next(Opposite(Next(corner)));

    public int NumVertices => _vertexToAttributeEntryIdMap.Count;
    public int NumFaces => _cornerTable.NumFaces;
    public int NumCorners => _cornerTable.NumCorners;

    public int Vertex(int corner)
    {
        if (corner < 0 || corner >= _cornerToVertexMap.Length)
            return CornerTable.kInvalidVertexIndex;
        return _cornerToVertexMap[corner];
    }

    public int VertexParent(int vert) => _vertexToAttributeEntryIdMap[vert];

    public int LeftMostCorner(int v)
    {
        if (v < 0 || v >= _vertexToLeftMostCornerMap.Count)
            return CornerTable.kInvalidCornerIndex;
        return _vertexToLeftMostCornerMap[v];
    }

    public int Face(int corner) => _cornerTable.Face(corner);
    public int FirstCorner(int face) => _cornerTable.FirstCorner(face);

    public bool IsOnBoundary(int vert)
    {
        int corner = LeftMostCorner(vert);
        if (corner == CornerTable.kInvalidCornerIndex)
            return true;
        return SwingLeft(corner) == CornerTable.kInvalidCornerIndex;
    }

    public bool IsDegenerated(int face) => _cornerTable.IsDegenerated(face);
    public bool NoInteriorSeams => _noInteriorSeams;
    public CornerTable BaseCornerTable => _cornerTable;

    public int Valence(int vertex)
    {
        if (vertex < 0 || vertex >= NumVertices)
            return -1;
        return ConfidentValence(vertex);
    }

    public int ConfidentValence(int vertex)
    {
        int corner = LeftMostCorner(vertex);
        if (corner == CornerTable.kInvalidCornerIndex)
            return 0;
        int startCorner = corner;
        int valence = 0;
        while (corner != CornerTable.kInvalidCornerIndex)
        {
            valence++;
            corner = SwingRight(corner);
            if (corner == startCorner)
                return valence;
        }
        // Boundary vertex.
        corner = SwingLeft(startCorner);
        while (corner != CornerTable.kInvalidCornerIndex)
        {
            valence++;
            corner = SwingLeft(corner);
        }
        return valence + 1;
    }
}
