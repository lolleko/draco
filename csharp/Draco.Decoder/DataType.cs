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

public enum DataType
{
    Invalid = 0,
    Int8 = 1,
    UInt8 = 2,
    Int16 = 3,
    UInt16 = 4,
    Int32 = 5,
    UInt32 = 6,
    Int64 = 7,
    UInt64 = 8,
    Float32 = 9,
    Float64 = 10,
    Bool = 11
}

public static class DataTypeExtensions
{
    public static int GetSize(this DataType dataType) => dataType switch
    {
        DataType.Int8 or DataType.UInt8 or DataType.Bool => 1,
        DataType.Int16 or DataType.UInt16 => 2,
        DataType.Int32 or DataType.UInt32 or DataType.Float32 => 4,
        DataType.Int64 or DataType.UInt64 or DataType.Float64 => 8,
        _ => 0
    };
}
