using DracoSharp.Compression.Attributes;
using DracoSharp.Compression.Entropy;
using DracoSharp.Core;

namespace DracoSharp.Compression;

public class MeshSequentialDecoder : MeshDecoder
{
    protected override bool DecodeConnectivity()
    {
        uint numFaces, numPoints;
        if (BitstreamVersion < Core.BitstreamVersion.Make(2, 2))
        {
            if (!Buffer.Decode(out numFaces))
                return false;
            if (!Buffer.Decode(out numPoints))
                return false;
        }
        else
        {
            if (!Buffer.DecodeVarint(out numFaces))
                return false;
            if (!Buffer.DecodeVarint(out numPoints))
                return false;
        }

        // Validate face count.
        if ((ulong)numFaces > 0xFFFFFFFF / 3)
            return false;
        if (numFaces > (ulong)Buffer.RemainingSize / 3)
            return false;

        if (!Buffer.Decode(out byte connectivityMethod))
            return false;

        if (connectivityMethod == 0)
        {
            if (!DecodeAndDecompressIndices(numFaces))
                return false;
        }
        else
        {
            if (!DecodeRawIndices(numFaces, numPoints))
                return false;
        }

        PointCloud.NumPoints = (int)numPoints;
        return true;
    }

    protected override bool CreateAttributesDecoder(int attrDecoderIndex)
    {
        var controller = new SequentialAttributeDecodersController();
        controller.Init(this, Buffer);
        AddAttributesDecoder(controller);
        return true;
    }

    private bool DecodeAndDecompressIndices(uint numFaces)
    {
        uint numIndices = numFaces * 3;
        uint[] indicesBuffer = new uint[numIndices];
        if (!SymbolDecoding.DecodeSymbols(numIndices, 1, Buffer, indicesBuffer))
            return false;

        // Reconstruct indices from zigzag-delta encoded differences.
        int lastIndexValue = 0;
        int vertexIndex = 0;
        for (uint i = 0; i < numFaces; i++)
        {
            int[] face = new int[3];
            for (int j = 0; j < 3; j++)
            {
                uint encodedVal = indicesBuffer[vertexIndex++];
                int indexDiff = (int)(encodedVal >> 1);
                if ((encodedVal & 1) != 0)
                {
                    if (indexDiff > lastIndexValue)
                        return false;
                    indexDiff = -indexDiff;
                }
                else
                {
                    if (indexDiff > int.MaxValue - lastIndexValue)
                        return false;
                }
                int indexValue = indexDiff + lastIndexValue;
                face[j] = indexValue;
                lastIndexValue = indexValue;
            }
            Mesh.AddFace(face);
        }
        return true;
    }

    private bool DecodeRawIndices(uint numFaces, uint numPoints)
    {
        if (numPoints < 256)
        {
            for (uint i = 0; i < numFaces; i++)
            {
                int[] face = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    if (!Buffer.Decode(out byte val))
                        return false;
                    face[j] = val;
                }
                Mesh.AddFace(face);
            }
        }
        else if (numPoints < (1 << 16))
        {
            for (uint i = 0; i < numFaces; i++)
            {
                int[] face = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    if (!Buffer.Decode(out ushort val))
                        return false;
                    face[j] = val;
                }
                Mesh.AddFace(face);
            }
        }
        else if (numPoints < (1 << 21) &&
                 BitstreamVersion >= Core.BitstreamVersion.Make(2, 2))
        {
            for (uint i = 0; i < numFaces; i++)
            {
                int[] face = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    if (!Buffer.DecodeVarint(out uint val))
                        return false;
                    face[j] = (int)val;
                }
                Mesh.AddFace(face);
            }
        }
        else
        {
            for (uint i = 0; i < numFaces; i++)
            {
                int[] face = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    if (!Buffer.Decode(out uint val))
                        return false;
                    face[j] = (int)val;
                }
                Mesh.AddFace(face);
            }
        }
        return true;
    }
}
