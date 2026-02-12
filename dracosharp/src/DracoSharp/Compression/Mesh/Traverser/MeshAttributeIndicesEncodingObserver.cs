using DracoSharp.Compression.Attributes;
using MeshType = DracoSharp.Mesh.Mesh;
using ICornerTable = global::DracoSharp.Mesh.ICornerTable;

namespace DracoSharp.Compression.Mesh.Traverser;

public class MeshAttributeIndicesEncodingObserver
{
    private readonly ICornerTable _attConnectivity;
    private readonly MeshAttributeIndicesEncodingData _encodingData;
    private readonly MeshType _mesh;
    private readonly PointsSequencer _sequencer;

    public MeshAttributeIndicesEncodingObserver(
        ICornerTable connectivity, MeshType mesh,
        PointsSequencer sequencer, MeshAttributeIndicesEncodingData encodingData)
    {
        _attConnectivity = connectivity;
        _encodingData = encodingData;
        _mesh = mesh;
        _sequencer = sequencer;
    }

    public void OnNewFaceVisited(int face) { }

    public void OnNewVertexVisited(int vertex, int corner)
    {
        int pointId = _mesh.CornerToPointId(corner);
        _sequencer.AddPointId(pointId);
        _encodingData.EncodedAttributeValueIndexToCornerMap.Add(corner);
        _encodingData.VertexToEncodedAttributeValueIndexMap[vertex] = _encodingData.NumValues;
        _encodingData.NumValues++;
    }
}
