using DracoSharp.Mesh;

namespace DracoSharp.Compression.PredictionSchemes;

public class MeshPredictionSchemeData
{
    public ICornerTable CornerTable { get; private set; }
    public int[] VertexToDataMap { get; private set; }
    public List<int> DataToCornerMap { get; private set; }

    public void Set(ICornerTable cornerTable, List<int> dataToCornerMap,
        int[] vertexToDataMap)
    {
        CornerTable = cornerTable;
        DataToCornerMap = dataToCornerMap;
        VertexToDataMap = vertexToDataMap;
    }

    public bool IsInitialized =>
        CornerTable != null && VertexToDataMap != null && DataToCornerMap != null;
}
