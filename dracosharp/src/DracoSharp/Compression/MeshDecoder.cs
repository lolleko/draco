using DracoSharp.Compression.Attributes;
using DracoSharp.Core;
using DracoSharp.Mesh;
using MeshType = DracoSharp.Mesh.Mesh;

namespace DracoSharp.Compression;

public abstract class MeshDecoder : PointCloudDecoder
{
    public override EncodedGeometryType GeometryType => EncodedGeometryType.TriangularMesh;

    public MeshType Mesh => (MeshType)PointCloud;

    public virtual CornerTable GetCornerTable() => null;

    public virtual MeshAttributeCornerTable GetAttributeCornerTable(int attId) => null;

    public virtual MeshAttributeIndicesEncodingData GetAttributeEncodingData(int attId) => null;

    protected override bool DecodeGeometryData()
    {
        if (!DecodeConnectivity())
            return false;
        return base.DecodeGeometryData();
    }

    protected abstract bool DecodeConnectivity();
}
