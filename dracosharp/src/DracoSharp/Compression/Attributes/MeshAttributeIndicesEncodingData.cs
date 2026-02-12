namespace DracoSharp.Compression.Attributes;

public class MeshAttributeIndicesEncodingData
{
    public List<int> EncodedAttributeValueIndexToCornerMap { get; } = [];
    public int[] VertexToEncodedAttributeValueIndexMap { get; private set; } = [];
    public int NumValues { get; set; }

    public void Init(int numVertices)
    {
        VertexToEncodedAttributeValueIndexMap = new int[numVertices];
        Array.Fill(VertexToEncodedAttributeValueIndexMap, -1);
        EncodedAttributeValueIndexToCornerMap.Clear();
        EncodedAttributeValueIndexToCornerMap.EnsureCapacity(numVertices);
        NumValues = 0;
    }
}
