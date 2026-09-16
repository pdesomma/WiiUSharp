namespace WiiUSharp.Nus;

/// <summary>
/// What a ticket says: which title, and its wrapped key.
/// </summary>
/// <param name="TitleId">Title the ticket is for.</param>
/// <param name="TitleKey">Title key as stored, wrapped with the common key.</param>
public sealed record TicketInfo(TitleId TitleId, EncryptedTitleKey TitleKey);
