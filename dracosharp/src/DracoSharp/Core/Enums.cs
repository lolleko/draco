namespace DracoSharp.Core;

public enum EncodedGeometryType
{
    InvalidGeometryType = -1,
    PointCloud = 0,
    TriangularMesh = 1,
    NumEncodedGeometryTypes = 2
}

public enum PointCloudEncodingMethod
{
    SequentialEncoding = 0,
    KdTreeEncoding = 1
}

public enum MeshEncoderMethod
{
    SequentialEncoding = 0,
    EdgebreakerEncoding = 1
}

public enum AttributeEncoderType
{
    BasicAttributeEncoder = 0,
    MeshTraversalAttributeEncoder = 1,
    KdTreeAttributeEncoder = 2
}

public enum SequentialAttributeEncoderType
{
    Generic = 0,
    Integer = 1,
    Quantization = 2,
    Normals = 3
}

public enum PredictionSchemeMethod
{
    None = -2,
    Undefined = -1,
    Difference = 0,
    Parallelogram = 1,
    MultiParallelogram = 2,
    TexCoordsDeprecated = 3,
    ConstrainedMultiParallelogram = 4,
    TexCoordsPortable = 5,
    GeometricNormal = 6,
    NumSchemes = 7
}

public enum PredictionSchemeTransformType
{
    None = -1,
    Delta = 0,
    Wrap = 1,
    NormalOctahedron = 2,
    NormalOctahedronCanonicalized = 3,
    NumTypes = 4
}

public enum MeshTraversalMethod
{
    DepthFirst = 0,
    PredictionDegree = 1
}

public enum MeshEdgebreakerConnectivityEncodingMethod
{
    StandardEncoding = 0,
    PredictiveEncoding = 1,
    ValenceEncoding = 2
}

public enum EdgebreakerTopologyBitPattern
{
    C = 0x0,
    S = 0x1,
    L = 0x3,
    R = 0x5,
    E = 0x7,
    InitFace = 8,
    Invalid = 9
}

public enum NormalPredictionMode
{
    OneTriangle = 0,
    TriangleArea = 1
}

public enum SymbolCodingMethod
{
    Tagged = 0,
    Raw = 1
}

public enum AttributeTransformType
{
    InvalidTransform = -1,
    NoTransform = 0,
    QuantizationTransform = 1,
    OctahedronTransform = 2
}

public enum MeshAttributeElementType
{
    VertexAttribute = 0,
    CornerAttribute = 1,
    FaceAttribute = 2
}
