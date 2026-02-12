using System.Buffers.Binary;
using System.Runtime.InteropServices;
using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class AttributeOctahedronTransform
{
    private int _quantizationBits = -1;

    public bool DecodeParameters(PointAttribute attribute, DecoderBuffer buffer)
    {
        if (!buffer.Decode(out byte quantizationBits))
            return false;
        _quantizationBits = quantizationBits;
        return true;
    }

    public bool TransferToAttribute(PointAttribute attribute)
    {
        var transformData = new AttributeTransformData();
        CopyToAttributeTransformData(transformData);
        attribute.SetAttributeTransformData(transformData);
        return true;
    }

    public int QuantizationBits => _quantizationBits;

    public bool InverseTransformAttribute(PointAttribute sourceAttribute, PointAttribute targetAttribute)
    {
        if (targetAttribute.DataType != DataType.Float32)
            return false;

        int numPoints = targetAttribute.Size;
        int numComponents = targetAttribute.NumComponents;
        if (numComponents != 3)
            return false;

        var toolBox = new OctahedronToolBox();
        if (!toolBox.SetQuantizationBits(_quantizationBits))
            return false;

        var sourceData = MemoryMarshal.Cast<byte, int>(sourceAttribute.Buffer.MutableData);
        Span<float> attVal = stackalloc float[3];
        int entrySize = 3 * sizeof(float);
        int outBytePos = 0;
        int srcIdx = 0;

        for (int i = 0; i < numPoints; i++)
        {
            int s = sourceData[srcIdx++];
            int t = sourceData[srcIdx++];
            toolBox.QuantizedOctahedralCoordsToUnitVector(s, t, attVal);

            targetAttribute.Buffer.Write(outBytePos, MemoryMarshal.AsBytes(attVal));
            outBytePos += entrySize;
        }
        return true;
    }

    private void CopyToAttributeTransformData(AttributeTransformData outData)
    {
        outData.TransformType = AttributeTransformType.OctahedronTransform;
        outData.AppendParameterValue(_quantizationBits);
    }
}
