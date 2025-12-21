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
        
        if (!buffer.Decode(out byte scheme))
            return false;
        
        if (scheme == (byte)SymbolCodingMethod.Tagged)
        {
            return DecodeTaggedSymbols(numValues, numComponents, buffer, outValues);
        }
        else if (scheme == (byte)SymbolCodingMethod.Raw)
        {
            return DecodeRawSymbols(numValues, buffer, outValues);
        }
        
        return false;
    }
    
    private static bool DecodeTaggedSymbols(uint numValues, int numComponents, DecoderBuffer buffer, uint[] outValues)
    {
        var tagDecoder = new RAnsSymbolDecoder(5);
        if (!tagDecoder.Create(buffer))
            return false;
        
        if (!tagDecoder.StartDecoding(buffer))
            return false;
        
        if (numValues > 0 && tagDecoder.NumSymbols == 0)
            return false;
        
        if (!buffer.StartBitDecoding(false, out ulong _))
            return false;
        
        int valueId = 0;
        for (uint i = 0; i < numValues; i += (uint)numComponents)
        {
            uint bitLength = tagDecoder.DecodeSymbol();
            
            for (int j = 0; j < numComponents; j++)
            {
                if (!buffer.DecodeLeastSignificantBits32((int)bitLength, out uint val))
                    return false;
                
                outValues[valueId++] = val;
            }
        }
        
        tagDecoder.EndDecoding();
        buffer.EndBitDecoding();
        
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
