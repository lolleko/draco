using DracoSharp.Compression.Attributes;
using DracoSharp.Compression.Mesh.Traverser;
using DracoSharp.Core;
using DracoSharp.Mesh;

namespace DracoSharp.Compression.Mesh;

public class MeshEdgebreakerDecoderImpl
{
    private MeshEdgebreakerDecoder _decoder;
    private CornerTable _cornerTable;
    private readonly List<int> _cornerTraversalStack = [];
    private readonly List<int> _vertexTraversalLength = [];
    private readonly List<TopologySplitEventData> _topologySplitData = [];
    private readonly List<HoleEventData> _holeEventData = [];
    private readonly List<bool> _initFaceConfigurations = [];
    private readonly List<int> _initCorners = [];
    private bool[] _isVertHole = [];
    private readonly Dictionary<int, int> _newToParentVertexMap = [];
    private int _numEncodedVertices;
    private readonly List<int> _processedCornerIds = [];
    private readonly List<int> _processedConnectivityCorners = [];
    private MeshAttributeIndicesEncodingData _posEncodingData = new();
    private int _posDataDecoderId = -1;
    private readonly List<AttributeData> _attributeData = [];
    private MeshEdgebreakerTraversalDecoder _traversalDecoder;
    private int _numNewVertices;
    private int _lastSymbolId = -1;
    private int _lastFaceId = -1;
    private int _lastVertId = -1;

    public MeshEdgebreakerDecoder Decoder => _decoder;
    public CornerTable CornerTable => _cornerTable;

    public bool Init(MeshEdgebreakerDecoder decoder)
    {
        _decoder = decoder;
        return true;
    }

    public void SetTraversalDecoder(MeshEdgebreakerTraversalDecoder traversalDecoder)
    {
        _traversalDecoder = traversalDecoder;
    }

    public MeshAttributeCornerTable GetAttributeCornerTable(int attId)
    {
        for (int i = 0; i < _attributeData.Count; i++)
        {
            int decoderId = _attributeData[i].DecoderId;
            if (decoderId < 0 || decoderId >= _decoder.NumAttributesDecoders)
                continue;
            var dec = _decoder.GetAttributesDecoder(decoderId);
            for (int j = 0; j < dec.NumAttributes; j++)
            {
                if (dec.GetAttributeId(j) == attId)
                {
                    if (_attributeData[i].IsConnectivityUsed)
                        return _attributeData[i].ConnectivityData;
                    return null;
                }
            }
        }
        return null;
    }

    public MeshAttributeIndicesEncodingData GetAttributeEncodingData(int attId)
    {
        for (int i = 0; i < _attributeData.Count; i++)
        {
            int decoderId = _attributeData[i].DecoderId;
            if (decoderId < 0 || decoderId >= _decoder.NumAttributesDecoders)
                continue;
            var dec = _decoder.GetAttributesDecoder(decoderId);
            for (int j = 0; j < dec.NumAttributes; j++)
            {
                if (dec.GetAttributeId(j) == attId)
                    return _attributeData[i].EncodingData;
            }
        }
        return _posEncodingData;
    }

    public bool CreateAttributesDecoder(int attDecoderId)
    {
        if (!_decoder.Buffer.Decode(out sbyte attDataId))
            return false;
        if (!_decoder.Buffer.Decode(out byte decoderType))
            return false;

        if (attDataId >= 0)
        {
            if (attDataId >= _attributeData.Count)
                return false;
            if (_attributeData[attDataId].DecoderId >= 0)
                return false;
            _attributeData[attDataId].DecoderId = attDecoderId;
        }
        else
        {
            if (_posDataDecoderId >= 0)
                return false;
            _posDataDecoderId = attDecoderId;
        }

        byte traversalMethodEncoded = 0; // Default: MESH_TRAVERSAL_DEPTH_FIRST
        if (_decoder.BitstreamVersion >= BitstreamVersion.Make(1, 2))
        {
            if (!_decoder.Buffer.Decode(out traversalMethodEncoded))
                return false;
            if (traversalMethodEncoded >= 2) // NUM_TRAVERSAL_METHODS
                return false;
        }

        var mesh = _decoder.Mesh;
        PointsSequencer sequencer;
        const byte MESH_VERTEX_ATTRIBUTE = 0;

        if (decoderType == MESH_VERTEX_ATTRIBUTE)
        {
            MeshAttributeIndicesEncodingData encodingData;
            if (attDataId < 0)
            {
                encodingData = _posEncodingData;
            }
            else
            {
                encodingData = _attributeData[attDataId].EncodingData;
                _attributeData[attDataId].IsConnectivityUsed = false;
            }
            sequencer = CreateVertexTraversalSequencer(
                encodingData, mesh, _cornerTable, traversalMethodEncoded);
        }
        else
        {
            if (traversalMethodEncoded != 0) // Only depth-first for corner attributes
                return false;
            if (attDataId < 0)
                return false;
            var encodingData = _attributeData[attDataId].EncodingData;
            var cornerTableForAtt = _attributeData[attDataId].ConnectivityData;
            sequencer = CreateVertexTraversalSequencer(encodingData, mesh, cornerTableForAtt);
        }

        if (sequencer == null)
            return false;

        var attController = new SequentialAttributeDecodersController(sequencer);
        return _decoder.SetAttributesDecoder(attDecoderId, attController);
    }

    private PointsSequencer CreateVertexTraversalSequencer(
        MeshAttributeIndicesEncodingData encodingData,
        DracoSharp.Mesh.Mesh mesh, ICornerTable cornerTable,
        byte traversalMethod = 0)
    {
        var traversalSequencer = new MeshTraversalSequencer(mesh, encodingData);
        var observer = new MeshAttributeIndicesEncodingObserver(
            cornerTable, mesh, traversalSequencer, encodingData);

        ITraverser traverser;
        if (traversalMethod == 1) // MESH_TRAVERSAL_PREDICTION_DEGREE
        {
            var predDegTraverser = new MaxPredictionDegreeTraverser();
            predDegTraverser.Init(cornerTable, observer);
            traverser = predDegTraverser;
        }
        else // MESH_TRAVERSAL_DEPTH_FIRST
        {
            var dfsTraverser = new DepthFirstTraverser();
            dfsTraverser.Init(cornerTable, observer);
            traverser = dfsTraverser;
        }

        traversalSequencer.SetTraverser(traverser);
        return traversalSequencer;
    }

    private bool DecodeUint32(out uint value)
    {
        if (_decoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
            return _decoder.Buffer.Decode(out value);
        return _decoder.Buffer.DecodeVarint(out value);
    }

    public bool DecodeConnectivity()
    {
        _numNewVertices = 0;
        _newToParentVertexMap.Clear();

        // Backward compat: version < 2.2 stores num_new_verts.
        if (_decoder.BitstreamVersion < BitstreamVersion.Make(2, 2))
        {
            if (!DecodeUint32(out uint numNewVerts))
                return false;
            _numNewVertices = (int)numNewVerts;
        }

        if (!DecodeUint32(out uint numEncodedVertices))
            return false;
        _numEncodedVertices = (int)numEncodedVertices;

        if (!DecodeUint32(out uint numFaces))
            return false;
        if (numFaces > int.MaxValue / 3)
            return false;
        if ((uint)_numEncodedVertices > numFaces * 3)
            return false;

        uint minNumFaceEdges = 3 * numFaces / 2;
        ulong numEncodedVertices64 = (ulong)_numEncodedVertices;
        ulong maxNumVertexEdges = numEncodedVertices64 * (numEncodedVertices64 - 1) / 2;
        if (maxNumVertexEdges < minNumFaceEdges)
            return false;

        if (!_decoder.Buffer.Decode(out byte numAttributeData))
            return false;

        if (!DecodeUint32(out uint numEncodedSymbols))
            return false;
        if (numFaces < numEncodedSymbols)
            return false;
        uint maxEncodedFaces = numEncodedSymbols + numEncodedSymbols / 3;
        if (numFaces > maxEncodedFaces)
            return false;

        if (!DecodeUint32(out uint numEncodedSplitSymbols))
            return false;
        if (numEncodedSplitSymbols > numEncodedSymbols)
            return false;

        // Decode topology.
        _vertexTraversalLength.Clear();
        _cornerTable = new CornerTable();
        _processedCornerIds.Clear();
        _processedConnectivityCorners.Clear();
        _topologySplitData.Clear();
        _holeEventData.Clear();
        _initFaceConfigurations.Clear();
        _initCorners.Clear();
        _lastSymbolId = -1;
        _lastFaceId = -1;
        _lastVertId = -1;
        _attributeData.Clear();
        for (int i = 0; i < numAttributeData; i++)
            _attributeData.Add(new AttributeData());

        if (!_cornerTable.Reset((int)numFaces, _numEncodedVertices + (int)numEncodedSplitSymbols))
            return false;

        _isVertHole = new bool[_numEncodedVertices + (int)numEncodedSplitSymbols];
        Array.Fill(_isVertHole, true);

        int topologySplitDecodedBytes = -1;
        if (_decoder.BitstreamVersion < BitstreamVersion.Make(2, 2))
        {
            // For older versions, hole and topology split events are stored
            // after the connectivity data. We need to decode them from a
            // separate buffer.
            uint encodedConnectivitySize;
            if (_decoder.BitstreamVersion < BitstreamVersion.Make(2, 0))
            {
                if (!_decoder.Buffer.Decode(out encodedConnectivitySize))
                    return false;
            }
            else
            {
                if (!_decoder.Buffer.DecodeVarint(out encodedConnectivitySize))
                    return false;
            }
            if (encodedConnectivitySize == 0 ||
                encodedConnectivitySize > (uint)_decoder.Buffer.RemainingSize)
                return false;

            var eventBuffer = new DecoderBuffer();
            var remaining = _decoder.Buffer.DataHead;
            eventBuffer.Init(
                remaining.Slice((int)encodedConnectivitySize),
                _decoder.Buffer.BitstreamVersion);
            topologySplitDecodedBytes =
                DecodeHoleAndTopologySplitEvents(eventBuffer);
            if (topologySplitDecodedBytes == -1)
                return false;
        }
        else
        {
            if (DecodeHoleAndTopologySplitEvents(_decoder.Buffer) == -1)
                return false;
        }

        _traversalDecoder.Init(this);
        _traversalDecoder.SetNumEncodedVertices(_numEncodedVertices + (int)numEncodedSplitSymbols);
        _traversalDecoder.SetNumAttributeData(numAttributeData);

        if (!_traversalDecoder.Start(out var traversalEndBuffer))
            return false;

        int numConnectivityVerts = DecodeConnectivityInternal((int)numEncodedSymbols);
        if (numConnectivityVerts == -1)
            return false;

        // Set the main buffer to the end of the traversal.
        _decoder.Buffer.Init(traversalEndBuffer.DataHead, _decoder.Buffer.BitstreamVersion);

        if (_decoder.BitstreamVersion < BitstreamVersion.Make(2, 2))
        {
            // Skip topology split data that was already decoded earlier.
            _decoder.Buffer.Advance(topologySplitDecodedBytes);
        }

        // Decode attribute connectivity.
        if (_attributeData.Count > 0)
        {
            if (_decoder.BitstreamVersion < BitstreamVersion.Make(2, 1))
            {
                for (int ci = 0; ci < _cornerTable.NumCorners; ci += 3)
                {
                    if (!DecodeAttributeConnectivitiesOnFaceLegacy(ci))
                        return false;
                }
            }
            else
            {
                for (int ci = 0; ci < _cornerTable.NumCorners; ci += 3)
                {
                    if (!DecodeAttributeConnectivitiesOnFace(ci))
                        return false;
                }
            }
        }
        _traversalDecoder.Done();

        // Prepare attribute connectivity data.
        for (int i = 0; i < _attributeData.Count; i++)
        {
            _attributeData[i].ConnectivityData.InitEmpty(_cornerTable);
            foreach (int c in _attributeData[i].AttributeSeamCorners)
                _attributeData[i].ConnectivityData.AddSeamEdge(c);
            if (!_attributeData[i].ConnectivityData.RecomputeVertices())
                return false;
        }

        _posEncodingData.Init(_cornerTable.NumVertices);
        for (int i = 0; i < _attributeData.Count; i++)
        {
            int attConnectivityVerts = _attributeData[i].ConnectivityData.NumVertices;
            if (attConnectivityVerts < _cornerTable.NumVertices)
                attConnectivityVerts = _cornerTable.NumVertices;
            _attributeData[i].EncodingData.Init(attConnectivityVerts);
        }

        if (!AssignPointsToCorners(numConnectivityVerts))
            return false;
        return true;
    }

    public bool OnAttributesDecoded() => true;

    private int DecodeConnectivityInternal(int numSymbols)
    {
        var activeCornerStack = new List<int>();
        var topologySplitActiveCorners = new Dictionary<int, int>();
        var invalidVertices = new List<int>();
        bool removeInvalidVertices = _attributeData.Count == 0;
        int maxNumVertices = _isVertHole.Length;
        int numFaces = 0;

        for (int symbolId = 0; symbolId < numSymbols; symbolId++)
        {
            int face = numFaces++;
            bool checkTopologySplit = false;
            uint symbol = _traversalDecoder.DecodeSymbol();

            if (symbol == (uint)EdgebreakerTopologyBitPattern.C)
            {
                if (activeCornerStack.Count == 0)
                    return -1;
                int cornerA = activeCornerStack[^1];
                int vertexX = _cornerTable.Vertex(_cornerTable.Next(cornerA));
                int cornerB = _cornerTable.Next(_cornerTable.LeftMostCorner(vertexX));
                if (cornerA == cornerB)
                    return -1;
                if (_cornerTable.Opposite(cornerA) != CornerTable.kInvalidCornerIndex ||
                    _cornerTable.Opposite(cornerB) != CornerTable.kInvalidCornerIndex)
                    return -1;

                int corner = 3 * face;
                _cornerTable.SetOppositeCorners(cornerA, corner + 1);
                _cornerTable.SetOppositeCorners(cornerB, corner + 2);

                int vertAPrev = _cornerTable.Vertex(_cornerTable.Previous(cornerA));
                int vertBNext = _cornerTable.Vertex(_cornerTable.Next(cornerB));
                if (vertexX == vertAPrev || vertexX == vertBNext)
                    return -1;

                _cornerTable.MapCornerToVertex(corner, vertexX);
                _cornerTable.MapCornerToVertex(corner + 1, vertBNext);
                _cornerTable.MapCornerToVertex(corner + 2, vertAPrev);
                _cornerTable.SetLeftMostCorner(vertAPrev, corner + 2);
                _isVertHole[vertexX] = false;
                activeCornerStack[^1] = corner;
            }
            else if (symbol == (uint)EdgebreakerTopologyBitPattern.R ||
                     symbol == (uint)EdgebreakerTopologyBitPattern.L)
            {
                if (activeCornerStack.Count == 0)
                    return -1;
                int cornerA = activeCornerStack[^1];
                if (_cornerTable.Opposite(cornerA) != CornerTable.kInvalidCornerIndex)
                    return -1;

                int corner = 3 * face;
                int oppCorner, cornerL, cornerR;
                if (symbol == (uint)EdgebreakerTopologyBitPattern.R)
                {
                    oppCorner = corner + 2;
                    cornerL = corner + 1;
                    cornerR = corner;
                }
                else
                {
                    oppCorner = corner + 1;
                    cornerL = corner;
                    cornerR = corner + 2;
                }
                _cornerTable.SetOppositeCorners(oppCorner, cornerA);
                int newVertIndex = _cornerTable.AddNewVertex();
                if (_cornerTable.NumVertices > maxNumVertices)
                    return -1;
                _cornerTable.MapCornerToVertex(oppCorner, newVertIndex);
                _cornerTable.SetLeftMostCorner(newVertIndex, oppCorner);

                int vertexR = _cornerTable.Vertex(_cornerTable.Previous(cornerA));
                _cornerTable.MapCornerToVertex(cornerR, vertexR);
                _cornerTable.SetLeftMostCorner(vertexR, cornerR);
                _cornerTable.MapCornerToVertex(cornerL, _cornerTable.Vertex(_cornerTable.Next(cornerA)));
                activeCornerStack[^1] = corner;
                checkTopologySplit = true;
            }
            else if (symbol == (uint)EdgebreakerTopologyBitPattern.S)
            {
                if (activeCornerStack.Count == 0)
                    return -1;
                int cornerB = activeCornerStack[^1];
                activeCornerStack.RemoveAt(activeCornerStack.Count - 1);

                if (topologySplitActiveCorners.TryGetValue(symbolId, out int splitCorner))
                    activeCornerStack.Add(splitCorner);

                if (activeCornerStack.Count == 0)
                    return -1;
                int cornerA = activeCornerStack[^1];
                if (cornerA == cornerB)
                    return -1;
                if (_cornerTable.Opposite(cornerA) != CornerTable.kInvalidCornerIndex ||
                    _cornerTable.Opposite(cornerB) != CornerTable.kInvalidCornerIndex)
                    return -1;

                int corner = 3 * face;
                _cornerTable.SetOppositeCorners(cornerA, corner + 2);
                _cornerTable.SetOppositeCorners(cornerB, corner + 1);

                int vertexP = _cornerTable.Vertex(_cornerTable.Previous(cornerA));
                _cornerTable.MapCornerToVertex(corner, vertexP);
                _cornerTable.MapCornerToVertex(corner + 1, _cornerTable.Vertex(_cornerTable.Next(cornerA)));
                int vertBPrev = _cornerTable.Vertex(_cornerTable.Previous(cornerB));
                _cornerTable.MapCornerToVertex(corner + 2, vertBPrev);
                _cornerTable.SetLeftMostCorner(vertBPrev, corner + 2);

                int cornerN = _cornerTable.Next(cornerB);
                int vertexN = _cornerTable.Vertex(cornerN);
                _traversalDecoder.MergeVertices(vertexP, vertexN);
                _cornerTable.SetLeftMostCorner(vertexP, _cornerTable.LeftMostCorner(vertexN));

                int firstCorner = cornerN;
                while (cornerN != CornerTable.kInvalidCornerIndex)
                {
                    _cornerTable.MapCornerToVertex(cornerN, vertexP);
                    cornerN = _cornerTable.SwingLeft(cornerN);
                    if (cornerN == firstCorner)
                        return -1;
                }
                _cornerTable.MakeVertexIsolated(vertexN);
                if (removeInvalidVertices)
                    invalidVertices.Add(vertexN);
                activeCornerStack[^1] = corner;
            }
            else if (symbol == (uint)EdgebreakerTopologyBitPattern.E)
            {
                int corner = 3 * face;
                int firstVertIndex = _cornerTable.AddNewVertex();
                _cornerTable.MapCornerToVertex(corner, firstVertIndex);
                _cornerTable.MapCornerToVertex(corner + 1, _cornerTable.AddNewVertex());
                _cornerTable.MapCornerToVertex(corner + 2, _cornerTable.AddNewVertex());
                if (_cornerTable.NumVertices > maxNumVertices)
                    return -1;
                _cornerTable.SetLeftMostCorner(firstVertIndex, corner);
                _cornerTable.SetLeftMostCorner(firstVertIndex + 1, corner + 1);
                _cornerTable.SetLeftMostCorner(firstVertIndex + 2, corner + 2);
                activeCornerStack.Add(corner);
                checkTopologySplit = true;
            }
            else
            {
                return -1;
            }

            _traversalDecoder.NewActiveCornerReached(activeCornerStack[^1]);

            if (checkTopologySplit)
            {
                int encoderSymbolId = numSymbols - symbolId - 1;
                while (IsTopologySplit(encoderSymbolId, out int splitEdge, out int encoderSplitSymbolId))
                {
                    if (encoderSplitSymbolId < 0)
                        return -1;
                    int actTopCorner = activeCornerStack[^1];
                    int newActiveCorner = splitEdge == 1 // RIGHT_FACE_EDGE
                        ? _cornerTable.Next(actTopCorner)
                        : _cornerTable.Previous(actTopCorner);
                    int decoderSplitSymbolId = numSymbols - encoderSplitSymbolId - 1;
                    topologySplitActiveCorners[decoderSplitSymbolId] = newActiveCorner;
                }
            }
        }

        if (_cornerTable.NumVertices > maxNumVertices)
            return -1;

        // Decode start faces.
        while (activeCornerStack.Count > 0)
        {
            int corner = activeCornerStack[^1];
            activeCornerStack.RemoveAt(activeCornerStack.Count - 1);
            bool interiorFace = _traversalDecoder.DecodeStartFaceConfiguration();
            if (interiorFace)
            {
                if (numFaces >= _cornerTable.NumFaces)
                    return -1;

                int cornerA = corner;
                int vertN = _cornerTable.Vertex(_cornerTable.Next(cornerA));
                int cornerB = _cornerTable.Next(_cornerTable.LeftMostCorner(vertN));
                int vertX = _cornerTable.Vertex(_cornerTable.Next(cornerB));
                int cornerC = _cornerTable.Next(_cornerTable.LeftMostCorner(vertX));

                if (corner == cornerB || corner == cornerC || cornerB == cornerC)
                    return -1;
                if (_cornerTable.Opposite(corner) != CornerTable.kInvalidCornerIndex ||
                    _cornerTable.Opposite(cornerB) != CornerTable.kInvalidCornerIndex ||
                    _cornerTable.Opposite(cornerC) != CornerTable.kInvalidCornerIndex)
                    return -1;

                int vertP = _cornerTable.Vertex(_cornerTable.Next(cornerC));
                int faceIdx = numFaces++;
                int newCorner = 3 * faceIdx;
                _cornerTable.SetOppositeCorners(newCorner, corner);
                _cornerTable.SetOppositeCorners(newCorner + 1, cornerB);
                _cornerTable.SetOppositeCorners(newCorner + 2, cornerC);
                _cornerTable.MapCornerToVertex(newCorner, vertX);
                _cornerTable.MapCornerToVertex(newCorner + 1, vertP);
                _cornerTable.MapCornerToVertex(newCorner + 2, vertN);

                for (int ci = 0; ci < 3; ci++)
                    _isVertHole[_cornerTable.Vertex(newCorner + ci)] = false;

                _initFaceConfigurations.Add(true);
                _initCorners.Add(newCorner);
            }
            else
            {
                _initFaceConfigurations.Add(false);
                _initCorners.Add(corner);
            }
        }

        if (numFaces != _cornerTable.NumFaces)
            return -1;

        int numVertices = _cornerTable.NumVertices;
        foreach (int invalidVert in invalidVertices)
        {
            int srcVert = numVertices - 1;
            while (_cornerTable.LeftMostCorner(srcVert) == CornerTable.kInvalidCornerIndex)
                srcVert = --numVertices - 1;
            if (srcVert < invalidVert)
                continue;

            // Remap corners mapped to srcVert to invalidVert.
            int c = _cornerTable.LeftMostCorner(srcVert);
            int startC = c;
            while (c != CornerTable.kInvalidCornerIndex)
            {
                if (_cornerTable.Vertex(c) != srcVert)
                    return -1;
                _cornerTable.MapCornerToVertex(c, invalidVert);
                c = _cornerTable.SwingRight(c);
                if (c == startC)
                    break;
            }
            _cornerTable.SetLeftMostCorner(invalidVert, _cornerTable.LeftMostCorner(srcVert));
            _cornerTable.MakeVertexIsolated(srcVert);
            _isVertHole[invalidVert] = _isVertHole[srcVert];
            _isVertHole[srcVert] = false;
            numVertices--;
        }
        return numVertices;
    }

    private bool IsTopologySplit(int encoderSymbolId, out int sourceEdge, out int encoderSplitSymbolId)
    {
        sourceEdge = 0;
        encoderSplitSymbolId = -1;
        if (_topologySplitData.Count == 0)
            return false;
        if (_topologySplitData[^1].SourceSymbolId > (uint)encoderSymbolId)
        {
            encoderSplitSymbolId = -1;
            return true;
        }
        if (_topologySplitData[^1].SourceSymbolId != encoderSymbolId)
            return false;
        sourceEdge = (int)_topologySplitData[^1].SourceEdge;
        encoderSplitSymbolId = (int)_topologySplitData[^1].SplitSymbolId;
        _topologySplitData.RemoveAt(_topologySplitData.Count - 1);
        return true;
    }

    private int DecodeHoleAndTopologySplitEvents(DecoderBuffer buffer)
    {
        ushort version = _decoder.BitstreamVersion;

        uint numTopologySplits;
        if (version < BitstreamVersion.Make(2, 0))
        {
            if (!buffer.Decode(out numTopologySplits))
                return -1;
        }
        else
        {
            if (!buffer.DecodeVarint(out numTopologySplits))
                return -1;
        }

        if (numTopologySplits > 0)
        {
            if (numTopologySplits > (uint)_cornerTable.NumFaces)
                return -1;

            if (version < BitstreamVersion.Make(1, 2))
            {
                // Legacy format: direct decode of fields.
                for (uint i = 0; i < numTopologySplits; i++)
                {
                    if (!buffer.Decode(out int splitSymbolId))
                        return -1;
                    if (!buffer.Decode(out int sourceSymbolId))
                        return -1;
                    if (!buffer.Decode(out byte edgeData))
                        return -1;
                    _topologySplitData.Add(new TopologySplitEventData
                    {
                        SplitSymbolId = (uint)splitSymbolId,
                        SourceSymbolId = (uint)sourceSymbolId,
                        SourceEdge = edgeData & 1u
                    });
                }
            }
            else
            {
                // Delta + varint coded source and split symbol ids.
                int lastSourceSymbolId = 0;
                for (uint i = 0; i < numTopologySplits; i++)
                {
                    if (!buffer.DecodeVarint(out uint delta))
                        return -1;
                    uint sourceSymbolId = delta + (uint)lastSourceSymbolId;
                    if (!buffer.DecodeVarint(out uint delta2))
                        return -1;
                    if (delta2 > sourceSymbolId)
                        return -1;
                    uint splitSymbolId = sourceSymbolId - delta2;
                    lastSourceSymbolId = (int)sourceSymbolId;
                    _topologySplitData.Add(new TopologySplitEventData
                    {
                        SplitSymbolId = splitSymbolId,
                        SourceSymbolId = sourceSymbolId,
                        SourceEdge = 0
                    });
                }
                // Split edges are decoded from a direct bit decoder.
                buffer.StartBitDecoding(false, out _);
                for (uint i = 0; i < numTopologySplits; i++)
                {
                    int numBits = version < BitstreamVersion.Make(2, 2) ? 2 : 1;
                    buffer.DecodeLeastSignificantBits32(numBits, out uint edgeData);
                    _topologySplitData[(int)i] = _topologySplitData[(int)i] with { SourceEdge = edgeData & 1 };
                }
                buffer.EndBitDecoding();
            }
        }

        // Decode hole events.
        uint numHoleEvents = 0;
        if (version < BitstreamVersion.Make(2, 0))
        {
            if (!buffer.Decode(out numHoleEvents))
                return -1;
        }
        else if (version < BitstreamVersion.Make(2, 1))
        {
            if (!buffer.DecodeVarint(out numHoleEvents))
                return -1;
        }
        // Version >= 2.1: no hole events.

        if (numHoleEvents > 0)
        {
            if (version < BitstreamVersion.Make(1, 2))
            {
                for (uint i = 0; i < numHoleEvents; i++)
                {
                    if (!buffer.Decode(out int symbolId))
                        return -1;
                    _holeEventData.Add(new HoleEventData(symbolId));
                }
            }
            else
            {
                int lastSymbolId = 0;
                for (uint i = 0; i < numHoleEvents; i++)
                {
                    if (!buffer.DecodeVarint(out uint delta))
                        return -1;
                    int symbolId = (int)delta + lastSymbolId;
                    lastSymbolId = symbolId;
                    _holeEventData.Add(new HoleEventData(symbolId));
                }
            }
        }
        return (int)buffer.DecodedSize;
    }

    private bool DecodeAttributeConnectivitiesOnFaceLegacy(int corner)
    {
        int[] corners = [corner, _cornerTable.Next(corner), _cornerTable.Previous(corner)];
        for (int c = 0; c < 3; c++)
        {
            int oppCorner = _cornerTable.Opposite(corners[c]);
            if (oppCorner == CornerTable.kInvalidCornerIndex)
            {
                for (int i = 0; i < _attributeData.Count; i++)
                    _attributeData[i].AttributeSeamCorners.Add(corners[c]);
                continue;
            }
            // Legacy: no face-id check, decode seams for all non-boundary edges.
            for (int i = 0; i < _attributeData.Count; i++)
            {
                bool isSeam = _traversalDecoder.DecodeAttributeSeam(i);
                if (isSeam)
                    _attributeData[i].AttributeSeamCorners.Add(corners[c]);
            }
        }
        return true;
    }

    private bool DecodeAttributeConnectivitiesOnFace(int corner)
    {
        int[] corners = [corner, _cornerTable.Next(corner), _cornerTable.Previous(corner)];
        int srcFaceId = _cornerTable.Face(corner);

        for (int c = 0; c < 3; c++)
        {
            int oppCorner = _cornerTable.Opposite(corners[c]);
            if (oppCorner == CornerTable.kInvalidCornerIndex)
            {
                for (int i = 0; i < _attributeData.Count; i++)
                    _attributeData[i].AttributeSeamCorners.Add(corners[c]);
                continue;
            }
            int oppFaceId = _cornerTable.Face(oppCorner);
            if (oppFaceId < srcFaceId)
                continue;

            for (int i = 0; i < _attributeData.Count; i++)
            {
                bool isSeam = _traversalDecoder.DecodeAttributeSeam(i);
                if (isSeam)
                    _attributeData[i].AttributeSeamCorners.Add(corners[c]);
            }
        }
        return true;
    }

    private bool AssignPointsToCorners(int numConnectivityVerts)
    {
        _decoder.Mesh.SetNumFaces(_cornerTable.NumFaces);

        if (_attributeData.Count == 0)
        {
            for (int f = 0; f < _decoder.Mesh.NumFaces; f++)
            {
                int startCorner = 3 * f;
                int[] faceArr =
                [
                    _cornerTable.Vertex(startCorner),
                    _cornerTable.Vertex(startCorner + 1),
                    _cornerTable.Vertex(startCorner + 2)
                ];
                _decoder.Mesh.SetFace(f, faceArr);
            }
            _decoder.PointCloud.NumPoints = numConnectivityVerts;
            return true;
        }

        // Deduplicate points for multiple attributes.
        var pointToCornerMap = new List<int>();
        var cornerToPointMap = new int[_cornerTable.NumCorners];

        for (int v = 0; v < _cornerTable.NumVertices; v++)
        {
            int c = _cornerTable.LeftMostCorner(v);
            if (c == CornerTable.kInvalidCornerIndex)
                continue;

            int deduplicationFirstCorner = c;
            if (_isVertHole[v])
            {
                deduplicationFirstCorner = c;
            }
            else
            {
                for (int i = 0; i < _attributeData.Count; i++)
                {
                    if (!_attributeData[i].ConnectivityData.IsCornerOnSeam(c))
                        continue;
                    int vertId = _attributeData[i].ConnectivityData.Vertex(c);
                    int actC = _cornerTable.SwingRight(c);
                    bool seamFound = false;
                    while (actC != c)
                    {
                        if (actC == CornerTable.kInvalidCornerIndex)
                            return false;
                        if (_attributeData[i].ConnectivityData.Vertex(actC) != vertId)
                        {
                            deduplicationFirstCorner = actC;
                            seamFound = true;
                            break;
                        }
                        actC = _cornerTable.SwingRight(actC);
                    }
                    if (seamFound)
                        break;
                }
            }

            c = deduplicationFirstCorner;
            cornerToPointMap[c] = pointToCornerMap.Count;
            pointToCornerMap.Add(c);

            int prevC = c;
            c = _cornerTable.SwingRight(c);
            while (c != CornerTable.kInvalidCornerIndex && c != deduplicationFirstCorner)
            {
                bool attributeSeam = false;
                for (int i = 0; i < _attributeData.Count; i++)
                {
                    if (_attributeData[i].ConnectivityData.Vertex(c) !=
                        _attributeData[i].ConnectivityData.Vertex(prevC))
                    {
                        attributeSeam = true;
                        break;
                    }
                }
                if (attributeSeam)
                {
                    cornerToPointMap[c] = pointToCornerMap.Count;
                    pointToCornerMap.Add(c);
                }
                else
                {
                    cornerToPointMap[c] = cornerToPointMap[prevC];
                }
                prevC = c;
                c = _cornerTable.SwingRight(c);
            }
        }

        for (int f = 0; f < _decoder.Mesh.NumFaces; f++)
        {
            int[] faceArr =
            [
                cornerToPointMap[3 * f],
                cornerToPointMap[3 * f + 1],
                cornerToPointMap[3 * f + 2]
            ];
            _decoder.Mesh.SetFace(f, faceArr);
        }
        _decoder.PointCloud.NumPoints = pointToCornerMap.Count;
        return true;
    }

    private record struct TopologySplitEventData(uint SplitSymbolId, uint SourceSymbolId, uint SourceEdge);
    private record struct HoleEventData(int SymbolId);

    private class AttributeData
    {
        public int DecoderId { get; set; } = -1;
        public MeshAttributeCornerTable ConnectivityData { get; } = new();
        public bool IsConnectivityUsed { get; set; } = true;
        public MeshAttributeIndicesEncodingData EncodingData { get; } = new();
        public List<int> AttributeSeamCorners { get; } = [];
    }
}
