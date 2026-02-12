using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Mesh;

public class Mesh : PointCloud.PointCloud
{
    private readonly List<int[]> _faces = [];
    private readonly List<MeshAttributeElementType> _attributeData = [];

    public void AddFace(int[] face)
    {
        _faces.Add(face);
    }

    public void AddFace(int v0, int v1, int v2)
    {
        _faces.Add([v0, v1, v2]);
    }

    public void SetFace(int faceIndex, int[] face)
    {
        while (_faces.Count <= faceIndex)
            _faces.Add([0, 0, 0]);
        _faces[faceIndex] = face;
    }

    public void SetNumFaces(int numFaces)
    {
        while (_faces.Count < numFaces)
            _faces.Add([0, 0, 0]);
        if (_faces.Count > numFaces)
            _faces.RemoveRange(numFaces, _faces.Count - numFaces);
    }

    public int NumFaces => _faces.Count;

    public ReadOnlySpan<int> Face(int faceIndex) => _faces[faceIndex];

    public int CornerToPointId(int cornerIndex)
    {
        if (cornerIndex < 0)
            return -1;
        int faceIndex = cornerIndex / 3;
        int localCorner = cornerIndex % 3;
        if (faceIndex >= _faces.Count)
            return -1;
        return _faces[faceIndex][localCorner];
    }

    public override void SetAttribute(int attId, PointAttribute pa)
    {
        base.SetAttribute(attId, pa);
        while (_attributeData.Count <= attId)
            _attributeData.Add(MeshAttributeElementType.CornerAttribute);
    }

    public MeshAttributeElementType GetAttributeElementType(int attId) => _attributeData[attId];

    public void SetAttributeElementType(int attId, MeshAttributeElementType elementType)
    {
        while (_attributeData.Count <= attId)
            _attributeData.Add(MeshAttributeElementType.CornerAttribute);
        _attributeData[attId] = elementType;
    }
}
