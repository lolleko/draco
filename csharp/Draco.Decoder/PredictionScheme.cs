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

public interface IPredictionSchemeDecoder
{
    bool ComputeOriginalValues(Span<int> corrValues, Span<int> outValues, int numComponents, int numEntries);
    bool DecodePredictionData(DecoderBuffer buffer);
    bool AreCorrectionsPositive();
}

public class PredictionSchemeDeltaDecoder : IPredictionSchemeDecoder
{
    public bool ComputeOriginalValues(Span<int> corrValues, Span<int> outValues, int numComponents, int numEntries)
    {
        if (corrValues.Length < numEntries * numComponents)
            return false;
        if (outValues.Length < numEntries * numComponents)
            return false;
        
        corrValues.Slice(0, numComponents).CopyTo(outValues.Slice(0, numComponents));
        
        int dstOffset = numComponents;
        int srcOffset = numComponents;
        
        for (int p = 1; p < numEntries; p++)
        {
            for (int c = 0; c < numComponents; c++)
            {
                outValues[dstOffset] = corrValues[srcOffset] + outValues[dstOffset - numComponents];
                dstOffset++;
                srcOffset++;
            }
        }
        
        return true;
    }
    
    public bool DecodePredictionData(DecoderBuffer buffer)
    {
        return true;
    }
    
    public bool AreCorrectionsPositive()
    {
        return false;
    }
}

public class PassthroughPredictionSchemeDecoder : IPredictionSchemeDecoder
{
    public bool ComputeOriginalValues(Span<int> corrValues, Span<int> outValues, int numComponents, int numEntries)
    {
        // Passthrough - corrections are the original values
        corrValues.CopyTo(outValues);
        return true;
    }
    
    public bool DecodePredictionData(DecoderBuffer buffer)
    {
        // No additional data to decode
        return true;
    }
    
    public bool AreCorrectionsPositive()
    {
        return false;
    }
}

public class PredictionSchemeWrapTransform
{
    private readonly int[] minValues;
    private readonly int[] maxValues;
    
    public PredictionSchemeWrapTransform(int numComponents)
    {
        minValues = new int[numComponents];
        maxValues = new int[numComponents];
    }
    
    public bool Init(DecoderBuffer buffer, int numComponents)
    {
        for (int i = 0; i < numComponents; i++)
        {
            if (!buffer.Decode(out minValues[i]))
                return false;
            if (!buffer.Decode(out maxValues[i]))
                return false;
        }
        return true;
    }
    
    public void ComputeOriginalValue(Span<int> predVals, Span<int> corrVals, Span<int> outOriginalVals)
    {
        for (int i = 0; i < predVals.Length; i++)
        {
            int range = maxValues[i] - minValues[i] + 1;
            if (range < 1)
                range = 1;
            
            int diff = corrVals[i];
            int originalVal = predVals[i] + diff;
            
            if (originalVal > maxValues[i])
            {
                originalVal = minValues[i] + (originalVal - maxValues[i] - 1) % range;
            }
            else if (originalVal < minValues[i])
            {
                originalVal = maxValues[i] - (minValues[i] - originalVal - 1) % range;
            }
            
            outOriginalVals[i] = originalVal;
        }
    }
}
