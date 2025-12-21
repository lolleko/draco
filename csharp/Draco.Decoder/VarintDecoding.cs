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

public static class VarintDecoding
{
    public static bool DecodeVarint(DecoderBuffer buffer, out uint value)
    {
        value = 0;
        byte b;
        int shift = 0;
        
        do
        {
            if (!buffer.Decode(out b))
                return false;
            
            value |= (uint)(b & 0x7F) << shift;
            shift += 7;
            
            if (shift > 35)
                return false;
                
        } while ((b & 0x80) != 0);
        
        return true;
    }
    
    public static bool DecodeVarintSigned(DecoderBuffer buffer, out int value)
    {
        if (!DecodeVarint(buffer, out uint uvalue))
        {
            value = 0;
            return false;
        }
        
        value = (int)((uvalue >> 1) ^ (-(int)(uvalue & 1)));
        return true;
    }
}
