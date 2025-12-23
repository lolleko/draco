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

using System.Numerics;

namespace Draco.Decoder;

public class AttributeQuantizationTransform
{
    private float[] minValues;
    private float range;
    private byte quantizationBits;
    
    public bool InitFromAttribute(DecoderBuffer buffer, int numComponents)
    {
        minValues = new float[numComponents];
        
        if (!buffer.Decode(out quantizationBits))
            return false;
        
        for (int i = 0; i < numComponents; i++)
        {
            if (!buffer.Decode(out minValues[i]))
                return false;
        }
        
        if (!buffer.Decode(out range))
            return false;
        
        return true;
    }
    
    public void Dequantize(ReadOnlySpan<int> quantizedVals, Span<float> outVals, int numComponents)
    {
        if (quantizationBits == 0)
        {
            for (int i = 0; i < outVals.Length; i++)
            {
                outVals[i] = 0.0f;
            }
            return;
        }
        
        float maxQuantizedValue = (float)((1 << quantizationBits) - 1);
        float dequantScale = range / maxQuantizedValue;
        
        int valueId = 0;
        for (int i = 0; i < quantizedVals.Length / numComponents; i++)
        {
            for (int c = 0; c < numComponents; c++)
            {
                float value = quantizedVals[valueId] * dequantScale + minValues[c];
                outVals[valueId] = value;
                valueId++;
            }
        }
    }
    
    public void DequantizeVector3(ReadOnlySpan<int> quantizedVals, Span<Vector3> outVals)
    {
        if (quantizationBits == 0 || minValues.Length < 3)
            return;
        
        float maxQuantizedValue = (float)((1 << quantizationBits) - 1);
        float dequantScale = range / maxQuantizedValue;
        
        for (int i = 0; i < outVals.Length; i++)
        {
            int baseIdx = i * 3;
            outVals[i] = new Vector3(
                quantizedVals[baseIdx] * dequantScale + minValues[0],
                quantizedVals[baseIdx + 1] * dequantScale + minValues[1],
                quantizedVals[baseIdx + 2] * dequantScale + minValues[2]
            );
        }
    }
}

public class AttributeOctahedronTransform
{
    private byte quantizationBits;
    private int maxQuantizedValue;
    private int centerValue;
    
    public bool InitFromAttribute(DecoderBuffer buffer)
    {
        if (!buffer.Decode(out quantizationBits))
            return false;
        
        maxQuantizedValue = (1 << quantizationBits) - 1;
        centerValue = maxQuantizedValue / 2;
        
        return true;
    }
    
    public void ComputeOriginalValue(ReadOnlySpan<int> quantizedVals, Span<float> outVals)
    {
        for (int i = 0; i < quantizedVals.Length / 2; i++)
        {
            int s = quantizedVals[i * 2];
            int t = quantizedVals[i * 2 + 1];
            
            OctahedronDecode(s, t, out float nx, out float ny, out float nz);
            
            outVals[i * 3] = nx;
            outVals[i * 3 + 1] = ny;
            outVals[i * 3 + 2] = nz;
        }
    }
    
    private void OctahedronDecode(int inS, int inT, out float outX, out float outY, out float outZ)
    {
        float s = inS;
        float t = inT;
        
        s = (s / maxQuantizedValue) * 2.0f - 1.0f;
        t = (t / maxQuantizedValue) * 2.0f - 1.0f;
        
        float absSum = Math.Abs(s) + Math.Abs(t);
        
        if (absSum > 1.0f)
        {
            s = (1.0f - Math.Abs(t)) * Math.Sign(s);
            t = (1.0f - Math.Abs(s)) * Math.Sign(t);
        }
        
        float x = s;
        float y = t;
        float z = 1.0f - absSum;
        
        float length = MathF.Sqrt(x * x + y * y + z * z);
        if (length > 0)
        {
            x /= length;
            y /= length;
            z /= length;
        }
        
        outX = x;
        outY = y;
        outZ = z;
    }
}
