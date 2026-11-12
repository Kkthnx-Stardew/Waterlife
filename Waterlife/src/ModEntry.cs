using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using xTile;

namespace Waterlife;

public sealed class ModEntry : Mod
{
    /// <summary>Asset name of the per-location rules, unchanged so content packs keep working.</summary>
    internal const string LocationDataAsset = "Mods/shekurika.VisibleFish/LocationData";

    /// <summary>Item and texture id of the extra legendary fish added by this mod.</summary>
    internal const string LegendaryFishItemId = "asdkmpwadp1231sda21";

    private const string AquariumDataAsset = "Data/AquariumFish";

    internal static ModEntry Instance { get; private set; } = null!;

    internal static PerScreen<FishManager?> Managers { get; } = new();

    internal ModConfig Config { get; set; } = new();

    private FishVisualCache visuals = null!;
    private bool stardewValleyExpandedLoaded;
    private Map? statueMap;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Log.Initialize(this.Monitor);

        this.Config = helper.ReadConfig<ModConfig>();
        this.visuals = new FishVisualCache(helper);
        this.stardewValleyExpandedLoaded = helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP");

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Content.AssetRequested += this.OnAssetRequested;

        new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Config = this.Helper.ReadConfig<ModConfig>();

        GenericModConfigMenu.Register(this, this.Helper, () => this.Config);

        GameLocation.RegisterTileAction(
            "VF.SpawnBasket",
            (location, args, who, tile) =>
            {
                Game1.playSound("skeletonStep");
                Managers.Value?.AddDecoration("(F)2425", 45 + Game1.random.Next(-3, 3), 22 + Game1.random.Next(-2, 2));
                return true;
            }
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        FishManager manager = new(this.Helper, this.visuals);
        manager.ResetForLocation(Game1.currentLocation);
        Managers.Value = manager;
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        Managers.Value = null;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        Managers.Value?.Update(Game1.currentGameTime);
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        Managers.Value?.ResetForLocation(e.NewLocation);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(LocationDataAsset))
        {
            e.LoadFromModFile<Dictionary<string, LocationFishData>>("assets/LocationData.json", AssetLoadPriority.High);
        }
        else if (e.Name.IsEquivalentTo(AquariumDataAsset))
        {
            // Gives the extra legendary fish an aquarium sprite. This previously targeted a
            // misspelled asset name, so the fish always fell back to the generic sprite.
            e.Edit(
                asset => asset.AsDictionary<string, string>().Data[LegendaryFishItemId] = $"0/fish/////{LegendaryFishItemId}",
                AssetEditPriority.Late
            );
        }
        else if (e.Name.IsEquivalentTo(LegendaryFishItemId))
        {
            // assets/misc.txt is a 24x48 PNG despite its extension.
            e.LoadFromModFile<Texture2D>("assets/misc.txt", AssetLoadPriority.High);
        }
        else if (e.Name.IsEquivalentTo("Maps/VFStatue"))
        {
            e.LoadFromModFile<Texture2D>("assets/.VFStatue.png", AssetLoadPriority.High);
        }
        else if (this.stardewValleyExpandedLoaded && e.Name.IsEquivalentTo("Maps/Woods"))
        {
            // Overlay, not Replace: the statue sprite is mostly empty and must not punch holes in the map.
            e.Edit(asset => asset.AsMap().PatchMap(this.GetStatueMap(), null, new Rectangle(72, 25, 2, 2), PatchMapMode.Overlay));
        }
    }

    private Map GetStatueMap()
    {
        return this.statueMap ??= this.Helper.ModContent.Load<Map>("assets/statue.tmx");
    }
}
