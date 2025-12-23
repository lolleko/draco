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
        int unclampedPrecision = (3 * uniqueSymbolsBitLength) / 2;
        // Clamp between 12 and 20 bits to match C++ implementation
        if (unclampedPrecision < 12)
            return 12;
        if (unclampedPrecision > 20)
            return 20;
        return unclampedPrecision;
    }
    
    public bool Create(DecoderBuffer buffer)
    {
        if (!VarintDecoding.DecodeVarint(buffer, out numSymbols))
        {
            Console.WriteLine($"[RAnsSymbolDecoder.Create] Failed to decode numSymbols varint");
            return false;
        }
        
        Console.WriteLine($"[RAnsSymbolDecoder.Create] numSymbols={numSymbols}, buffer position: {buffer.DecodedSize}");
        
        if (numSymbols / 64 > buffer.RemainingSize)
        {
            Console.WriteLine($"[RAnsSymbolDecoder.Create] numSymbols too large");
            return false;
        }
        
        probabilityTable = new uint[numSymbols];
        cumulativeProbabilities = new uint[numSymbols];
        
        if (numSymbols == 0)
        {
            Console.WriteLine($"[RAnsSymbolDecoder.Create] numSymbols==0, returning true");
            return true;
        }
        
        for (uint i = 0; i < numSymbols; i++)
        {
            if (!buffer.Decode(out byte probData))
                return false;
            
            int token = probData & 3;
            if (token == 3)
            {
                uint offset = (uint)(probData >> 2);
                Console.WriteLine($"[Create] Symbol {i}: token=3, offset={offset}, probData={probData}");
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
                Console.WriteLine($"[Create] Symbol {i}: token={token}, extraBytes={extraBytes}, probData={probData}, initial prob={prob}");
                for (int b = 0; b < extraBytes; b++)
                {
                    if (!buffer.Decode(out byte eb))
                        return false;
                    uint shift = (uint)(8 * (b + 1) - 2);
                    Console.WriteLine($"[Create] Symbol {i}: Reading extra byte {b}: eb={eb}, shift={shift}");
                    prob |= (uint)eb << (int)shift;
                }
                Console.WriteLine($"[Create] Symbol {i}: Final prob={prob}");
                probabilityTable[i] = prob;
            }
        }
        
        Console.WriteLine($"[RAnsSymbolDecoder.Create] Calling BuildLookupTable, buffer position: {buffer.DecodedSize}");
        return BuildLookupTable();
    }
    
    private bool BuildLookupTable()
    {
        lookupTable = new uint[precision];
        
        uint cumulativeProb = 0;
        uint actProb = 0;
        
        Console.WriteLine($"[BuildLookupTable] numSymbols={numSymbols}, precision={precision}");
        
        for (uint i = 0; i < numSymbols; i++)
        {
            uint prob = probabilityTable[i];
            cumulativeProbabilities[i] = cumulativeProb;
            cumulativeProb += prob;
            
            Console.WriteLine($"[BuildLookupTable] Symbol {i}: prob={prob}, cumulativeProb={cumulativeProb}");
            
            if (cumulativeProb > (uint)precision)
            {
                Console.WriteLine($"[BuildLookupTable] cumulativeProb ({cumulativeProb}) > precision ({precision})");
                return false;
            }
            
            for (uint j = actProb; j < cumulativeProb; j++)
            {
                if (j >= precision)
                {
                    Console.WriteLine($"[BuildLookupTable] j ({j}) >= precision ({precision})");
                    return false;
                }
                lookupTable[j] = i;
            }
            actProb = cumulativeProb;
        }
        
        if (cumulativeProb != precision)
        {
            Console.WriteLine($"[BuildLookupTable] cumulativeProb ({cumulativeProb}) != precision ({precision})");
            return false;
        }
        
        Console.WriteLine($"[BuildLookupTable] Success");
        return true;
    }
    
    public bool StartDecoding(DecoderBuffer buffer)
    {
        Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] Starting, buffer position: {buffer.DecodedSize}");
        
        if (!VarintDecoding.DecodeVarint(buffer, out ulong bytesEncoded))
        {
            Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] Failed to decode bytesEncoded varint");
            return false;
        }
        
        Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] bytesEncoded={bytesEncoded}, buffer.RemainingSize={buffer.RemainingSize}, buffer position: {buffer.DecodedSize}");
        
        // Validate that bytesEncoded doesn't exceed remaining buffer size
        if (bytesEncoded > (ulong)buffer.RemainingSize)
        {
            Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] bytesEncoded ({bytesEncoded}) > RemainingSize ({buffer.RemainingSize})");
            return false;
        }
        
        this.buffer = buffer.GetDataAtCurrentPosition();
        int offset = (int)bytesEncoded;
        
        Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] offset={offset}");
        
        // When num_symbols == 0 and bytesEncoded == 0, this is valid - no rANS data to decode
        if (offset == 0 && numSymbols == 0)
        {
            Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] No rANS data (numSymbols==0, bytesEncoded==0), advancing buffer");
            buffer.Advance((int)bytesEncoded);
            return true;
        }
        
        if (offset < 1)
        {
            Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] offset < 1");
            return false;
        }
        
        byte lastByte = this.buffer[offset - 1];
        uint x = (uint)(lastByte >> 6);
        
        Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] lastByte=0x{lastByte:X2}, x={x}");
        
        if (x == 0)
        {
            bufferOffset = offset - 1;
            state = (uint)(lastByte & 0x3F);
        }
        else if (x == 1)
        {
            if (offset < 2)
            {
                Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] x==1 but offset < 2");
                return false;
            }
            bufferOffset = offset - 2;
            state = (uint)((lastByte & 0x3F) | ((this.buffer[offset - 2] & 0xFF) << 6));
        }
        else if (x == 2)
        {
            if (offset < 3)
            {
                Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] x==2 but offset < 3");
                return false;
            }
            bufferOffset = offset - 3;
            state = (uint)((lastByte & 0x3F) | ((this.buffer[offset - 2] & 0xFF) << 6) | ((this.buffer[offset - 3] & 0xFF) << 14));
            Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] state before adding RANS_L: {state}, bytes: 0x{this.buffer[offset-3]:X2} 0x{this.buffer[offset-2]:X2} 0x{lastByte:X2}");
        }
        else
        {
            Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] Invalid x value: {x}");
            return false;
        }
        
        state += RANS_L;
        Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] state after adding RANS_L: {state}");
        
        // Note: C++ doesn't validate upper bound here - renormalization during DecodeSymbol
        // keeps state >= RANS_L by pulling bytes as needed
        
        buffer.Advance((int)bytesEncoded);
        
        Console.WriteLine($"[RAnsSymbolDecoder.StartDecoding] Success, buffer position after advance: {buffer.DecodedSize}");
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
