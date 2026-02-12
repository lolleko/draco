namespace DracoSharp.Mesh;

public interface ICornerTable
{
    int NumVertices { get; }
    int NumFaces { get; }
    int NumCorners { get; }
    int Vertex(int corner);
    int Next(int corner);
    int Previous(int corner);
    int Opposite(int corner);
    int Face(int corner);
    int LeftMostCorner(int vertex);
    int GetLeftCorner(int corner);
    int GetRightCorner(int corner);
    int SwingRight(int corner);
    int SwingLeft(int corner);
    bool IsOnBoundary(int vertex);
}
