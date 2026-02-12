using System.Buffers.Binary;
using DracoSharp.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression.Attributes;

public class AttributeQuantizationTransform
{
    private int _quantizationBits = -1;
    private float[] _minValues = [];
    private float _range;

    public int QuantizationBits => _quantizationBits;
    public float Range => _range;
    public float MinValue(int axis) => _minValues[axis];
    public bool IsInitialized => _quantizationBits != -1;

    public bool DecodeParameters(PointAttribute attribute, DecoderBuffer buffer)
    {
        int numComponents = attribute.NumComponents;
        _minValues = new float[numComponents];
        Span<byte> floatBytes = stackalloc byte[4];
        for (int i = 0; i < numComponents; i++)
        {
            if (!buffer.Decode(out float val))
                return false;
            _minValues[i] = val;
        }
        if (!buffer.Decode(out _range))
            return false;
        if (!buffer.Decode(out byte quantizationBits))
            return false;
        if (!IsQuantizationValid(quantizationBits))
            return false;
        _quantizationBits = quantizationBits;
        return true;
    }

    public void CopyToAttributeTransformData(AttributeTransformData outData)
    {
        outData.TransformType = AttributeTransformType.QuantizationTransform;
        outData.AppendParameterValue(_quantizationBits);
        for (int i = 0; i < _minValues.Length; i++)
            outData.AppendParameterValue(_minValues[i]);
        outData.AppendParameterValue(_range);
    }

    public bool InverseTransformAttribute(PointAttribute srcAttribute, PointAttribute targetAttribute)
    {
        if (targetAttribute.DataType != DataType.Float32)
            return false;

        int maxQuantizedValue = (1 << _quantizationBits) - 1;
        int numComponents = targetAttribute.NumComponents;
        int entrySize = sizeof(float) * numComponents;
        Span<float> attVal = stackalloc float[numComponents];

        var dequantizer = new Dequantizer();
        if (!dequantizer.Init(_range, maxQuantizedValue))
            return false;

        int numValues = targetAttribute.Size;
        int quantValId = 0;
        int outBytePos = 0;

        ReadOnlySpan<byte> srcData = srcAttribute.Buffer.Data;
        Span<byte> bytes = stackalloc byte[entrySize];

        for (int i = 0; i < numValues; i++)
        {
            for (int c = 0; c < numComponents; c++)
            {
                int qVal = BinaryPrimitives.ReadInt32LittleEndian(
                    srcData.Slice(quantValId * 4));
                quantValId++;
                float value = dequantizer.DequantizeFloat(qVal) + _minValues[c];
                attVal[c] = value;
            }
            for (int c = 0; c < numComponents; c++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.Slice(c * 4), attVal[c]);
            targetAttribute.Buffer.Write(outBytePos, bytes);
            outBytePos += entrySize;
        }
        return true;
    }

    public bool TransferToAttribute(PointAttribute attribute)
    {
        var transformData = new AttributeTransformData();
        CopyToAttributeTransformData(transformData);
        attribute.SetAttributeTransformData(transformData);
        return true;
    }

    private static bool IsQuantizationValid(int quantizationBits) =>
        quantizationBits >= 1 && quantizationBits <= 30;
}
