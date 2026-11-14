using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using xTile;

namespace Waterlife;

public sealed class ModEntry : Mod
{
    /// <summary>
    /// Legacy asset name of the per-location rules. Kept exactly as Visible Fish used it, because
    /// Stardew Valley Expanded, Ridgeside and Sunberry patch this key by name.
    /// </summary>
    internal const string LocationDataAsset = "Mods/shekurika.VisibleFish/LocationData";

    private const string SpawnBasketAction = "Kkthnx.Waterlife_SpawnBasket";

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

        // Lets LocationData.json spawns wait for a mod, e.g. "Kkthnx.Waterlife_MOD_LOADED Some.ModId".
        GameStateQuery.Register(
            "Kkthnx.Waterlife_MOD_LOADED",
            (query, _) => query.Length > 1 && helper.ModRegistry.IsLoaded(query[1])
        );

        new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Config = this.Helper.ReadConfig<ModConfig>();

        GenericModConfigMenu.Register(this, this.Helper, () => this.Config);

        GameLocation.RegisterTileAction(
            SpawnBasketAction,
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
        // SMAPI raises this for every player in multiplayer. Another player's warp must not rebuild
        // the water for the local screen.
        if (!e.IsLocalPlayer)
            return;

        Managers.Value?.ResetForLocation(e.NewLocation);
    }

    /// <summary>Throw away the current fish on every screen so a changed setting shows up right away.</summary>
    internal static void RefreshAll()
    {
        foreach (KeyValuePair<int, FishManager?> screen in Managers.GetActiveValues())
            screen.Value?.Invalidate();
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(LocationDataAsset))
        {
            e.LoadFromModFile<Dictionary<string, LocationFishData>>("assets/LocationData.json", AssetLoadPriority.High);
        }
        else if (this.stardewValleyExpandedLoaded && e.Name.IsEquivalentTo("Maps/Woods"))
        {
            // Overlay, not Replace: the empty corners of the statue map must not punch holes in the woods.
            // The map is two tiles wide and three tall, and its bottom row is the part you bump into.
            e.Edit(asset => asset.AsMap().PatchMap(this.GetStatueMap(), null, new Rectangle(72, 24, 2, 3), PatchMapMode.Overlay));
        }
    }

    private Map GetStatueMap()
    {
        return this.statueMap ??= this.Helper.ModContent.Load<Map>("assets/statue.tmx");
    }
}
