using System;
using System.Buffers.Binary;
using System.IO;

namespace SMMDownloader.Avalonia.Services;

/// <summary>
/// Portable ASH0 decompressor based on crediar's ASH v0.1 extractor logic.
/// Works without the original Windows-only ASH.exe, so it can be used on Windows, macOS and Linux.
/// </summary>
public static class AshExtractor
{
    /// <summary>
    /// Decompresses an ASH/ASH0 buffer and returns the decompressed bytes, usually a U8 .arc file.
    /// </summary>
    public static byte[] Decompress(byte[] ashData)
    {
        if (ashData is null)
            throw new ArgumentNullException(nameof(ashData));

        if (ashData.Length < 0x10)
            throw new InvalidDataException("File is too small to be a valid ASH file.");

        uint magic = ReadU32BE(ashData, 0);
        if ((magic & 0xFFFFFF00u) != 0x41534800u) // "ASH\0", "ASH0", ...
            throw new InvalidDataException("This is not a valid ASH file.");

        int decompressedSize = checked((int)(ReadU32BE(ashData, 4) & 0x00FFFFFFu));
        if (decompressedSize < 0)
            throw new InvalidDataException("Invalid decompressed size.");

        int secondStreamOffset = checked((int)ReadU32BE(ashData, 8));
        if (secondStreamOffset < 0x10 || secondStreamOffset + 4 > ashData.Length)
            throw new InvalidDataException("Invalid ASH stream offset.");

        var firstStream = new BitReader(ashData, firstWordOffset: 0x0C, nextWordOffset: 0x10);
        var secondStream = new BitReader(ashData, firstWordOffset: secondStreamOffset, nextWordOffset: secondStreamOffset + 4);

        HuffmanTree literalTree = BuildTree(firstStream, root: 0x200, leafBitCount: 9, maxNodes: 0x400);
        HuffmanTree distanceTree = BuildTree(secondStream, root: 0x800, leafBitCount: 11, maxNodes: 0x1000);

        byte[] output = new byte[decompressedSize];
        int outPos = 0;

        while (outPos < decompressedSize)
        {
            int symbol = literalTree.Decode(firstStream);

            if (symbol < 0x100)
            {
                output[outPos++] = (byte)symbol;
                continue;
            }

            int distanceSymbol = distanceTree.Decode(secondStream);
            int length = symbol - 0xFD;
            int copySource = outPos - distanceSymbol;

            if (length <= 0)
                throw new InvalidDataException($"Invalid ASH copy length: {length}.");

            // The original code copies from (r23 - 1), effectively using distanceSymbol as a 1-based distance.
            copySource--;

            if (copySource < 0)
                throw new InvalidDataException("Invalid ASH back-reference distance.");

            if (outPos + length > decompressedSize)
                throw new InvalidDataException("ASH back-reference exceeds output size.");

            for (int i = 0; i < length; i++)
            {
                output[outPos++] = output[copySource++];
            }
        }

        return output;
    }

    /// <summary>
    /// Decompresses an ASH file from disk.
    /// </summary>
    public static byte[] DecompressFile(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("Input path is empty.", nameof(inputPath));

        return Decompress(File.ReadAllBytes(inputPath));
    }

    /// <summary>
    /// Decompresses an ASH file and writes the result to disk.
    /// If outputPath is omitted, the output path is inputPath + ".arc", matching ASH.exe.
    /// </summary>
    public static string ExtractFile(string inputPath, string? outputPath = null)
    {
        byte[] decompressed = DecompressFile(inputPath);
        outputPath ??= inputPath + ".arc";
        File.WriteAllBytes(outputPath, decompressed);
        return outputPath;
    }

    private static HuffmanTree BuildTree(BitReader reader, int root, int leafBitCount, int maxNodes)
    {
        ushort[] left = new ushort[maxNodes];
        ushort[] right = new ushort[maxNodes];
        ushort[] stack = new ushort[maxNodes * 2];

        int stackCount = 0;
        int node = root;
        int nextNode = root;

        while (true)
        {
            if (reader.ReadBit() != 0)
            {
                if (stackCount + 2 > stack.Length)
                    throw new InvalidDataException("ASH Huffman tree stack overflow.");

                stack[stackCount++] = (ushort)(node | 0x8000); // right child marker
                stack[stackCount++] = (ushort)(node | 0x4000); // left child marker
                node++;
                nextNode++;
                continue;
            }

            uint leafValue = reader.ReadBits(leafBitCount);

            while (true)
            {
                if (stackCount == 0)
                    return new HuffmanTree(left, right, root);

                ushort packed = stack[--stackCount];
                int targetNode = (packed & 0x1FFF);

                if ((packed & 0x8000) != 0)
                {
                    right[targetNode] = (ushort)leafValue;

                    if (stackCount == 0)
                        return new HuffmanTree(left, right, root);

                    // Continue popping while completing right-side leaves, matching the original loop.
                    leafValue = (uint)targetNode;
                    continue;
                }

                left[targetNode] = (ushort)leafValue;
                node = nextNode;
                break;
            }
        }
    }

    private static uint ReadU32BE(byte[] data, int offset)
    {
        if ((uint)offset > (uint)(data.Length - 4))
            throw new InvalidDataException("Unexpected end of ASH data.");

        return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
    }

    private sealed class BitReader
    {
        private readonly byte[] _data;
        private uint _word;
        private int _bitIndex;
        private int _nextWordOffset;

        public BitReader(byte[] data, int firstWordOffset, int nextWordOffset)
        {
            _data = data;
            _word = ReadU32BE(data, firstWordOffset);
            _nextWordOffset = nextWordOffset;
            _bitIndex = 0;
        }

        public int ReadBit()
        {
            int bit = (int)(_word >> 31);

            if (_bitIndex == 31)
            {
                _word = ReadU32BE(_data, _nextWordOffset);
                _nextWordOffset += 4;
                _bitIndex = 0;
            }
            else
            {
                _bitIndex++;
                _word <<= 1;
            }

            return bit;
        }

        public uint ReadBits(int count)
        {
            if (count is < 0 or > 32)
                throw new ArgumentOutOfRangeException(nameof(count));

            uint value = 0;
            for (int i = 0; i < count; i++)
                value = (value << 1) | (uint)ReadBit();

            return value;
        }
    }

    private sealed class HuffmanTree
    {
        private readonly ushort[] _left;
        private readonly ushort[] _right;
        private readonly int _root;

        public HuffmanTree(ushort[] left, ushort[] right, int root)
        {
            _left = left;
            _right = right;
            _root = root;
        }

        public int Decode(BitReader reader)
        {
            int value = _root;

            while (value >= _root)
            {
                if ((uint)value >= (uint)_left.Length)
                    throw new InvalidDataException("Invalid ASH Huffman node.");

                value = reader.ReadBit() == 0 ? _left[value] : _right[value];
            }

            return value;
        }
    }
}
