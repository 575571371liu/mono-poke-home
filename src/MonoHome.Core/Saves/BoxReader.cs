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

public static class BoxReader
{
    public static IReadOnlyList<PokemonSlot> Read(string path)
    {
        return Read(File.ReadAllBytes(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<PokemonSlot> Read(ReadOnlyMemory<byte> bytes, string displayName)
    {
        var save = SaveUtil.GetSaveFile(bytes.ToArray(), displayName) ?? throw new InvalidDataException("Unsupported or corrupted Pokémon save.");
        var result = new List<PokemonSlot>();

        for (var slot = 0; slot < Math.Min(save.PartyCount, 6); slot++)
            Add(save.GetPartySlotAtIndex(slot), "Party", -1, slot, result);

        for (var box = 0; box < save.BoxCount; box++)
        for (var slot = 0; slot < save.BoxSlotCount; slot++)
            Add(save.GetBoxSlotAtIndex(box, slot), "Box", box, slot, result);

        return result;
    }

    public static PKM ReadPokemon(string path, PokemonSlot selected)
    {
        var save = SaveUtil.GetSaveFile(path) ?? throw new InvalidDataException("Unsupported or corrupted Pokémon save.");
        var pokemon = selected.Location == "Party"
            ? save.GetPartySlotAtIndex(selected.Slot)
            : selected.Location == "Box"
                ? save.GetBoxSlotAtIndex(selected.Box, selected.Slot)
                : throw new InvalidDataException("Unknown source location.");
        if (pokemon.Species == 0 || pokemon.Species != selected.Species)
            throw new InvalidDataException("Selected Pokémon no longer matches the source save.");
        return pokemon.Clone();
    }

    private static void Add(PKM pk, string location, int box, int slot, List<PokemonSlot> result)
    {
        if (pk.Species == 0)
            return;
        result.Add(new PokemonSlot(
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
            slot));
    }
}
