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

public class PointCloud
{
    private readonly List<PointAttribute> attributes;
    private readonly Dictionary<GeometryAttributeType, List<int>> namedAttributeIndices;

    public int NumPoints { get; set; }

    public PointCloud()
    {
        attributes = new List<PointAttribute>();
        namedAttributeIndices = new Dictionary<GeometryAttributeType, List<int>>();
        NumPoints = 0;
    }

    public int NumAttributes => attributes.Count;

    public PointAttribute GetAttribute(int attributeId)
    {
        if (attributeId < 0 || attributeId >= attributes.Count)
            return null;
        return attributes[attributeId];
    }

    public int AddAttribute(PointAttribute attribute)
    {
        int attributeId = attributes.Count;
        attributes.Add(attribute);

        if (attribute.AttributeType != GeometryAttributeType.Invalid)
        {
            if (!namedAttributeIndices.ContainsKey(attribute.AttributeType))
                namedAttributeIndices[attribute.AttributeType] = new List<int>();
            namedAttributeIndices[attribute.AttributeType].Add(attributeId);
        }

        return attributeId;
    }

    public PointAttribute GetNamedAttribute(GeometryAttributeType type, int index = 0)
    {
        if (!namedAttributeIndices.TryGetValue(type, out var indices))
            return null;
        if (index < 0 || index >= indices.Count)
            return null;
        return attributes[indices[index]];
    }

    public int GetNamedAttributeId(GeometryAttributeType type, int index = 0)
    {
        if (!namedAttributeIndices.TryGetValue(type, out var indices))
            return -1;
        if (index < 0 || index >= indices.Count)
            return -1;
        return indices[index];
    }

    public int NumNamedAttributes(GeometryAttributeType type)
    {
        if (!namedAttributeIndices.TryGetValue(type, out var indices))
            return 0;
        return indices.Count;
    }
}
