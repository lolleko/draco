using DracoSharp.Compression.Entropy;
using DracoSharp.Core;
using DracoSharp.Mesh;

namespace DracoSharp.Compression.Mesh;

public class MeshEdgebreakerTraversalValenceDecoder : MeshEdgebreakerTraversalDecoder
{
    private static readonly uint[] EdgeBreakerSymbolToTopologyId =
        [ 0x0, 0x1, 0x3, 0x5, 0x7 ]; // C, S, L, R, E

    private CornerTable _cornerTable;
    private int _numVertices;
    private int[] _vertexValences = [];
    private int _lastSymbol = -1;
    private int _activeContext = -1;
    private int _minValence = 2;
    private int _maxValence = 7;
    private uint[][] _contextSymbols = [];
    private int[] _contextCounters = [];

    public override void Init(MeshEdgebreakerDecoderImpl decoder)
    {
        base.Init(decoder);
        _cornerTable = decoder.CornerTable;
    }

    public override void SetNumEncodedVertices(int numVertices) => _numVertices = numVertices;

    public override bool Start(out DecoderBuffer outBuffer)
    {
        outBuffer = null;

        // For v < 2.2, decode traversal symbols first (the base class approach).
        if (BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
        {
            if (!DecodeTraversalSymbols())
                return false;
        }

        if (!DecodeStartFaces())
            return false;
        if (!DecodeAttributeSeams())
            return false;
        outBuffer = Buffer;

        // For v < 2.2, decode split symbols count and mode byte.
        if (BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
        {
            uint numSplitSymbols;
            if (BitstreamVersion < Core.BitstreamVersion.Make(2, 0))
            {
                if (!outBuffer.Decode(out numSplitSymbols))
                    return false;
            }
            else
            {
                if (!outBuffer.DecodeVarint(out numSplitSymbols))
                    return false;
            }
            if (numSplitSymbols >= (uint)_numVertices)
                return false;

            if (!outBuffer.Decode(out sbyte mode))
                return false;
            if (mode == 0) // EDGEBREAKER_VALENCE_MODE_2_7
            {
                _minValence = 2;
                _maxValence = 7;
            }
            else
            {
                return false; // Unsupported mode.
            }
        }
        else
        {
            _minValence = 2;
            _maxValence = 7;
        }

        if (_numVertices < 0)
            return false;

        _vertexValences = new int[_numVertices];

        int numUniqueValences = _maxValence - _minValence + 1;
        _contextSymbols = new uint[numUniqueValences][];
        _contextCounters = new int[numUniqueValences];

        for (int i = 0; i < numUniqueValences; i++)
        {
            if (!outBuffer.DecodeVarint(out uint numSymbols))
                return false;
            if (numSymbols > (uint)_cornerTable.NumFaces)
                return false;
            if (numSymbols > 0)
            {
                _contextSymbols[i] = new uint[numSymbols];
                if (!SymbolDecoding.DecodeSymbols(numSymbols, 1, outBuffer, _contextSymbols[i]))
                    return false;
                _contextCounters[i] = (int)numSymbols;
            }
            else
            {
                _contextSymbols[i] = [];
                _contextCounters[i] = 0;
            }
        }
        return true;
    }

    public override uint DecodeSymbol()
    {
        if (_activeContext != -1)
        {
            int contextCounter = --_contextCounters[_activeContext];
            if (contextCounter < 0)
                return (uint)EdgebreakerTopologyBitPattern.Invalid;
            uint symbolId = _contextSymbols[_activeContext][contextCounter];
            if (symbolId > 4)
                return (uint)EdgebreakerTopologyBitPattern.Invalid;
            _lastSymbol = (int)EdgeBreakerSymbolToTopologyId[symbolId];
        }
        else
        {
            // For v < 2.2, decode from the bit-encoded symbol buffer.
            if (BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
            {
                _lastSymbol = (int)base.DecodeSymbol();
            }
            else
            {
                _lastSymbol = (int)EdgebreakerTopologyBitPattern.E;
            }
        }
        return (uint)_lastSymbol;
    }

    public override void NewActiveCornerReached(int corner)
    {
        int next = _cornerTable.Next(corner);
        int prev = _cornerTable.Previous(corner);

        switch (_lastSymbol)
        {
            case (int)EdgebreakerTopologyBitPattern.C:
            case (int)EdgebreakerTopologyBitPattern.S:
                _vertexValences[_cornerTable.Vertex(next)] += 1;
                _vertexValences[_cornerTable.Vertex(prev)] += 1;
                break;
            case (int)EdgebreakerTopologyBitPattern.R:
                _vertexValences[_cornerTable.Vertex(corner)] += 1;
                _vertexValences[_cornerTable.Vertex(next)] += 1;
                _vertexValences[_cornerTable.Vertex(prev)] += 2;
                break;
            case (int)EdgebreakerTopologyBitPattern.L:
                _vertexValences[_cornerTable.Vertex(corner)] += 1;
                _vertexValences[_cornerTable.Vertex(next)] += 2;
                _vertexValences[_cornerTable.Vertex(prev)] += 1;
                break;
            case (int)EdgebreakerTopologyBitPattern.E:
                _vertexValences[_cornerTable.Vertex(corner)] += 2;
                _vertexValences[_cornerTable.Vertex(next)] += 2;
                _vertexValences[_cornerTable.Vertex(prev)] += 2;
                break;
        }

        int activeValence = _vertexValences[_cornerTable.Vertex(next)];
        int clampedValence = Math.Clamp(activeValence, _minValence, _maxValence);
        _activeContext = clampedValence - _minValence;
    }

    public override void MergeVertices(int dest, int source)
    {
        _vertexValences[dest] += _vertexValences[source];
    }

    public override void Done()
    {
        base.Done();
    }
}
