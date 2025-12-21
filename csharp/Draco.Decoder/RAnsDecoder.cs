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
    private uint[] probabilityTable;
    private uint[] cumulativeProbabilities;
    private uint[] lookupTable;
    private uint numSymbols;
    private int precisionBits;
    private int precision;
    
    private uint state;
    private byte[] buffer;
    private int bufferOffset;
    private const uint RANS_L = 4096;
    
    public uint NumSymbols => numSymbols;
    
    public RAnsSymbolDecoder(int uniqueSymbolsBitLength)
    {
        precisionBits = ComputeRAnsPrecision(uniqueSymbolsBitLength);
        precision = 1 << precisionBits;
    }
    
    private static int ComputeRAnsPrecision(int uniqueSymbolsBitLength)
    {
        return (3 * uniqueSymbolsBitLength) / 2 + 1;
    }
    
    public bool Create(DecoderBuffer buffer)
    {
        if (!VarintDecoding.DecodeVarint(buffer, out numSymbols))
            return false;
        
        if (numSymbols / 64 > buffer.RemainingSize)
            return false;
        
        probabilityTable = new uint[numSymbols];
        cumulativeProbabilities = new uint[numSymbols];
        
        if (numSymbols == 0)
            return true;
        
        for (uint i = 0; i < numSymbols; i++)
        {
            if (!buffer.Decode(out byte probData))
                return false;
            
            int token = probData & 3;
            if (token == 3)
            {
                uint offset = (uint)(probData >> 2);
                if (i + offset >= numSymbols)
                    return false;
                
                for (uint j = 0; j <= offset; j++)
                {
                    probabilityTable[i + j] = 0;
                }
                i += offset;
            }
            else
            {
                int extraBytes = token;
                uint prob = (uint)(probData >> 2);
                for (int b = 0; b < extraBytes; b++)
                {
                    if (!buffer.Decode(out byte eb))
                        return false;
                    prob |= (uint)eb << (8 * (b + 1) - 2);
                }
                probabilityTable[i] = prob;
            }
        }
        
        return BuildLookupTable();
    }
    
    private bool BuildLookupTable()
    {
        lookupTable = new uint[precision];
        
        uint cumulativeProb = 0;
        uint actProb = 0;
        
        for (uint i = 0; i < numSymbols; i++)
        {
            uint prob = probabilityTable[i];
            cumulativeProbabilities[i] = cumulativeProb;
            cumulativeProb += prob;
            
            if (cumulativeProb > (uint)precision)
                return false;
            
            for (uint j = actProb; j < cumulativeProb; j++)
            {
                if (j >= precision)
                    return false;
                lookupTable[j] = i;
            }
            actProb = cumulativeProb;
        }
        
        if (cumulativeProb != precision)
            return false;
        
        return true;
    }
    
    public bool StartDecoding(DecoderBuffer buffer)
    {
        if (!VarintDecoding.DecodeVarint(buffer, out ulong bytesEncoded))
            return false;
        
        if (bytesEncoded > (ulong)buffer.RemainingSize)
            return false;
        
        this.buffer = buffer.GetDataAtCurrentPosition();
        int offset = (int)bytesEncoded;
        
        if (offset < 1)
            return false;
        
        byte lastByte = this.buffer[offset - 1];
        uint x = (uint)(lastByte >> 6);
        
        if (x == 0)
        {
            bufferOffset = offset - 1;
            state = (uint)(lastByte & 0x3F);
        }
        else if (x == 1)
        {
            if (offset < 2)
                return false;
            bufferOffset = offset - 2;
            state = (uint)(((this.buffer[offset - 1] & 0xFF) | ((this.buffer[offset - 2] & 0xFF) << 8)) & 0x3FFF);
        }
        else if (x == 2)
        {
            if (offset < 3)
                return false;
            bufferOffset = offset - 3;
            state = (uint)(((this.buffer[offset - 1] & 0xFF) | ((this.buffer[offset - 2] & 0xFF) << 8) | ((this.buffer[offset - 3] & 0xFF) << 16)) & 0x3FFFFF);
        }
        else
        {
            return false;
        }
        
        state += RANS_L;
        if (state >= RANS_L * 256)
            return false;
        
        buffer.Advance((int)bytesEncoded);
        
        return true;
    }
    
    public uint DecodeSymbol()
    {
        while (state < RANS_L && bufferOffset > 0)
        {
            state = state * 256 + buffer[--bufferOffset];
        }
        
        uint quotient = state / (uint)precision;
        uint remainder = state % (uint)precision;
        
        uint symbol = lookupTable[remainder];
        uint prob = probabilityTable[symbol];
        uint cumProb = cumulativeProbabilities[symbol];
        
        state = quotient * prob + remainder - cumProb;
        
        return symbol;
    }
    
    public void EndDecoding()
    {
        state = 0;
        buffer = null;
    }
}
