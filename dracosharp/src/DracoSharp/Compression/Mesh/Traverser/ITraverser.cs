namespace DracoSharp.Compression.Mesh.Traverser;

public interface ITraverser
{
    global::DracoSharp.Mesh.ICornerTable CornerTable { get; }
    void OnTraversalStart();
    void OnTraversalEnd();
    bool TraverseFromCorner(int cornerId);
}
