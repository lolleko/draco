// Copyright 2024 The Draco Authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Draco.Decoder;

public enum PredictionSchemeMethod : byte
{
    None = 0,
    Difference = 1,
    Parallelogram = 2,
    MultiParallelogram = 3,
    TexCoordsDeprecated = 4,
    TexCoordsPortable = 5,
    GeometricNormal = 6,
    Count = 7
}

public enum PredictionSchemeTransformType : sbyte
{
    None = -1,
    Wrap = 0,
    NormalOctahedron = 1,
    NormalOctahedronCanonicalized = 2,
    Count = 3
}

public class SequentialAttributeDecoder
{
    protected PointAttribute attribute;
    protected PointAttribute portableAttribute;
    
    public virtual bool DecodePortableAttribute(int numPoints, DecoderBuffer buffer)
    {
        if (attribute.NumComponents <= 0)
            return false;
        
        return DecodeValues(numPoints, buffer);
    }
    
    public virtual bool DecodeDataNeededByPortableTransform(DecoderBuffer buffer)
    {
        return true;
    }
    
    public virtual bool TransformAttributeToOriginalFormat(int numPoints)
    {
        return true;
    }
    
    protected virtual bool DecodeValues(int numPoints, DecoderBuffer buffer)
    {
        int numComponents = attribute.NumComponents;
        int valueSize = attribute.ValueSize;
        
        for (int i = 0; i < numPoints; i++)
        {
            int offset = i * valueSize;
            if (!buffer.Decode(attribute.Data.AsSpan().Slice(offset, valueSize)))
                return false;
        }
        
        return true;
    }
    
    public void SetAttribute(PointAttribute attr)
    {
        attribute = attr;
    }
    
    public void SetPortableAttribute(PointAttribute attr)
    {
        portableAttribute = attr;
    }
    
    public PointAttribute GetPortableAttribute()
    {
        return portableAttribute ?? attribute;
    }
}

public class SequentialIntegerAttributeDecoder : SequentialAttributeDecoder
{
    private IPredictionSchemeDecoder predictionScheme;
    
    protected override bool DecodeValues(int numPoints, DecoderBuffer buffer)
    {
        if (!buffer.Decode(out byte predictionSchemeMethod))
            return false;
        
        if (predictionSchemeMethod < 0 || predictionSchemeMethod >= (byte)PredictionSchemeMethod.Count)
            return false;
        
        if (predictionSchemeMethod != (byte)PredictionSchemeMethod.None)
        {
            if (!buffer.Decode(out byte predictionTransformType))
                return false;
            
            if (predictionTransformType >= (byte)PredictionSchemeTransformType.Count)
                return false;
            
            predictionScheme = CreatePredictionScheme(
                (PredictionSchemeMethod)predictionSchemeMethod,
                (PredictionSchemeTransformType)predictionTransformType);
        }
        
        if (!DecodeIntegerValues(numPoints, buffer))
            return false;
        
        return true;
    }
    
    private IPredictionSchemeDecoder CreatePredictionScheme(
        PredictionSchemeMethod method, PredictionSchemeTransformType transformType)
    {
        if (transformType == PredictionSchemeTransformType.Wrap)
        {
            if (method == PredictionSchemeMethod.Difference)
            {
                return new PredictionSchemeDeltaDecoder();
            }
        }
        
        return null;
    }
    
    private bool DecodeIntegerValues(int numPoints, DecoderBuffer buffer)
    {
        int numComponents = attribute.NumComponents;
        if (numComponents <= 0)
            return false;
        
        int numValues = numPoints * numComponents;
        
        var portableData = new int[numValues];
        
        if (!buffer.Decode(out byte compressed))
            return false;
        
        if (compressed > 0)
        {
            if (!DecodeSymbols((uint)numValues, numComponents, buffer, portableData))
                return false;
        }
        else
        {
            if (!buffer.Decode(out byte numBytes))
                return false;
            
            if (numBytes == 4)
            {
                for (int i = 0; i < numValues; i++)
                {
                    if (!buffer.Decode(out portableData[i]))
                        return false;
                }
            }
            else
            {
                for (int i = 0; i < numValues; i++)
                {
                    int value = 0;
                    for (int b = 0; b < numBytes; b++)
                    {
                        if (!buffer.Decode(out byte byteVal))
                            return false;
                        value |= byteVal << (b * 8);
                    }
                    if (numBytes < 4 && (value & (1 << (numBytes * 8 - 1))) != 0)
                    {
                        value |= -1 << (numBytes * 8);
                    }
                    portableData[i] = value;
                }
            }
        }
        
        if (numValues > 0 && (predictionScheme == null || !predictionScheme.AreCorrectionsPositive()))
        {
            ConvertSymbolsToSignedInts(portableData);
        }
        
        if (predictionScheme != null)
        {
            if (!predictionScheme.DecodePredictionData(buffer))
                return false;
            
            if (numValues > 0)
            {
                var outValues = new int[numValues];
                if (!predictionScheme.ComputeOriginalValues(portableData, outValues, numComponents, numPoints))
                    return false;
                portableData = outValues;
            }
        }
        
        PreparePortableAttribute(numPoints, numComponents);
        StorePortableData(portableData);
        
        return true;
    }
    
    private bool DecodeSymbols(uint numValues, int numComponents, DecoderBuffer buffer, int[] outValues)
    {
        if (!VarintDecoding.DecodeVarint(buffer, out uint maxValue))
            return false;
        
        if (maxValue == 0)
        {
            Array.Clear(outValues, 0, outValues.Length);
            return true;
        }
        
        if (!buffer.Decode(out byte schemeMethod))
            return false;
        
        if (schemeMethod == 0)
        {
            for (int i = 0; i < numValues; i++)
            {
                if (!VarintDecoding.DecodeVarint(buffer, out uint val))
                    return false;
                outValues[i] = (int)val;
            }
            return true;
        }
        
        return false;
    }
    
    private void ConvertSymbolsToSignedInts(int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            uint val = (uint)values[i];
            bool isNegative = (val & 1) != 0;
            val >>= 1;
            values[i] = isNegative ? -(int)val - 1 : (int)val;
        }
    }
    
    private void PreparePortableAttribute(int numEntries, int numComponents)
    {
        var portAttr = new PointAttribute
        {
            AttributeType = attribute.AttributeType,
            DataType = DataType.Int32,
            NumComponents = (byte)numComponents,
            UniqueId = attribute.UniqueId
        };
        portAttr.Init(attribute.AttributeType, DataType.Int32, (byte)numComponents, numEntries);
        SetPortableAttribute(portAttr);
    }
    
    private void StorePortableData(int[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            int offset = i * 4;
            var bytes = BitConverter.GetBytes(data[i]);
            bytes.CopyTo(portableAttribute.Data, offset);
        }
    }
    
    public override bool TransformAttributeToOriginalFormat(int numPoints)
    {
        return StoreValues(numPoints);
    }
    
    private bool StoreValues(int numPoints)
    {
        int numComponents = attribute.NumComponents;
        int numValues = numPoints * numComponents;
        
        var portableData = new int[numValues];
        for (int i = 0; i < numValues; i++)
        {
            int offset = i * 4;
            portableData[i] = BitConverter.ToInt32(portableAttribute.Data, offset);
        }
        
        int valueId = 0;
        int outBytePos = 0;
        
        for (int i = 0; i < numPoints; i++)
        {
            for (int c = 0; c < numComponents; c++)
            {
                int value = portableData[valueId++];
                
                switch (attribute.DataType)
                {
                    case DataType.Int8:
                        attribute.Data[outBytePos++] = (byte)(sbyte)value;
                        break;
                    case DataType.UInt8:
                        attribute.Data[outBytePos++] = (byte)value;
                        break;
                    case DataType.Int16:
                        BitConverter.GetBytes((short)value).CopyTo(attribute.Data, outBytePos);
                        outBytePos += 2;
                        break;
                    case DataType.UInt16:
                        BitConverter.GetBytes((ushort)value).CopyTo(attribute.Data, outBytePos);
                        outBytePos += 2;
                        break;
                    case DataType.Int32:
                    case DataType.UInt32:
                        BitConverter.GetBytes(value).CopyTo(attribute.Data, outBytePos);
                        outBytePos += 4;
                        break;
                    case DataType.Float32:
                        BitConverter.GetBytes((float)value).CopyTo(attribute.Data, outBytePos);
                        outBytePos += 4;
                        break;
                    default:
                        return false;
                }
            }
        }
        
        return true;
    }
}
