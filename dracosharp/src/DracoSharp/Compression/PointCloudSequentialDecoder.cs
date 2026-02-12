using DracoSharp.Compression.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression;

public class PointCloudSequentialDecoder : PointCloudDecoder
{
    public override EncodedGeometryType GeometryType => EncodedGeometryType.PointCloud;

    protected override bool DecodeGeometryData()
    {
        if (!Buffer.Decode(out int numPoints))
            return false;
        if (numPoints < 0)
            return false;
        PointCloud.NumPoints = numPoints;
        return true;
    }

    protected override bool CreateAttributesDecoder(int attrDecoderIndex)
    {
        var controller = new SequentialAttributeDecodersController();
        SetAttributesDecoder(attrDecoderIndex, controller);
        return true;
    }
}
