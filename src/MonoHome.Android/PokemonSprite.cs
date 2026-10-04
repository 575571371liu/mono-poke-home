namespace MonoHome.Android;

using global::Android.Content;
using PKHeX.Core;

static class PokemonSprite
{
    public static int ResourceId(Context context, PKM pokemon) => ResourceId(context, pokemon.Species, pokemon.IsShiny, pokemon.IsEgg);

    public static int ResourceId(Context context, int species, bool shiny, bool egg)
    {
        var name = egg ? "a_egg" : $"a_{species}{(shiny ? "s" : "")}";
        var resource = context.Resources!.GetIdentifier(name, "drawable", context.PackageName);
        return resource != 0 ? resource : context.Resources.GetIdentifier($"a_{species}", "drawable", context.PackageName);
    }
}
