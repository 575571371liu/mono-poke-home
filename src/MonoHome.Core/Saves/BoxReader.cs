using PKHeX.Core;

namespace MonoHome.Core.Saves;

public sealed record PokemonSlot(
    int Species,
    string Nickname,
    int Form,
    int Gender,
    int Level,
    string Nature,
    int Ability,
    bool IsShiny,
    int HeldItem,
    int StatusCondition,
    int PokerusStrain,
    int PokerusDays,
    bool IsEgg,
    int RibbonCount,
    string Location,
    int Box,
    int Slot);

public sealed record StorageSlot(int Index, PokemonSlot? Pokemon);

public sealed record StoragePage(string Id, string Name, int Capacity, IReadOnlyList<StorageSlot> Slots);

public static class BoxReader
{
    const int PartyCapacity = 6;

    public static IReadOnlyList<PokemonSlot> Read(string path)
    {
        return Read(File.ReadAllBytes(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<PokemonSlot> Read(ReadOnlyMemory<byte> bytes, string displayName)
    {
        var save = Open(bytes, displayName);
        var result = new List<PokemonSlot>();

        for (var slot = 0; slot < PartySlotCount(save); slot++)
            Add(save.GetPartySlotAtIndex(slot), "Party", -1, slot, result);

        for (var box = 0; box < save.BoxCount; box++)
        for (var slot = 0; slot < save.BoxSlotCount; slot++)
            Add(save.GetBoxSlotAtIndex(box, slot), "Box", box, slot, result);

        return result;
    }

    public static IReadOnlyList<StoragePage> ReadPages(ReadOnlyMemory<byte> bytes, string displayName)
    {
        var save = Open(bytes, displayName);
        var pages = new List<StoragePage>
        {
            new("party", "随身携带", PartyCapacity,
                Enumerable.Range(0, PartySlotCount(save))
                    .Select(index => new StorageSlot(index, ToSlot(save.GetPartySlotAtIndex(index), "Party", -1, index)))
                    .ToArray()),
        };

        for (var box = 0; box < save.BoxCount; box++)
        {
            var slots = Enumerable.Range(0, save.BoxSlotCount)
                .Select(index => new StorageSlot(index, ToSlot(save.GetBoxSlotAtIndex(box, index), "Box", box, index)))
                .ToArray();
            pages.Add(new StoragePage($"box-{box}", $"仓库 {box + 1}", save.BoxSlotCount, slots));
        }

        return pages;
    }

    /// <summary>
    /// Number of readable party positions.
    ///
    /// Storage-only saves (Pokémon Box RS, Stadium, Bank dumps) report a negative party
    /// offset, so reading a party slot would throw; they expose no party at all.
    /// </summary>
    static int PartySlotCount(SaveFile save) => save.HasParty ? Math.Clamp(save.PartyCount, 0, PartyCapacity) : 0;

    public static PKM ReadPokemon(string path, PokemonSlot selected)
    {
        var save = Open(path);
        var pokemon = selected.Location == "Party"
            ? save.GetPartySlotAtIndex(selected.Slot)
            : selected.Location == "Box"
                ? save.GetBoxSlotAtIndex(selected.Box, selected.Slot)
                : throw new InvalidDataException("Unknown source location.");
        if (pokemon.Species == 0 || pokemon.Species != selected.Species)
            throw new InvalidDataException("Selected Pokémon no longer matches the source save.");
        return pokemon.Clone();
    }

    /// <summary>Parses a save file, with a single place defining the failure message.</summary>
    public static SaveFile Open(string path) => SaveUtil.GetSaveFile(path) ?? throw new InvalidDataException(UnreadableSave);

    /// <summary>Parses a save from bytes already in memory.</summary>
    public static SaveFile Open(ReadOnlyMemory<byte> bytes, string displayName) =>
        SaveUtil.GetSaveFile(bytes.ToArray(), displayName) ?? throw new InvalidDataException(UnreadableSave);

    const string UnreadableSave = "Unsupported or corrupted Pokémon save.";

    private static void Add(PKM pk, string location, int box, int slot, List<PokemonSlot> result)
    {
        var value = ToSlot(pk, location, box, slot);
        if (value is not null)
            result.Add(value);
    }

    private static PokemonSlot? ToSlot(PKM pk, string location, int box, int slot)
    {
        if (pk.Species == 0)
            return null;
        return new PokemonSlot(
            pk.Species,
            pk.Nickname,
            pk.Form,
            pk.Gender,
            pk.CurrentLevel,
            pk.Nature.ToString(),
            pk.Ability,
            pk.IsShiny,
            pk.HeldItem,
            pk.Status_Condition,
            pk.PokerusStrain,
            pk.PokerusDays,
            pk.IsEgg,
            pk is IRibbonSetRibbons ribbons ? ribbons.RibbonCount : 0,
            location,
            box,
            slot);
    }
}
