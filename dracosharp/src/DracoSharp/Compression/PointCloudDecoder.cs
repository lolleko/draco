using DracoSharp.Attributes;
using DracoSharp.Compression.Attributes;
using DracoSharp.Core;

namespace DracoSharp.Compression;

public abstract class PointCloudDecoder
{
    private PointCloud.PointCloud _pointCloud;
    private readonly List<AttributesDecoder> _attributesDecoders = [];
    private int[] _attributeToDecoderMap = [];
    private DecoderBuffer _buffer;
    private ushort _bitstreamVersion;

    public PointCloud.PointCloud PointCloud => _pointCloud;
    public ushort BitstreamVersion => _bitstreamVersion;
    public DecoderBuffer Buffer => _buffer;

    public abstract EncodedGeometryType GeometryType { get; }

    public static bool DecodeHeader(DecoderBuffer buffer, out DracoHeader header)
    {
        header = default;
        Span<byte> magic = stackalloc byte[5];
        if (!buffer.Decode(magic))
            return false;
        if (magic[0] != (byte)'D' || magic[1] != (byte)'R' ||
            magic[2] != (byte)'A' || magic[3] != (byte)'C' || magic[4] != (byte)'O')
            return false;
        if (!buffer.Decode(out byte versionMajor))
            return false;
        if (!buffer.Decode(out byte versionMinor))
            return false;
        if (!buffer.Decode(out byte encoderType))
            return false;
        if (!buffer.Decode(out byte encoderMethod))
            return false;
        if (!buffer.Decode(out ushort flags))
            return false;
        header = new DracoHeader
        {
            VersionMajor = versionMajor,
            VersionMinor = versionMinor,
            EncoderType = encoderType,
            EncoderMethod = encoderMethod,
            Flags = flags
        };
        return true;
    }

    public bool Decode(DecoderBuffer inBuffer, PointCloud.PointCloud outPointCloud)
    {
        _buffer = inBuffer;
        _pointCloud = outPointCloud;

        if (!DecodeHeader(inBuffer, out var header))
            return false;

        if (header.EncoderType != (byte)GeometryType)
            return false;

        _bitstreamVersion = Core.BitstreamVersion.Make(header.VersionMajor, header.VersionMinor);
        _buffer.BitstreamVersion = _bitstreamVersion;

        // Skip metadata if present (version >= 1.3 and metadata flag set).
        if (_bitstreamVersion >= Core.BitstreamVersion.Make(1, 3) && header.HasMetadata)
        {
            if (!SkipMetadata())
                return false;
        }

        if (!InitializeDecoder())
            return false;
        if (!DecodeGeometryData())
            return false;
        if (!DecodePointAttributes())
            return false;
        return true;
    }

    protected virtual bool InitializeDecoder() => true;
    protected virtual bool DecodeGeometryData() => true;

    private bool DecodePointAttributes()
    {
        if (!_buffer.Decode(out byte numAttributesDecoders))
            return false;

        for (int i = 0; i < numAttributesDecoders; i++)
        {
            if (!CreateAttributesDecoder(i))
                return false;
        }

        for (int i = 0; i < _attributesDecoders.Count; i++)
        {
            if (!_attributesDecoders[i].Init(this, _buffer))
                return false;
        }

        for (int i = 0; i < _attributesDecoders.Count; i++)
        {
            if (!_attributesDecoders[i].DecodeAttributesDecoderData(_buffer))
                return false;
        }

        // Build attribute-to-decoder map.
        for (int i = 0; i < _attributesDecoders.Count; i++)
        {
            int numAttrs = _attributesDecoders[i].NumAttributes;
            for (int j = 0; j < numAttrs; j++)
            {
                int attId = _attributesDecoders[i].GetAttributeId(j);
                if (attId >= _attributeToDecoderMap.Length)
                    Array.Resize(ref _attributeToDecoderMap, attId + 1);
                _attributeToDecoderMap[attId] = i;
            }
        }

        for (int i = 0; i < _attributesDecoders.Count; i++)
        {
            if (!_attributesDecoders[i].DecodeAttributes(_buffer))
                return false;
        }

        OnAttributesDecoded();
        return true;
    }

    protected abstract bool CreateAttributesDecoder(int attrDecoderIndex);

    protected void AddAttributesDecoder(AttributesDecoder decoder)
    {
        _attributesDecoders.Add(decoder);
    }

    public bool SetAttributesDecoder(int attDecoderId, AttributesDecoder decoder)
    {
        while (_attributesDecoders.Count <= attDecoderId)
            _attributesDecoders.Add(null);
        _attributesDecoders[attDecoderId] = decoder;
        return true;
    }

    public int NumAttributesDecoders => _attributesDecoders.Count;
    public AttributesDecoder GetAttributesDecoder(int index) => _attributesDecoders[index];

    protected virtual void OnAttributesDecoded() { }

    public PointAttribute GetPortableAttribute(int parentAttId)
    {
        if (parentAttId < 0 || parentAttId >= _pointCloud.NumAttributes)
            return null;
        if (parentAttId >= _attributeToDecoderMap.Length)
            return null;
        int decoderIndex = _attributeToDecoderMap[parentAttId];
        return _attributesDecoders[decoderIndex].GetPortableAttribute(parentAttId);
    }

    private bool SkipMetadata()
    {
        return SkipGeometryMetadata(_buffer);
    }

    private static bool SkipGeometryMetadata(DecoderBuffer buffer)
    {
        // Skip attribute metadata section first.
        if (!buffer.DecodeVarint(out uint numAttMetadata))
            return false;
        for (uint i = 0; i < numAttMetadata; i++)
        {
            if (!buffer.DecodeVarint(out uint _)) // att_unique_id
                return false;
            if (!SkipMetadataElement(buffer))
                return false;
        }
        // Then skip the geometry metadata itself.
        return SkipMetadataElement(buffer);
    }

    private static bool SkipMetadataElement(DecoderBuffer buffer)
    {
        // Matches C++ MetadataDecoder::DecodeMetadata using iterative stack.
        // Each element has: entries, then sub-metadata count.
        // Sub-metadata items have a name prefix before their entries.
        int stackCount = 1; // Start with 1 element to process (no name prefix).
        bool isFirst = true;

        while (stackCount > 0)
        {
            stackCount--;

            // Sub-metadata items (not the first) have a name prefix.
            if (!isFirst)
            {
                if (!SkipName(buffer))
                    return false;
            }
            isFirst = false;

            // Decode entries.
            if (!buffer.DecodeVarint(out uint numEntries))
                return false;
            for (uint i = 0; i < numEntries; i++)
            {
                if (!SkipEntry(buffer))
                    return false;
            }

            // Decode sub-metadata count and push onto stack.
            if (!buffer.DecodeVarint(out uint numSubMetadata))
                return false;
            if (numSubMetadata > (uint)buffer.RemainingSize)
                return false;
            stackCount += (int)numSubMetadata;
        }
        return true;
    }

    private static bool SkipEntry(DecoderBuffer buffer)
    {
        // Entry: name (uint8 length + bytes) + value (varint length + bytes).
        if (!SkipName(buffer))
            return false;
        if (!buffer.DecodeVarint(out uint dataSize))
            return false;
        if (dataSize == 0 || dataSize > (uint)buffer.RemainingSize)
            return false;
        buffer.Advance(dataSize);
        return true;
    }

    private static bool SkipName(DecoderBuffer buffer)
    {
        // Name: uint8 length + bytes (NOT varint).
        if (!buffer.Decode(out byte nameLen))
            return false;
        if (nameLen > 0)
            buffer.Advance(nameLen);
        return true;
    }
}
