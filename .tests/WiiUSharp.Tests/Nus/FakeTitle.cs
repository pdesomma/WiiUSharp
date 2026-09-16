namespace WiiUSharp.Nus.Tests;

/// <summary>
/// Builds a small code/content/meta tree and knows what each file holds.
/// </summary>
internal static class FakeTitle
{
    public const string AppXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><app type=\"complex\" access=\"777\"><version type=\"unsignedInt\" length=\"4\">16</version><os_version type=\"hexBinary\" length=\"8\">000500101000400A</os_version><title_id type=\"hexBinary\" length=\"8\">000500021ABCDE00</title_id><title_version type=\"hexBinary\" length=\"2\">0011</title_version><sdk_version type=\"unsignedInt\" length=\"4\">21204</sdk_version><app_type type=\"hexBinary\" length=\"4\">80000000</app_type><group_id type=\"hexBinary\" length=\"4\">00001ABC</group_id></app>";

    public static readonly IReadOnlyDictionary<string, byte[]> Files = new Dictionary<string, byte[]>
    {
        ["code/app.xml"] = System.Text.Encoding.UTF8.GetBytes(AppXml),
        ["code/cos.xml"] = Reference.Pattern(300, 1),
        ["code/fw.img"] = Reference.Pattern(0x9000, 2),
        ["code/game.rpx"] = Reference.Pattern(70000, 3),
        ["content/a.bin"] = Reference.Pattern(100, 4),
        ["content/sub/b.bin"] = Reference.Pattern(NusFormat.HashBlockDataSize + 50, 5),
        ["content/sub/hif_000000.nfs"] = Reference.Pattern(48, 6),
        ["meta/bootLogoTex.tga"] = Reference.Pattern(500, 7),
        ["meta/iconTex.tga"] = Reference.Pattern(640, 8),
        ["meta/meta.xml"] = Reference.Pattern(200, 9),
    };

    public static string Create(string root)
    {
        foreach (var pair in Files)
        {
            var path = Path.Combine(root, pair.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, pair.Value);
        }
        return root;
    }
}
