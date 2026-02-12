using DracoSharp.Compression.BitCoders;
using DracoSharp.Core;
using DracoSharp.Mesh;

namespace DracoSharp.Compression.Mesh;

public class MeshEdgebreakerTraversalDecoder
{
    private DecoderBuffer _buffer;
    private DecoderBuffer _symbolBuffer;
    private DecoderBuffer _startFaceBuffer;
    private RAnsBitDecoder _startFaceDecoder = new();
    private RAnsBitDecoder[] _attributeConnectivityDecoders;
    private int _numAttributeData;
    protected MeshEdgebreakerDecoderImpl _decoderImpl;

    public virtual void Init(MeshEdgebreakerDecoderImpl decoder)
    {
        _decoderImpl = decoder;
        _buffer = new DecoderBuffer();
        var parentBuffer = decoder.Decoder.Buffer;
        _buffer.Init(parentBuffer.DataHead, parentBuffer.BitstreamVersion);
    }

    public ushort BitstreamVersion => _decoderImpl.Decoder.BitstreamVersion;

    public virtual void SetNumEncodedVertices(int numVertices) { }

    public void SetNumAttributeData(int numData) => _numAttributeData = numData;

    public virtual bool Start(out DecoderBuffer outBuffer)
    {
        outBuffer = null;
        if (!DecodeTraversalSymbols())
            return false;
        if (!DecodeStartFaces())
            return false;
        if (!DecodeAttributeSeams())
            return false;
        outBuffer = _buffer;
        return true;
    }

    public virtual bool DecodeStartFaceConfiguration()
    {
        if (_buffer.BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
        {
            _startFaceBuffer.DecodeLeastSignificantBits32(1, out uint faceConfiguration);
            return faceConfiguration != 0;
        }
        return _startFaceDecoder.DecodeNextBit();
    }

    public virtual uint DecodeSymbol()
    {
        _symbolBuffer.DecodeLeastSignificantBits32(1, out uint symbol);
        if (symbol == (uint)EdgebreakerTopologyBitPattern.C)
            return symbol;
        _symbolBuffer.DecodeLeastSignificantBits32(2, out uint symbolSuffix);
        symbol |= symbolSuffix << 1;
        return symbol;
    }

    public virtual void NewActiveCornerReached(int corner) { }
    public virtual void MergeVertices(int dest, int source) { }

    public bool DecodeAttributeSeam(int attribute) =>
        _attributeConnectivityDecoders[attribute].DecodeNextBit();

    public virtual void Done()
    {
        if (_symbolBuffer != null && _symbolBuffer.BitDecoderActive)
            _symbolBuffer.EndBitDecoding();
        if (_buffer.BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
        {
            _startFaceBuffer?.EndBitDecoding();
        }
        else
        {
            _startFaceDecoder.EndDecoding();
        }
    }

    protected DecoderBuffer Buffer => _buffer;
    protected void SetBuffer(DecoderBuffer buffer) => _buffer = buffer;

    protected bool DecodeTraversalSymbols()
    {
        _symbolBuffer = _buffer.Clone();
        if (!_symbolBuffer.StartBitDecoding(true, out ulong traversalSize))
            return false;
        _buffer = _symbolBuffer.Clone();
        if ((long)traversalSize > _buffer.RemainingSize)
            return false;
        _buffer.Advance((long)traversalSize);
        return true;
    }

    protected bool DecodeStartFaces()
    {
        if (_buffer.BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
        {
            _startFaceBuffer = _buffer.Clone();
            if (!_startFaceBuffer.StartBitDecoding(true, out ulong traversalSize))
                return false;
            _buffer = _startFaceBuffer.Clone();
            if ((long)traversalSize > _buffer.RemainingSize)
                return false;
            _buffer.Advance((long)traversalSize);
            return true;
        }
        return _startFaceDecoder.StartDecoding(_buffer);
    }

    protected bool DecodeAttributeSeams()
    {
        if (_numAttributeData > 0)
        {
            _attributeConnectivityDecoders = new RAnsBitDecoder[_numAttributeData];
            for (int i = 0; i < _numAttributeData; i++)
            {
                _attributeConnectivityDecoders[i] = new RAnsBitDecoder();
                if (!_attributeConnectivityDecoders[i].StartDecoding(_buffer))
                    return false;
            }
        }
        return true;
    }
}
