using DracoSharp.Compression.Attributes;
using DracoSharp.Core;
using DracoSharp.Mesh;

namespace DracoSharp.Compression.Mesh;

public class MeshEdgebreakerDecoder : MeshDecoder
{
    private MeshEdgebreakerDecoderImpl _impl;

    public override CornerTable GetCornerTable() => _impl?.CornerTable;

    public override MeshAttributeCornerTable GetAttributeCornerTable(int attId) =>
        _impl?.GetAttributeCornerTable(attId);

    public override MeshAttributeIndicesEncodingData GetAttributeEncodingData(int attId) =>
        _impl?.GetAttributeEncodingData(attId);

    protected override bool InitializeDecoder()
    {
        if (!Buffer.Decode(out byte traversalDecoderType))
            return false;

        _impl = new MeshEdgebreakerDecoderImpl();

        MeshEdgebreakerTraversalDecoder traversalDecoder =
            (MeshEdgebreakerConnectivityEncodingMethod)traversalDecoderType switch
            {
                MeshEdgebreakerConnectivityEncodingMethod.StandardEncoding =>
                    new MeshEdgebreakerTraversalDecoder(),
                MeshEdgebreakerConnectivityEncodingMethod.ValenceEncoding =>
                    new MeshEdgebreakerTraversalValenceDecoder(),
                _ => null
            };

        if (traversalDecoder == null)
            return false;

        _impl.SetTraversalDecoder(traversalDecoder);

        if (!_impl.Init(this))
            return false;
        return true;
    }

    protected override bool CreateAttributesDecoder(int attrDecoderIndex) =>
        _impl.CreateAttributesDecoder(attrDecoderIndex);

    protected override bool DecodeConnectivity() =>
        _impl.DecodeConnectivity();

    protected override void OnAttributesDecoded() =>
        _impl.OnAttributesDecoded();
}
