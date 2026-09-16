using System.Security.Cryptography;

namespace WiiUSharp.Nus;

/// <summary>
/// H0–H3 SHA-1 tree over the 0xFC00-byte blocks of a hashed content.
/// </summary>
internal sealed class HashTree
{
    private const int Levels = 4;

    private readonly byte[][][] _levels;

    private HashTree(byte[][][] levels)
    {
        _levels = levels;
    }

    public int BlockCount => _levels[0].Length;

    /// <summary>
    /// Hashes every block of the stream; an empty stream is one zero block.
    /// </summary>
    public static HashTree Compute(Stream plain, CancellationToken cancellationToken)
    {
        var h0 = new List<byte[]>();
        var block = new byte[NusFormat.HashBlockDataSize];
        using (var sha = SHA1.Create())
        {
            int read;
            while ((read = Fill(plain, block)) > 0 || h0.Count == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Array.Clear(block, read, block.Length - read);
                h0.Add(sha.ComputeHash(block));
                if (read < block.Length)
                    break;
            }
        }

        var levels = new byte[Levels][][];
        levels[0] = h0.ToArray();
        for (var level = 1; level < Levels; level++)
            levels[level] = Reduce(levels[level - 1]);
        return new HashTree(levels);
    }

    /// <summary>
    /// The 0x400-byte hash header stored before the data of a block.
    /// </summary>
    public byte[] HeaderFor(int block)
    {
        if (block < 0 || block >= BlockCount)
            throw new ArgumentOutOfRangeException(nameof(block));

        var header = new byte[NusFormat.HashBlockHeaderSize];
        var groupSize = NusFormat.HashesPerGroup;
        for (var level = 0; level < Levels - 1; level++)
        {
            var first = block / groupSize * NusFormat.HashesPerGroup;
            for (var i = 0; i < NusFormat.HashesPerGroup; i++)
            {
                var index = first + i;
                if (index < _levels[level].Length)
                    _levels[level][index].CopyTo(header, (level * NusFormat.HashesPerGroup + i) * NusFormat.HashSize);
            }
            groupSize *= NusFormat.HashesPerGroup;
        }
        return header;
    }

    /// <summary>
    /// H0 of a block; its first sixteen bytes are the IV for that block.
    /// </summary>
    public byte[] BlockHash(int block) => (byte[])_levels[0][block].Clone();

    /// <summary>
    /// The H3 table as written to the .h3 file.
    /// </summary>
    public byte[] H3Bytes()
    {
        var h3 = _levels[Levels - 1];
        var bytes = new byte[h3.Length * NusFormat.HashSize];
        for (var i = 0; i < h3.Length; i++)
            h3[i].CopyTo(bytes, i * NusFormat.HashSize);
        return bytes;
    }

    private static int Fill(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private static byte[][] Reduce(byte[][] lower)
    {
        var count = (lower.Length + NusFormat.HashesPerGroup - 1) / NusFormat.HashesPerGroup;
        var upper = new byte[count][];
        var group = new byte[NusFormat.HashesPerGroup * NusFormat.HashSize];
        using var sha = SHA1.Create();
        for (var j = 0; j < count; j++)
        {
            Array.Clear(group, 0, group.Length);
            for (var i = 0; i < NusFormat.HashesPerGroup; i++)
            {
                var index = j * NusFormat.HashesPerGroup + i;
                if (index < lower.Length)
                    lower[index].CopyTo(group, i * NusFormat.HashSize);
            }
            upper[j] = sha.ComputeHash(group);
        }
        return upper;
    }
}
