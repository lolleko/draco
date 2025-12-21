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

public class PointAttribute
{
    public GeometryAttributeType AttributeType { get; set; }
    public DataType DataType { get; set; }
    public int NumComponents { get; set; }
    public int UniqueId { get; set; }
    public byte[] Data { get; set; }
    public int[] IndexMap { get; set; }

    public PointAttribute()
    {
        AttributeType = GeometryAttributeType.Invalid;
        DataType = DataType.Invalid;
        NumComponents = 0;
        UniqueId = 0;
        Data = Array.Empty<byte>();
        IndexMap = Array.Empty<int>();
    }

    public void Init(GeometryAttributeType attributeType, DataType dataType, int numComponents, int numValues)
    {
        AttributeType = attributeType;
        DataType = dataType;
        NumComponents = numComponents;
        int valueSize = dataType.GetSize() * numComponents;
        Data = new byte[numValues * valueSize];
        IndexMap = new int[numValues];
        for (int i = 0; i < numValues; i++)
            IndexMap[i] = i;
    }

    public int ValueSize => DataType.GetSize() * NumComponents;
    public int NumValues => Data.Length / ValueSize;

    public ReadOnlySpan<byte> GetValue(int index)
    {
        if (index < 0 || index >= NumValues)
            throw new ArgumentOutOfRangeException(nameof(index));

        int actualIndex = index < IndexMap.Length ? IndexMap[index] : index;
        int offset = actualIndex * ValueSize;
        return Data.AsSpan(offset, ValueSize);
    }

    public void SetValue(int index, ReadOnlySpan<byte> value)
    {
        if (index < 0 || index >= NumValues)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (value.Length != ValueSize)
            throw new ArgumentException($"Value size must be {ValueSize} bytes", nameof(value));

        int actualIndex = index < IndexMap.Length ? IndexMap[index] : index;
        int offset = actualIndex * ValueSize;
        value.CopyTo(Data.AsSpan(offset, ValueSize));
    }
}
