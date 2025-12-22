using System;

namespace Draco.Decoder
{
    public class SequentialMeshDecoder
    {
        private readonly Mesh mesh;
        private readonly DecoderBuffer buffer;
        private readonly ushort bitstreamVersion;

        public SequentialMeshDecoder(Mesh mesh, DecoderBuffer buffer, ushort bitstreamVersion)
        {
            this.mesh = mesh;
            this.buffer = buffer;
            this.bitstreamVersion = bitstreamVersion;
        }

        public StatusOr<bool> DecodeConnectivity()
        {
            uint numFaces;
            uint numPoints;

            Console.WriteLine($"[SequentialMeshDecoder.DecodeConnectivity] bitstreamVersion=0x{bitstreamVersion:X4}, buffer position: {buffer.DecodedSize}");

            // DRACO_BITSTREAM_VERSION(2, 2) is 0x0202
            if (bitstreamVersion < 0x0202)
            {
                if (!buffer.Decode(out numFaces))
                    return Status.IoError("Failed to decode num_faces");
                Console.WriteLine($"[SequentialMeshDecoder.DecodeConnectivity] numFaces={numFaces}, buffer position: {buffer.DecodedSize}");
                if (!buffer.Decode(out numPoints))
                    return Status.IoError("Failed to decode num_points");
                Console.WriteLine($"[SequentialMeshDecoder.DecodeConnectivity] numPoints={numPoints}, buffer position: {buffer.DecodedSize}");
            }
            else
            {
                if (!VarintDecoding.DecodeVarint(buffer, out numFaces))
                    return Status.IoError("Failed to decode num_faces varint");
                if (!VarintDecoding.DecodeVarint(buffer, out numPoints))
                    return Status.IoError("Failed to decode num_points varint");
            }

            ulong faces64 = numFaces;
            if (faces64 > (ulong)(0xffffffff / 3))
                return Status.DracoError("Too many faces");
            
            if (faces64 > (ulong)buffer.RemainingSize / 3)
                return Status.DracoError("Face count exceeds buffer size");

            if (!buffer.Decode(out byte connectivityMethod))
                return Status.IoError("Failed to decode connectivity_method");

            if (connectivityMethod == 0)
            {
                var result = DecodeAndDecompressIndices(numFaces);
                if (!result.Ok)
                    return result.Status;
            }
            else
            {
                if (numPoints < 256)
                {
                    for (uint i = 0; i < numFaces; i++)
                    {
                        var face = new int[3];
                        for (int j = 0; j < 3; j++)
                        {
                            if (!buffer.Decode(out byte val))
                                return Status.IoError($"Failed to decode face {i} index {j} as uint8");
                            face[j] = val;
                        }
                        mesh.AddFace(face);
                    }
                }
                else if (numPoints < (1 << 16))
                {
                    for (uint i = 0; i < numFaces; i++)
                    {
                        var face = new int[3];
                        for (int j = 0; j < 3; j++)
                        {
                            if (!buffer.Decode(out ushort val))
                                return Status.IoError($"Failed to decode face {i} index {j} as uint16");
                            face[j] = val;
                        }
                        mesh.AddFace(face);
                    }
                }
                else if (numPoints < (1 << 21) && bitstreamVersion >= 0x0202)
                {
                    for (uint i = 0; i < numFaces; i++)
                    {
                        var face = new int[3];
                        for (int j = 0; j < 3; j++)
                        {
                            if (!VarintDecoding.DecodeVarint(buffer, out uint val))
                                return Status.IoError($"Failed to decode face {i} index {j} as varint");
                            face[j] = (int)val;
                        }
                        mesh.AddFace(face);
                    }
                }
                else
                {
                    for (uint i = 0; i < numFaces; i++)
                    {
                        var face = new int[3];
                        for (int j = 0; j < 3; j++)
                        {
                            if (!buffer.Decode(out uint val))
                                return Status.IoError($"Failed to decode face {i} index {j} as uint32");
                            face[j] = (int)val;
                        }
                        mesh.AddFace(face);
                    }
                }
            }

            mesh.SetNumPoints((int)numPoints);
            return StatusOr<bool>.FromValue(true);
        }

        private StatusOr<bool> DecodeAndDecompressIndices(uint numFaces)
        {
            uint[] indicesBuffer = new uint[numFaces * 3];
            if (!SymbolDecoding.DecodeSymbols(numFaces * 3, 1, buffer, indicesBuffer))
                return Status.IoError("Failed to decode compressed indices");

            int lastIndexValue = 0;
            int vertexIndex = 0;

            for (uint i = 0; i < numFaces; i++)
            {
                var face = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    int encodedVal = (int)indicesBuffer[vertexIndex++];
                    int indexDiff = (encodedVal >> 1);
                    if ((encodedVal & 1) != 0)
                        indexDiff = -indexDiff;
                    int indexValue = indexDiff + lastIndexValue;
                    face[j] = indexValue;
                    lastIndexValue = indexValue;
                }
                mesh.AddFace(face);
            }

            return StatusOr<bool>.FromValue(true);
        }
    }
}
