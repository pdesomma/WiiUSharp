namespace WiiUSharp.Nus;

/// <summary>
/// One entry of the FST's content table: where the content sits (in 0x8000 sectors, meaningful on a disc), its data size in sectors, who owns it and how it is hashed.
/// </summary>
/// <param name="Index">Content index; matches the TMD.</param>
/// <param name="Offset">Start in sectors from the partition's data.</param>
/// <param name="Size">Data sectors, hash blocks not counted.</param>
/// <param name="OwnerTitleId">Title that owns the content.</param>
/// <param name="GroupId">Group of that title.</param>
/// <param name="HashMode">1 for plain, 2 for hashed blocks.</param>
public sealed record FstContent(int Index, uint Offset, uint Size, ulong OwnerTitleId, uint GroupId, byte HashMode);
