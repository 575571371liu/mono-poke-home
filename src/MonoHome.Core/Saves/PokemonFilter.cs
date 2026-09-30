using PKHeX.Core;

namespace MonoHome.Core.Saves;

public sealed record PokemonFilter(
    int? MinLevel = null,
    int? MaxLevel = null,
    int Type = -1,
    int EggGroup = -1,
    int Gender = -1,
    int Shiny = -1,
    int Egg = -1)
{
    public int Count => (MinLevel is not null ? 1 : 0) + (MaxLevel is not null ? 1 : 0) +
        (Type >= 0 ? 1 : 0) + (EggGroup >= 0 ? 1 : 0) + (Gender >= 0 ? 1 : 0) +
        (Shiny >= 0 ? 1 : 0) + (Egg >= 0 ? 1 : 0);

    public bool Matches(PKM pokemon) => Matches(pokemon.CurrentLevel, pokemon.PersonalInfo.Type1,
        pokemon.PersonalInfo.Type2, pokemon.PersonalInfo.EggGroup1, pokemon.PersonalInfo.EggGroup2,
        pokemon.Gender, pokemon.IsShiny, pokemon.IsEgg);

    public bool Matches(PokemonSlot slot) => Matches(slot.Level, slot.Type1, slot.Type2,
        slot.EggGroup1, slot.EggGroup2, slot.Gender, slot.IsShiny, slot.IsEgg);

    bool Matches(int level, int type1, int type2, int eggGroup1, int eggGroup2,
        int gender, bool shiny, bool egg) =>
        (MinLevel is null || level >= MinLevel) &&
        (MaxLevel is null || level <= MaxLevel) &&
        (Type < 0 || type1 == Type || type2 == Type) &&
        (EggGroup < 0 || eggGroup1 == EggGroup || eggGroup2 == EggGroup) &&
        (Gender < 0 || (Gender == 2 ? gender is not 0 and not 1 : gender == Gender)) &&
        (Shiny < 0 || shiny == (Shiny == 1)) &&
        (Egg < 0 || egg == (Egg == 1));
}
