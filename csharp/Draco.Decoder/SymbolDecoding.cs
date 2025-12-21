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

public enum SymbolCodingMethod : byte
{
    Tagged = 0,
    Raw = 1
}

public static class SymbolDecoding
{
    public static bool DecodeSymbols(uint numValues, int numComponents, DecoderBuffer buffer, uint[] outValues)
    {
        if (numValues == 0)
            return true;
        
        Console.WriteLine($"[SymbolDecoding.DecodeSymbols] Starting, numValues={numValues}, numComponents={numComponents}, buffer position: {buffer.DecodedSize}");
        
        if (!buffer.Decode(out byte scheme))
        {
            Console.WriteLine($"[SymbolDecoding.DecodeSymbols] Failed to read scheme byte");
            return false;
        }
        
        Console.WriteLine($"[SymbolDecoding.DecodeSymbols] scheme={scheme}");
        
        if (scheme == (byte)SymbolCodingMethod.Tagged)
        {
            Console.WriteLine($"[SymbolDecoding.DecodeSymbols] Using Tagged decoding");
            return DecodeTaggedSymbols(numValues, numComponents, buffer, outValues);
        }
        else if (scheme == (byte)SymbolCodingMethod.Raw)
        {
            Console.WriteLine($"[SymbolDecoding.DecodeSymbols] Using Raw decoding");
            return DecodeRawSymbols(numValues, buffer, outValues);
        }
        
        Console.WriteLine($"[SymbolDecoding.DecodeSymbols] Unknown scheme: {scheme}");
        return false;
    }
    
    private static bool DecodeTaggedSymbols(uint numValues, int numComponents, DecoderBuffer buffer, uint[] outValues)
    {
        Console.WriteLine($"[DecodeTaggedSymbols] Starting, buffer position: {buffer.DecodedSize}");
        
        var tagDecoder = new RAnsSymbolDecoder(5);
        Console.WriteLine($"[DecodeTaggedSymbols] Created RAnsSymbolDecoder with maxBitLength=5");
        
        if (!tagDecoder.Create(buffer))
        {
            Console.WriteLine($"[DecodeTaggedSymbols] tagDecoder.Create failed at buffer position: {buffer.DecodedSize}");
            return false;
        }
        
        Console.WriteLine($"[DecodeTaggedSymbols] tagDecoder.Create succeeded, buffer position: {buffer.DecodedSize}");
        
        if (!tagDecoder.StartDecoding(buffer))
        {
            Console.WriteLine($"[DecodeTaggedSymbols] tagDecoder.StartDecoding failed at buffer position: {buffer.DecodedSize}");
            return false;
        }
        
        Console.WriteLine($"[DecodeTaggedSymbols] tagDecoder.StartDecoding succeeded, NumSymbols={tagDecoder.NumSymbols}, buffer position: {buffer.DecodedSize}");
        
        if (numValues > 0 && tagDecoder.NumSymbols == 0)
        {
            Console.WriteLine($"[DecodeTaggedSymbols] NumSymbols is 0 but numValues={numValues}");
            return false;
        }
        
        if (!buffer.StartBitDecoding(false, out ulong _))
        {
            Console.WriteLine($"[DecodeTaggedSymbols] StartBitDecoding failed");
            return false;
        }
        
        Console.WriteLine($"[DecodeTaggedSymbols] StartBitDecoding succeeded, buffer position: {buffer.DecodedSize}");
        
        int valueId = 0;
        for (uint i = 0; i < numValues; i += (uint)numComponents)
        {
            uint bitLength = tagDecoder.DecodeSymbol();
            
            if (i < 3)
                Console.WriteLine($"[DecodeTaggedSymbols] Iteration {i}: bitLength={bitLength}");
            
            for (int j = 0; j < numComponents; j++)
            {
                if (!buffer.DecodeLeastSignificantBits32((int)bitLength, out uint val))
                {
                    Console.WriteLine($"[DecodeTaggedSymbols] DecodeLeastSignificantBits32 failed at valueId={valueId}, iteration={i}, component={j}");
                    return false;
                }
                
                outValues[valueId++] = val;
            }
        }
        
        Console.WriteLine($"[DecodeTaggedSymbols] Decoded all {valueId} values successfully");
        
        tagDecoder.EndDecoding();
        buffer.EndBitDecoding();
        
        Console.WriteLine($"[DecodeTaggedSymbols] Success, buffer position: {buffer.DecodedSize}");
        return true;
    }
    
    private static bool DecodeRawSymbols(uint numValues, DecoderBuffer buffer, uint[] outValues)
    {
        if (!buffer.Decode(out byte maxBitLength))
            return false;
        
        var decoder = new RAnsSymbolDecoder(maxBitLength);
        if (!decoder.Create(buffer))
            return false;
        
        if (numValues > 0 && decoder.NumSymbols == 0)
            return false;
        
        if (!decoder.StartDecoding(buffer))
            return false;
        
        for (uint i = 0; i < numValues; i++)
        {
            outValues[i] = decoder.DecodeSymbol();
        }
        
        decoder.EndDecoding();
        
        return true;
    }
}
