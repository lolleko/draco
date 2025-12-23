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

public class SequentialNormalAttributeDecoder : SequentialIntegerAttributeDecoder
{
    private AttributeOctahedronTransform octahedronTransform;

    public SequentialNormalAttributeDecoder()
    {
        octahedronTransform = new AttributeOctahedronTransform();
    }

    protected override bool DecodeValues(int numPoints, DecoderBuffer buffer)
    {
        // Normal attributes are always 3 components (x, y, z) in the final output,
        // but are encoded as 2 components using octahedron quantization.
        // Create a temporary portable attribute with 2 components for decoding
        var portableAttr = new PointAttribute();
        portableAttr.AttributeType = attribute.AttributeType;
        portableAttr.DataType = DataType.Int32;  // Quantized normals are stored as integers
        portableAttr.NumComponents = 2;  // Octahedron encoding uses 2 components
        portableAttr.Init(portableAttr.AttributeType, portableAttr.DataType, portableAttr.NumComponents, numPoints);
        SetPortableAttribute(portableAttr);

        Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeValues] Starting, buffer position: {buffer.DecodedSize}");
        
        if (!buffer.Decode(out byte predictionSchemeMethod))
            return false;
        
        Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeValues] predictionSchemeMethod={predictionSchemeMethod}");
        
        if (!buffer.Decode(out sbyte predictionTransformType))
            return false;
        
        Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeValues] predictionTransformType={predictionTransformType}");
        
        // For now, we'll decode using the base integer decoder logic but with 2 components
        Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeValues] Calling DecodeIntegerValues with 2 components");
        
        if (!DecodeIntegerValues(numPoints, 2, buffer))
        {
            Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeValues] DecodeIntegerValues failed");
            return false;
        }
        
        Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeValues] Success");
        return true;
    }

    private bool DecodeIntegerValues(int numPoints, int numComponents, DecoderBuffer buffer)
    {
        var portableAttr = GetPortableAttribute();
        if (portableAttr == null)
            return false;

        int numValues = numPoints * numComponents;
        
        Console.WriteLine($"[DecodeIntegerValues] numPoints={numPoints}, numComponents={numComponents}, numValues={numValues}");
        Console.WriteLine($"[DecodeIntegerValues] Buffer position before compressed byte: {buffer.DecodedSize}");
        
        if (!buffer.Decode(out byte compressed))
            return false;
        
        Console.WriteLine($"[DecodeIntegerValues] compressed={compressed}");
        
        uint[] portableAttributeData = new uint[numValues];
        
        if (compressed > 0)
        {
            if (!SymbolDecoding.DecodeSymbols((uint)numValues, numComponents, buffer, portableAttributeData))
            {
                Console.WriteLine($"[DecodeIntegerValues] DecodeSymbols failed");
                return false;
            }
        }
        else
        {
            for (int i = 0; i < numValues; i++)
            {
                if (!VarintDecoding.DecodeVarint(buffer, out uint val))
                {
                    Console.WriteLine($"[DecodeIntegerValues] DecodeVarint failed at index {i}");
                    return false;
                }
                portableAttributeData[i] = val;
            }
        }
        
        // Copy to portable attribute (convert uint to int)
        int[] intData = new int[numValues];
        for (int i = 0; i < numValues; i++)
        {
            intData[i] = (int)portableAttributeData[i];
        }
        Buffer.BlockCopy(intData, 0, portableAttr.Data, 0, numValues * sizeof(int));
        
        return true;
    }

    public override bool DecodeDataNeededByPortableTransform(DecoderBuffer buffer)
    {
        Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeDataNeededByPortableTransform] Starting, buffer position: {buffer.DecodedSize}");
        
        // Decode the octahedron transform parameters
        if (!octahedronTransform.InitFromAttribute(buffer))
        {
            Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeDataNeededByPortableTransform] InitFromAttribute failed");
            return false;
        }

        Console.WriteLine($"[SequentialNormalAttributeDecoder.DecodeDataNeededByPortableTransform] Success");
        return true;
    }

    public override bool TransformAttributeToOriginalFormat(int numPoints)
    {
        if (attribute == null)
            return false;

        Console.WriteLine($"[SequentialNormalAttributeDecoder.TransformAttributeToOriginalFormat] Starting");

        // The portable attribute contains quantized 2-component octahedron values
        // We need to dequantize them back to 3D normals
        var portableAttr = GetPortableAttribute();
        if (portableAttr == null)
        {
            Console.WriteLine($"[SequentialNormalAttributeDecoder.TransformAttributeToOriginalFormat] portableAttr is null");
            return false;
        }

        // Convert int[] to float[] for the output attribute
        int[] quantizedData = new int[portableAttr.Data.Length / sizeof(int)];
        Buffer.BlockCopy(portableAttr.Data, 0, quantizedData, 0, portableAttr.Data.Length);

        float[] outputData = new float[numPoints * 3];  // 3 components for normals
        octahedronTransform.ComputeOriginalValue(quantizedData, outputData);

        // Copy result to output attribute
        Buffer.BlockCopy(outputData, 0, attribute.Data, 0, outputData.Length * sizeof(float));

        Console.WriteLine($"[SequentialNormalAttributeDecoder.TransformAttributeToOriginalFormat] Success");
        return true;
    }
}
