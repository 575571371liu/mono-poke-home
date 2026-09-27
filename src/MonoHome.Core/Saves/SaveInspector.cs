using PKHeX.Core;

namespace MonoHome.Core.Saves;

public sealed record SaveInspection(string Game, int Generation, string Version, long FileSize);

public static class SaveInspector
{
    public static SaveInspection Inspect(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Inspect(bytes, Path.GetFileName(path));
    }

    public static SaveInspection Inspect(ReadOnlyMemory<byte> bytes, string displayName)
    {
        var save = SaveUtil.GetSaveFile(bytes.ToArray(), displayName) ?? throw new InvalidDataException("Unsupported or corrupted Pokémon save.");
        return new SaveInspection(GetGameName(save), save.Generation, save.Version.ToString(), bytes.Length);
    }

    private static string GetGameName(SaveFile save) => save switch
    {
        SAV3E => "Emerald",
        SAV4HGSS when save.Version == GameVersion.HG => "HeartGold",
        SAV4HGSS when save.Version == GameVersion.SS => "SoulSilver",
        SAV4HGSS => "HeartGold",
        _ => save.Version.ToString(),
    };
}
