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

public class RAnsSymbolDecoder
{
    private uint prob;
    private uint cumulProb;
    
    public uint Probability => prob;
    public uint CumulativeProbability => cumulProb;
    
    public bool Decode(uint precisionBits, ReadOnlySpan<byte> data, ref int offset)
    {
        uint precision = 1u << (int)precisionBits;
        
        if (offset + 1 > data.Length)
            return false;
            
        prob = data[offset++];
        
        if (prob == 0 || prob > precision)
            return false;
        
        if (offset >= data.Length)
            return false;
            
        cumulProb = data[offset++];
        
        if (cumulProb >= precision)
            return false;
        
        return true;
    }
}

public class RAnsDecoder
{
    private const uint RAnsL = 4096;
    private const uint RAnsPrecision = 12;
    
    private uint state;
    private readonly byte[] buffer;
    private int bufferOffset;
    
    public RAnsDecoder(byte[] inputBuffer, int startOffset = 0)
    {
        buffer = inputBuffer;
        bufferOffset = startOffset;
        state = 0;
    }
    
    public bool StartDecoding(int numBytes)
    {
        if (numBytes <= 0)
            return false;
            
        if (bufferOffset + numBytes > buffer.Length)
            return false;
        
        bufferOffset += numBytes - 1;
        
        state = 0;
        for (int i = 0; i < 4 && bufferOffset >= 0; i++)
        {
            state = (state << 8) | buffer[bufferOffset--];
        }
        
        return true;
    }
    
    public uint DecodeSymbol(RAnsSymbolDecoder symbolDecoder)
    {
        uint x = state;
        uint quot = x / RAnsL;
        uint rem = x % RAnsL;
        
        uint fetch = 0;
        if (bufferOffset >= 0 && bufferOffset < buffer.Length)
        {
            fetch = buffer[bufferOffset--];
        }
        
        uint symbol = rem >> (int)(RAnsPrecision - symbolDecoder.Probability);
        
        uint start = symbolDecoder.CumulativeProbability;
        state = start + (quot * RAnsL) + (rem - start * RAnsL);
        
        while (state < RAnsL && bufferOffset >= 0)
        {
            state = (state << 8) | buffer[bufferOffset--];
        }
        
        return symbol;
    }
    
    public void EndDecoding()
    {
        state = 0;
    }
}
