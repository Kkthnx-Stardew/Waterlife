using System;
using StardewModdingAPI;

namespace Waterlife;

/// <summary>The subset of Generic Mod Config Menu's API that this mod uses.</summary>
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );

    void AddNumberOption(
        IManifest mod,
        Func<int> getValue,
        Action<int> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        int? min = null,
        int? max = null,
        int? interval = null,
        Func<int, string>? formatValue = null,
        string? fieldId = null
    );

    void AddNumberOption(
        IManifest mod,
        Func<float> getValue,
        Action<float> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        float? min = null,
        float? max = null,
        float? interval = null,
        Func<float, string>? formatValue = null,
        string? fieldId = null
    );
}

internal static class GenericModConfigMenu
{
    private const string ModId = "spacechase0.GenericModConfigMenu";

    internal static void Register(ModEntry mod, IModHelper helper, Func<ModConfig> getConfig)
    {
        IGenericModConfigMenuApi? api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(ModId);
        if (api is null)
            return;

        api.Register(
            mod.ModManifest,
            reset: () => mod.Config = new ModConfig(),
            save: () => helper.WriteConfig(mod.Config)
        );

        api.AddNumberOption(
            mod.ModManifest,
            getValue: () => getConfig().MaximumFish,
            setValue: value => getConfig().MaximumFish = value,
            name: () => "Maximum fish",
            tooltip: () => "Upper limit for how many fish can be in the water at once. Lower it if the mod costs too much performance. Default: 500",
            min: 0,
            max: 8000,
            interval: 10
        );

        api.AddNumberOption(
            mod.ModManifest,
            getValue: () => getConfig().FishDensity,
            setValue: value => getConfig().FishDensity = value,
            name: () => "Fish density",
            tooltip: () => "Fish per water tile, so 0.1 means every 10th tile gets a fish. Still limited by Maximum fish. Default: 0.2",
            min: 0.01f,
            max: 2f,
            interval: 0.01f
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().ShowTreasure,
            setValue: value => getConfig().ShowTreasure = value,
            name: () => "Show treasure in the water",
            tooltip: () => "Show paintings, furniture and other non-fish treasures that can be fished here."
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().TrashOutside,
            setValue: value => getConfig().TrashOutside = value,
            name: () => "Trash outside the farm"
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().TrashFarm,
            setValue: value => getConfig().TrashFarm = value,
            name: () => "Trash on the farm"
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().DisableTrashLvl10,
            setValue: value => getConfig().DisableTrashLvl10 = value,
            name: () => "Hide junk at fishing level 10",
            tooltip: () => "Stop showing junk items (trash, driftwood, soggy newspaper...) once your fishing level is 10. Seaweed and algae are still shown."
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().DrawItemShadows,
            setValue: value => getConfig().DrawItemShadows = value,
            name: () => "Draw shadows under items"
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().DrawFishShadows,
            setValue: value => getConfig().DrawFishShadows = value,
            name: () => "Draw shadows under fish"
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().RotateAlgae,
            setValue: value => getConfig().RotateAlgae = value,
            name: () => "Rotate algae and seaweed"
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().FilterEels,
            setValue: value => getConfig().FilterEels = value,
            name: () => "Hide eel-like fish"
        );
    }
}
