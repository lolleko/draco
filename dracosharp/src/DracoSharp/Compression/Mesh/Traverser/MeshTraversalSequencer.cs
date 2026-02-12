using DracoSharp.Attributes;
using DracoSharp.Compression.Attributes;
using MeshType = DracoSharp.Mesh.Mesh;

namespace DracoSharp.Compression.Mesh.Traverser;

public class MeshTraversalSequencer : PointsSequencer
{
    private readonly MeshType _mesh;
    private readonly MeshAttributeIndicesEncodingData _encodingData;
    private ITraverser _traverser;

    public MeshTraversalSequencer(MeshType mesh, MeshAttributeIndicesEncodingData encodingData)
    {
        _mesh = mesh;
        _encodingData = encodingData;
    }

    public void SetTraverser(ITraverser traverser) => _traverser = traverser;

    public override bool UpdatePointToAttributeIndexMapping(PointAttribute attribute)
    {
        var cornerTable = _traverser.CornerTable;
        attribute.SetExplicitMapping(_mesh.NumPoints);
        int numFaces = _mesh.NumFaces;
        int numPoints = _mesh.NumPoints;
        for (int f = 0; f < numFaces; f++)
        {
            var face = _mesh.Face(f);
            for (int p = 0; p < 3; p++)
            {
                int pointId = face[p];
                int vertId = cornerTable.Vertex(3 * f + p);
                if (vertId == -1) // kInvalidVertexIndex
                    return false;
                int attEntryId = _encodingData.VertexToEncodedAttributeValueIndexMap[vertId];
                if (pointId >= numPoints || attEntryId >= numPoints)
                    return false;
                attribute.SetPointMapEntry(pointId, attEntryId);
            }
        }
        return true;
    }

    protected override bool GenerateSequenceInternal()
    {
        OutPointIds.EnsureCapacity(_traverser.CornerTable.NumVertices);
        _traverser.OnTraversalStart();
        int numFaces = _traverser.CornerTable.NumFaces;
        for (int i = 0; i < numFaces; i++)
        {
            if (!_traverser.TraverseFromCorner(3 * i))
                return false;
        }
        _traverser.OnTraversalEnd();
        return true;
    }
}
