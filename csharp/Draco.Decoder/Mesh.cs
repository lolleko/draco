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

public readonly struct Face
{
    public int V0 { get; }
    public int V1 { get; }
    public int V2 { get; }

    public Face(int v0, int v1, int v2)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
    }

    public int this[int index] => index switch
    {
        0 => V0,
        1 => V1,
        2 => V2,
        _ => throw new IndexOutOfRangeException()
    };
}

public class Mesh : PointCloud
{
    private readonly List<Face> faces;

    public Mesh() : base()
    {
        faces = new List<Face>();
    }

    public int NumFaces => faces.Count;

    public void AddFace(Face face) => faces.Add(face);

    public void AddFace(int[] indices)
    {
        if (indices == null || indices.Length != 3)
            throw new ArgumentException("Face must have exactly 3 indices");
        faces.Add(new Face(indices[0], indices[1], indices[2]));
    }

    public void SetFace(int faceId, Face face)
    {
        if (faceId >= faces.Count)
        {
            int toAdd = faceId - faces.Count + 1;
            for (int i = 0; i < toAdd; i++)
                faces.Add(new Face(0, 0, 0));
        }
        faces[faceId] = face;
    }

    public Face GetFace(int faceId)
    {
        if (faceId < 0 || faceId >= faces.Count)
            throw new IndexOutOfRangeException();
        return faces[faceId];
    }

    public void SetNumFaces(int numFaces)
    {
        while (faces.Count < numFaces)
            faces.Add(new Face(0, 0, 0));
        while (faces.Count > numFaces)
            faces.RemoveAt(faces.Count - 1);
    }

    public int[] GetIndices()
    {
        int[] indices = new int[faces.Count * 3];
        for (int i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            indices[i * 3] = face.V0;
            indices[i * 3 + 1] = face.V1;
            indices[i * 3 + 2] = face.V2;
        }
        return indices;
    }

    public void SetNumPoints(int numPoints)
    {
        NumPoints = numPoints;
    }
}
