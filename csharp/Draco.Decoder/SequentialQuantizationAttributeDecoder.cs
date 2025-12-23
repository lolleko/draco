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

public class SequentialQuantizationAttributeDecoder : SequentialIntegerAttributeDecoder
{
    private AttributeQuantizationTransform quantizationTransform;
    private bool quantizationDataDecoded;
    
    public SequentialQuantizationAttributeDecoder()
    {
        quantizationTransform = new AttributeQuantizationTransform();
        quantizationDataDecoded = false;
    }
    
    public override bool DecodePortableAttribute(int numPoints, DecoderBuffer buffer)
    {
        // Call base implementation to decode integer values
        if (!base.DecodePortableAttribute(numPoints, buffer))
            return false;
        
        // For bitstream version < 2.0, decode quantization data immediately after integer values
        if (buffer.BitstreamVersion < 0x0200)
        {
            var portAttr = GetPortableAttribute();
            if (portAttr == null)
            {
                portAttr = attribute;
            }
            
            if (!quantizationTransform.InitFromAttribute(buffer, portAttr.NumComponents))
                return false;
            
            quantizationDataDecoded = true;
        }
        
        return true;
    }
    
    public override bool DecodeDataNeededByPortableTransform(DecoderBuffer buffer)
    {
        // For bitstream version >= 2.0, decode quantization data here
        // For older versions, it was already decoded in DecodePortableAttribute
        if (buffer.BitstreamVersion >= 0x0200 && !quantizationDataDecoded)
        {
            var portAttr = GetPortableAttribute();
            if (portAttr == null)
            {
                portAttr = attribute;
            }
            
            if (!quantizationTransform.InitFromAttribute(buffer, portAttr.NumComponents))
                return false;
            
            quantizationDataDecoded = true;
        }
        
        return true;
    }
    
    public override bool TransformAttributeToOriginalFormat(int numPoints)
    {
        return DequantizeValues(numPoints);
    }
    
    private bool DequantizeValues(int numPoints)
    {
        var portAttr = GetPortableAttribute();
        if (portAttr == null)
            return false;
        
        int numComponents = attribute.NumComponents;
        int numValues = numPoints * numComponents;
        
        var quantizedData = new int[numValues];
        for (int i = 0; i < numValues; i++)
        {
            quantizedData[i] = BitConverter.ToInt32(portAttr.Data, i * 4);
        }
        
        var dequantizedData = new float[numValues];
        quantizationTransform.Dequantize(quantizedData, dequantizedData, numComponents);
        
        for (int i = 0; i < numValues; i++)
        {
            BitConverter.GetBytes(dequantizedData[i]).CopyTo(attribute.Data, i * 4);
        }
        
        return true;
    }
}
