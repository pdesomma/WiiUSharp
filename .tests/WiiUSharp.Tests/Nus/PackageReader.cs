using System.Text;

namespace WiiUSharp.Nus.Tests;

/// <summary>
/// Independent reader: walks the FST and decrypts contents the way the console would.
/// </summary>
internal sealed class PackageReader
{
    private readonly string _directory;
    private readonly byte[] _fst;
    private readonly byte[] _key;
    private readonly bool[] _hashed;
    private readonly Dictionary<int, byte[]> _plain = new();

    public PackageReader(string directory, TitleKey key)
    {
        _directory = directory;
        _key = key.ToArray();

        var tmd = File.ReadAllBytes(Path.Combine(directory, "title.tmd"));
        var count = Reference.ReadUInt16(tmd, 0x1DE);
        _hashed = new bool[count];
        for (var i = 0; i < count; i++)
            _hashed[i] = (Reference.ReadUInt16(tmd, Tmd.ContentRecordsOffset + i * NusFormat.ContentRecordSize + 6) & 0x0002) != 0;

        _fst = Reference.AesCbc(_key, new byte[16], File.ReadAllBytes(Path.Combine(directory, NusFormat.ContentFileName(0))), encrypt: false);
        if (Encoding.ASCII.GetString(_fst, 0, 3) != "FST")
            throw new InvalidDataException("Bad FST magic.");
    }

    public byte[] Read(string path)
    {
        var contentCount = (int)Reference.ReadUInt32(_fst, 8);
        var entriesAt = 0x20 + contentCount * 0x20;
        var entryCount = (int)Reference.ReadUInt32(_fst, entriesAt + 8);
        var stringsAt = entriesAt + entryCount * 0x10;

        var parts = path.TrimStart('/').Split('/');
        var index = 0;
        var end = entryCount;
        foreach (var part in parts)
        {
            var found = -1;
            for (var i = index + 1; i < end;)
            {
                var at = entriesAt + i * 0x10;
                var isDirectory = (_fst[at] & 0x01) != 0;
                var nameOffset = _fst[at + 1] << 16 | _fst[at + 2] << 8 | _fst[at + 3];
                var nameEnd = Array.IndexOf(_fst, (byte)0, stringsAt + nameOffset);
                var name = Encoding.UTF8.GetString(_fst, stringsAt + nameOffset, nameEnd - stringsAt - nameOffset);
                if (name == part)
                {
                    found = i;
                    break;
                }
                i = isDirectory ? (int)Reference.ReadUInt32(_fst, at + 8) : i + 1;
            }
            if (found < 0)
                throw new FileNotFoundException(path);
            index = found;
            var foundAt = entriesAt + found * 0x10;
            if ((_fst[foundAt] & 0x01) != 0)
                end = (int)Reference.ReadUInt32(_fst, foundAt + 8);
        }

        var entry = entriesAt + index * 0x10;
        var offset = (long)Reference.ReadUInt32(_fst, entry + 4) << 5;
        var size = (int)Reference.ReadUInt32(_fst, entry + 8);
        var content = Reference.ReadUInt16(_fst, entry + 14);
        return Reference.Slice(Plain(content), (int)offset, size);
    }

    private byte[] Plain(int content)
    {
        if (_plain.TryGetValue(content, out var cached))
            return cached;

        var encrypted = File.ReadAllBytes(Path.Combine(_directory, NusFormat.ContentFileName(content)));
        byte[] plain;
        if (!_hashed[content])
        {
            plain = Reference.AesCbc(_key, Reference.IndexIv(content), encrypted, encrypt: false);
        }
        else
        {
            var blocks = encrypted.Length / NusFormat.HashBlockSize;
            plain = new byte[blocks * NusFormat.HashBlockDataSize];
            for (var b = 0; b < blocks; b++)
            {
                var header = Reference.AesCbc(_key, Reference.IndexIv(content), Reference.Slice(encrypted, b * NusFormat.HashBlockSize, NusFormat.HashBlockHeaderSize), encrypt: false);
                header[1] ^= (byte)content;
                var iv = Reference.Slice(header, b % 16 * 20, 16);
                var data = Reference.AesCbc(_key, iv, Reference.Slice(encrypted, b * NusFormat.HashBlockSize + NusFormat.HashBlockHeaderSize, NusFormat.HashBlockDataSize), encrypt: false);
                CollectionAssert.AreEqual(Reference.Sha1(data), Reference.Slice(header, b % 16 * 20, 20), $"H0 of block {b} in content {content}");
                data.CopyTo(plain, b * NusFormat.HashBlockDataSize);
            }
        }
        _plain[content] = plain;
        return plain;
    }
}
