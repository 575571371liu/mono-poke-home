using System.Text.Json;
using PKHeX.Core;

namespace MonoHome.Core.Repository;

public sealed record StoredPokemon(
    string Id,
    int Species,
    string OriginalPath,
    string WorkingPath,
    string ManifestPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string LegalityStatus,
    string? ParentId = null,
    long Revision = 1);

public sealed record WorkingEdit(
    string? Nickname,
    int? Level,
    int? HeldItem,
    int? StatusCondition,
    int? PokerusStrain,
    int? PokerusDays,
    IReadOnlyList<int>? Moves = null,
    IReadOnlyList<int>? IVs = null,
    IReadOnlyList<int>? EVs = null,
    int? Species = null,
    int? Nature = null,
    int? AbilityIndex = null,
    int? Gender = null,
    int? Form = null,
    bool? Shiny = null,
    bool? Egg = null);

public static class LocalRepository
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly HashSet<string> LegalityStates = ["valid", "invalid", "pending", "stale", "unsupported"];

    public static StoredPokemon Upload(PKM pokemon, string root)
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(root, id);
        Directory.CreateDirectory(directory);
        var now = DateTimeOffset.UtcNow;
        var stored = new StoredPokemon(
            id,
            pokemon.Species,
            Path.Combine(directory, "original.pkm"),
            Path.Combine(directory, "working.pkm"),
            Path.Combine(directory, "record.json"),
            now,
            now,
            "pending");
        WritePokemon(stored.OriginalPath, pokemon);
        File.Copy(stored.OriginalPath, stored.WorkingPath);
        WriteRecord(stored);
        return stored;
    }

    public static PKM LoadWorking(StoredPokemon stored) =>
        EntityFormat.GetFromBytes(File.ReadAllBytes(stored.WorkingPath)) ?? throw new InvalidDataException("Stored Pokémon is unreadable.");

    public static PKM ApplyEdit(PKM source, WorkingEdit edit)
    {
        var pokemon = source.Clone();
        var originalShiny = pokemon.IsShiny;
        if (!string.IsNullOrWhiteSpace(edit.Nickname))
        {
            pokemon.Nickname = edit.Nickname;
            pokemon.IsNicknamed = true;
        }
        if (edit.Level is { } level)
        {
            if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(edit.Level));
            pokemon.CurrentLevel = (byte)level;
        }
        if (edit.Species is { } species)
        {
            if (species is < 1 || species > pokemon.MaxSpeciesID) throw new ArgumentOutOfRangeException(nameof(edit.Species));
            pokemon.Species = (ushort)species;
        }
        if (edit.Nature is { } nature)
        {
            if (nature is < 0 or > 24) throw new ArgumentOutOfRangeException(nameof(edit.Nature));
            pokemon.SetPIDNature((Nature)nature);
        }
        if (edit.AbilityIndex is { } ability)
        {
            if (ability < 0 || ability >= pokemon.PersonalInfo.AbilityCount) throw new ArgumentOutOfRangeException(nameof(edit.AbilityIndex));
            pokemon.RefreshAbility(ability);
        }
        if (edit.Gender is { } gender)
        {
            if (gender is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(edit.Gender));
            pokemon.SetPIDGender((byte)gender);
        }
        if (edit.Form is { } form)
        {
            if (form is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(edit.Form));
            pokemon.Form = (byte)form;
        }
        if (edit.Shiny is { } shiny)
        {
            if (shiny && !pokemon.IsShiny) pokemon.SetShiny();
            if (!shiny && pokemon.IsShiny) pokemon.SetShinySID(Shiny.Never);
        }
        else if ((edit.Nature is not null || edit.Gender is not null) && originalShiny && !pokemon.IsShiny)
            pokemon.SetShiny();
        if (edit.Egg is { } egg)
            pokemon.IsEgg = egg;
        if (edit.HeldItem is { } item)
        {
            if (item < 0) throw new ArgumentOutOfRangeException(nameof(edit.HeldItem));
            pokemon.HeldItem = item;
        }
        if (edit.StatusCondition is { } status)
        {
            if (status < 0) throw new ArgumentOutOfRangeException(nameof(edit.StatusCondition));
            pokemon.Status_Condition = status;
        }
        if (edit.PokerusStrain is { } strain)
        {
            if (strain is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(edit.PokerusStrain));
            pokemon.PokerusStrain = strain;
        }
        if (edit.PokerusDays is { } days)
        {
            if (days is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(edit.PokerusDays));
            pokemon.PokerusDays = days;
        }
        if (edit.Moves is { } moves)
        {
            if (moves.Count != 4 || moves.Any(move => move < 0 || move > pokemon.MaxMoveID)) throw new ArgumentOutOfRangeException(nameof(edit.Moves));
            pokemon.SetMoves(moves.Select(move => (ushort)move).ToArray());
        }
        if (edit.IVs is { } ivs)
        {
            if (ivs.Count != 6 || ivs.Any(iv => iv is < 0 or > 31)) throw new ArgumentOutOfRangeException(nameof(edit.IVs));
            pokemon.SetIVs(ivs.ToArray());
        }
        if (edit.EVs is { } evs)
        {
            if (evs.Count != 6 || evs.Any(ev => ev is < 0 or > 255) || evs.Sum() > 510) throw new ArgumentOutOfRangeException(nameof(edit.EVs));
            pokemon.SetEVs(evs.ToArray());
        }
        return pokemon;
    }

    public static StoredPokemon SaveWorking(StoredPokemon stored, PKM pokemon)
    {
        WritePokemon(stored.WorkingPath, pokemon);
        var status = "stale";
        try
        {
            status = new LegalityAnalysis(pokemon).Valid ? "valid" : "invalid";
        }
        catch
        {
            // Keep the explicit stale state if this format cannot be checked locally.
        }
        var updated = stored with { UpdatedAt = DateTimeOffset.UtcNow, LegalityStatus = status, Revision = stored.Revision + 1 };
        WriteRecord(updated);
        return updated;
    }

    public static StoredPokemon CreateLegalCopy(StoredPokemon parent, PKM legalPokemon, string root)
    {
        var copy = Upload(legalPokemon, root);
        var linked = copy with { ParentId = parent.Id, Revision = 1, LegalityStatus = "valid" };
        WriteRecord(linked);
        return linked;
    }

    public static StoredPokemon DiscardEdits(StoredPokemon stored)
    {
        var original = EntityFormat.GetFromBytes(File.ReadAllBytes(stored.OriginalPath)) ?? throw new InvalidDataException("Original Pokémon is unreadable.");
        WritePokemon(stored.WorkingPath, original);
        var updated = stored with { UpdatedAt = DateTimeOffset.UtcNow, LegalityStatus = "stale" };
        WriteRecord(updated);
        return updated;
    }

    public static StoredPokemon SetLegality(StoredPokemon stored, string status)
    {
        if (!LegalityStates.Contains(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        var updated = stored with { UpdatedAt = DateTimeOffset.UtcNow, LegalityStatus = status };
        WriteRecord(updated);
        return updated;
    }

    public static StoredPokemon? GetLatest(string root)
        => List(root).FirstOrDefault();

    public static IReadOnlyList<StoredPokemon> List(string root)
    {
        if (!Directory.Exists(root))
            return [];
        return Directory.EnumerateFiles(root, "record.json", SearchOption.AllDirectories)
            .Select(ReadRecord)
            .Where(record => record is not null && File.Exists(record.WorkingPath) && File.Exists(record.OriginalPath))
            .OrderByDescending(record => record!.UpdatedAt)
            .Cast<StoredPokemon>()
            .ToArray();
    }

    static StoredPokemon? ReadRecord(string path)
    {
        try { return JsonSerializer.Deserialize<StoredPokemon>(File.ReadAllText(path), Json); }
        catch (JsonException) { return null; }
    }

    static void WritePokemon(string path, PKM pokemon)
    {
        var data = new byte[pokemon.SIZE_STORED];
        pokemon.WriteEncryptedDataStored(data);
        WriteAtomic(path, data);
        if (EntityFormat.GetFromBytes(data) is null)
            throw new InvalidDataException("Working copy verification failed.");
    }

    static void WriteRecord(StoredPokemon stored) =>
        WriteAtomic(stored.ManifestPath, JsonSerializer.SerializeToUtf8Bytes(stored, Json));

    static void WriteAtomic(string path, byte[] data)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temporary, data);
        File.Move(temporary, path, true);
    }
}
