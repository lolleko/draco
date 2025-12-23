using System;
using System.Collections.Generic;

namespace Draco.Decoder
{
    public enum EdgebreakerSymbol
    {
        C = 0,  // Continue - most common
        S = 1,  // Split
        L = 2,  // Left
        R = 3,  // Right
        E = 4,  // End
        Invalid = 5
    }

    public class EdgebreakerMeshDecoder
    {
        private readonly Mesh mesh;
        private readonly DecoderBuffer buffer;
        
        private List<int> cornerToVertex = new List<int>();
        private List<int> vertexCorners = new List<int>();
        private List<bool> vertexVisited;
        private List<bool> faceVisited;
        
        private int numVertices;
        private int numFaces;

        public EdgebreakerMeshDecoder(Mesh mesh, DecoderBuffer buffer)
        {
            this.mesh = mesh;
            this.buffer = buffer;
        }

        public StatusOr<int> DecodeConnectivity()
        {
            // Read traversal decoder type
            if (!buffer.Decode(out byte traversalDecoderType))
                return Status.IoError("Failed to read traversal decoder type");
            
            // For versions < 2.2, read num_new_vertices first
            int numNewVertices = 0;
            if (buffer.BitstreamVersion < 0x0202)
            {
                if (buffer.BitstreamVersion < 0x0200)
                {
                    if (!buffer.Decode(out uint numNewVerts))
                        return Status.IoError("Failed to read num_new_vertices");
                    numNewVertices = (int)numNewVerts;
                }
                else
                {
                    if (!VarintDecoding.DecodeVarint(buffer, out uint numNewVerts))
                        return Status.IoError("Failed to read num_new_vertices");
                    numNewVertices = (int)numNewVerts;
                }
            }
            
            // Read number of encoded vertices
            int numEncodedVertices;
            if (buffer.BitstreamVersion < 0x0200)
            {
                if (!buffer.Decode(out uint numEncVerts))
                    return Status.IoError("Failed to read num_encoded_vertices");
                numEncodedVertices = (int)numEncVerts;
            }
            else
            {
                if (!VarintDecoding.DecodeVarint(buffer, out uint numEncVerts))
                    return Status.IoError("Failed to read num_encoded_vertices");
                numEncodedVertices = (int)numEncVerts;
            }
            
            // Read number of faces
            if (buffer.BitstreamVersion < 0x0200)
            {
                if (!buffer.Decode(out uint numFcs))
                    return Status.IoError("Failed to read number of faces");
                numFaces = (int)numFcs;
            }
            else
            {
                if (!VarintDecoding.DecodeVarint(buffer, out uint numFcs))
                    return Status.IoError("Failed to read number of faces");
                numFaces = (int)numFcs;
            }

            if (numEncodedVertices < 0 || numFaces < 0)
            {
                return Status.DracoError("Invalid encoded vertices/face count in edgebreaker decoder");
            }
            
            // The actual number of vertices is computed during decoding
            // For now, allocate based on faces * 3 (upper bound)
            numVertices = numEncodedVertices + numFaces;  // Conservative estimate
            
            mesh.SetNumFaces(numFaces);

            if (numFaces == 0)
            {
                return StatusOr<int>.FromValue(0);
            }

            // Initialize corner table
            cornerToVertex = new List<int>(new int[numFaces * 3]);
            vertexCorners = new List<int>(new int[numVertices]);
            for (int i = 0; i < numVertices; i++)
            {
                vertexCorners[i] = -1;
            }

            vertexVisited = new List<bool>(new bool[numVertices]);
            faceVisited = new List<bool>(new bool[numFaces]);

            // Read number of attribute data (needed for attribute connectivity)
            // This comes BEFORE the symbol data
            if (!buffer.Decode(out byte numAttributeData))
                return Status.IoError("Failed to read num_attribute_data");
            
            // Read number of encoded symbols
            if (!VarintDecoding.DecodeVarint(buffer, out uint numEncodedSymbols))
                return Status.IoError("Failed to read number of encoded symbols");
            
            Console.WriteLine($"Edgebreaker: numEncodedVertices={numEncodedVertices}, numFaces={numFaces}, numEncodedSymbols={numEncodedSymbols}");

            // Read number of split symbols (topology changes)
            if (!VarintDecoding.DecodeVarint(buffer, out uint numSplitSymbols))
                return Status.IoError("Failed to read number of split symbols");

            // Decode traversal symbols (they are in a bit-decoded section)
            // StartBitDecoding returns the size of the encoded section
            if (!buffer.StartBitDecoding(true, out ulong traversalSize))
                return Status.IoError("Failed to start bit decoding for traversal symbols");
            
            // Read symbols from bit-decoded buffer
            List<EdgebreakerSymbol> symbols = new List<EdgebreakerSymbol>();
            for (int i = 0; i < numEncodedSymbols; i++)
            {
                EdgebreakerSymbol symbol = ReadSymbol();
                if (symbol == EdgebreakerSymbol.Invalid)
                {
                    return Status.DracoError("Invalid edgebreaker symbol");
                }
                symbols.Add(symbol);
            }

            // Advance buffer past the bit-decoded section
            buffer.EndBitDecoding();

            // Decode the mesh connectivity using edgebreaker algorithm
            var status = DecodeSymbols(symbols, (int)numSplitSymbols);
            if (!status.Ok)
            {
                return StatusOr<int>.FromStatus(status.Status);
            }

            // Build face array using SetFace method
            for (int f = 0; f < numFaces; f++)
            {
                int corner = f * 3;
                mesh.SetFace(f, new Face(cornerToVertex[corner + 0], cornerToVertex[corner + 1], cornerToVertex[corner + 2]));
            }

            // Return numAttributeData - this tells the caller how many attribute decoders to expect
            // In C++, this is stored in attribute_data_.size()
            Console.WriteLine($"Edgebreaker: numAttributeData={numAttributeData}");

            return StatusOr<int>.FromValue((int)numAttributeData);
        }

        private EdgebreakerSymbol ReadSymbol()
        {
            // Read edgebreaker symbols with variable bit-length encoding
            // C = 0 (1 bit)
            // S, L, R, E = 1xx (3 bits)
            
            if (!buffer.DecodeLeastSignificantBits32(1, out uint bit))
                return EdgebreakerSymbol.Invalid;
                
            if (bit == 0)
            {
                return EdgebreakerSymbol.C;
            }

            // Read next 2 bits to distinguish S, L, R, E
            if (!buffer.DecodeLeastSignificantBits32(2, out uint bits))
                return EdgebreakerSymbol.Invalid;
                
            switch (bits)
            {
                case 0: return EdgebreakerSymbol.S;  // 100
                case 1: return EdgebreakerSymbol.R;  // 101
                case 2: return EdgebreakerSymbol.L;  // 110
                case 3: return EdgebreakerSymbol.E;  // 111
                default: return EdgebreakerSymbol.Invalid;
            }
        }

        private StatusOr<bool> DecodeSymbols(List<EdgebreakerSymbol> symbols, int numSplitSymbols)
        {
            // Simplified edgebreaker decoding
            // The edgebreaker algorithm processes symbols in reverse order (Spirale Reversi)
            // and builds the mesh topology as it goes
            
            int currentVertex = 0;
            int currentFace = 0;
            
            if (numFaces == 0)
            {
                return StatusOr<bool>.FromValue(true);
            }
            
            // Create first triangle with 3 new vertices
            if (currentVertex + 3 > numVertices)
            {
                return Status.DracoError("Not enough vertices for initial triangle");
            }
            
            cornerToVertex[0] = currentVertex;
            cornerToVertex[1] = currentVertex + 1;
            cornerToVertex[2] = currentVertex + 2;
            
            vertexCorners[currentVertex] = 0;
            vertexCorners[currentVertex + 1] = 1;
            vertexCorners[currentVertex + 2] = 2;
            
            vertexVisited[currentVertex] = true;
            vertexVisited[currentVertex + 1] = true;
            vertexVisited[currentVertex + 2] = true;
            faceVisited[0] = true;
            
            currentVertex += 3;
            int activeCorner = 0;
            currentFace = 1;

            // Process each symbol
            for (int i = 0; i < symbols.Count && currentFace < numFaces; i++)
            {
                EdgebreakerSymbol symbol = symbols[i];

                switch (symbol)
                {
                    case EdgebreakerSymbol.C:
                        // Create new triangle sharing edge, add one new vertex
                        {
                            if (currentVertex >= numVertices)
                            {
                                return Status.DracoError($"C symbol: Not enough vertices (need {currentVertex + 1}, have {numVertices})");
                            }
                            
                            int corner = currentFace * 3;
                            int v0 = cornerToVertex[activeCorner];
                            int v1 = cornerToVertex[Next(activeCorner)];
                            
                            cornerToVertex[corner + 0] = currentVertex;
                            cornerToVertex[corner + 1] = v0;
                            cornerToVertex[corner + 2] = v1;
                            
                            if (vertexCorners[currentVertex] == -1)
                            {
                                vertexCorners[currentVertex] = corner;
                            }
                            
                            vertexVisited[currentVertex] = true;
                            faceVisited[currentFace] = true;
                            
                            activeCorner = corner;
                            currentVertex++;
                            currentFace++;
                        }
                        break;

                    case EdgebreakerSymbol.R:
                        // Right turn - share two vertices, create face to the right
                        {
                            if (currentVertex >= numVertices)
                            {
                                return Status.DracoError($"R symbol: Not enough vertices (need {currentVertex + 1}, have {numVertices})");
                            }
                            
                            int corner = currentFace * 3;
                            int v1 = cornerToVertex[Next(activeCorner)];
                            int v2 = cornerToVertex[Previous(activeCorner)];
                            
                            cornerToVertex[corner + 0] = currentVertex;
                            cornerToVertex[corner + 1] = v2;
                            cornerToVertex[corner + 2] = v1;
                            
                            if (vertexCorners[currentVertex] == -1)
                            {
                                vertexCorners[currentVertex] = corner;
                            }
                            
                            vertexVisited[currentVertex] = true;
                            faceVisited[currentFace] = true;
                            
                            activeCorner = corner;
                            currentVertex++;
                            currentFace++;
                        }
                        break;

                    case EdgebreakerSymbol.L:
                        // Left turn - share two vertices, create face to the left
                        {
                            if (currentVertex >= numVertices)
                            {
                                return Status.DracoError($"L symbol: Not enough vertices (need {currentVertex + 1}, have {numVertices})");
                            }
                            
                            int corner = currentFace * 3;
                            int v0 = cornerToVertex[activeCorner];
                            int v1 = cornerToVertex[Next(activeCorner)];
                            
                            cornerToVertex[corner + 0] = currentVertex;
                            cornerToVertex[corner + 1] = v0;
                            cornerToVertex[corner + 2] = v1;
                            
                            if (vertexCorners[currentVertex] == -1)
                            {
                                vertexCorners[currentVertex] = corner;
                            }
                            
                            vertexVisited[currentVertex] = true;
                            faceVisited[currentFace] = true;
                            
                            activeCorner = corner;
                            currentVertex++;
                            currentFace++;
                        }
                        break;

                    case EdgebreakerSymbol.E:
                        // End - close the current hole, share all three vertices
                        {
                            int corner = currentFace * 3;
                            int v0 = cornerToVertex[activeCorner];
                            int v1 = cornerToVertex[Next(activeCorner)];
                            int v2 = cornerToVertex[Previous(activeCorner)];
                            
                            cornerToVertex[corner + 0] = v2;
                            cornerToVertex[corner + 1] = v0;
                            cornerToVertex[corner + 2] = v1;
                            
                            faceVisited[currentFace] = true;
                            
                            currentFace++;
                        }
                        break;

                    case EdgebreakerSymbol.S:
                        // Split - topology split, shares edge with previous geometry
                        {
                            if (currentVertex >= numVertices)
                            {
                                return Status.DracoError($"S symbol: Not enough vertices (need {currentVertex + 1}, have {numVertices})");
                            }
                            
                            int corner = currentFace * 3;
                            int v0 = cornerToVertex[activeCorner];
                            int v1 = cornerToVertex[Next(activeCorner)];
                            
                            cornerToVertex[corner + 0] = currentVertex;
                            cornerToVertex[corner + 1] = v0;
                            cornerToVertex[corner + 2] = v1;
                            
                            if (vertexCorners[currentVertex] == -1)
                            {
                                vertexCorners[currentVertex] = corner;
                            }
                            
                            vertexVisited[currentVertex] = true;
                            faceVisited[currentFace] = true;
                            
                            activeCorner = corner;
                            currentVertex++;
                            currentFace++;
                        }
                        break;
                }
            }
            
            // Set the actual number of vertices that were used
            mesh.NumPoints = currentVertex;

            return StatusOr<bool>.FromValue(true);
        }

        private int Next(int corner)
        {
            // Get next corner in the same triangle
            int face = corner / 3;
            int offset = corner % 3;
            return face * 3 + ((offset + 1) % 3);
        }

        private int Previous(int corner)
        {
            // Get previous corner in the same triangle
            int face = corner / 3;
            int offset = corner % 3;
            return face * 3 + ((offset + 2) % 3);
        }
    }
}
