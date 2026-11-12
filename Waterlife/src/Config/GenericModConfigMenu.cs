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

        // Menu text lives in i18n/default.json so it can be translated.
        Func<string> Text(string key) => () => helper.Translation.Get(key).ToString();

        api.Register(
            mod.ModManifest,
            reset: () => mod.Config = new ModConfig(),
            save: () => helper.WriteConfig(mod.Config)
        );

        api.AddNumberOption(
            mod.ModManifest,
            getValue: () => getConfig().MaximumFish,
            setValue: value => getConfig().MaximumFish = value,
            name: Text("config.maximum-fish.name"),
            tooltip: Text("config.maximum-fish.tooltip"),
            min: 0,
            max: 8000,
            interval: 10
        );

        api.AddNumberOption(
            mod.ModManifest,
            getValue: () => getConfig().FishDensity,
            setValue: value => getConfig().FishDensity = value,
            name: Text("config.fish-density.name"),
            tooltip: Text("config.fish-density.tooltip"),
            min: 0.01f,
            max: 2f,
            interval: 0.01f
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().ShowTreasure,
            setValue: value => getConfig().ShowTreasure = value,
            name: Text("config.show-treasure.name"),
            tooltip: Text("config.show-treasure.tooltip")
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().TrashOutside,
            setValue: value => getConfig().TrashOutside = value,
            name: Text("config.trash-outside.name")
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().TrashFarm,
            setValue: value => getConfig().TrashFarm = value,
            name: Text("config.trash-farm.name")
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().DisableTrashLvl10,
            setValue: value => getConfig().DisableTrashLvl10 = value,
            name: Text("config.disable-trash-lvl10.name"),
            tooltip: Text("config.disable-trash-lvl10.tooltip")
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().DrawItemShadows,
            setValue: value => getConfig().DrawItemShadows = value,
            name: Text("config.draw-item-shadows.name")
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().DrawFishShadows,
            setValue: value => getConfig().DrawFishShadows = value,
            name: Text("config.draw-fish-shadows.name")
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().RotateAlgae,
            setValue: value => getConfig().RotateAlgae = value,
            name: Text("config.rotate-algae.name")
        );

        api.AddBoolOption(
            mod.ModManifest,
            getValue: () => getConfig().FilterEels,
            setValue: value => getConfig().FilterEels = value,
            name: Text("config.filter-eels.name")
        );
    }
}
