using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace Waterlife;

/// <summary>
/// Owns everything that is drawn in the water for one screen. One instance per save and screen.
/// </summary>
internal sealed class FishManager
{
    /// <summary>Wait a few ticks after a warp so the new location is fully loaded before reading its water data.</summary>
    private const int LocationSettleTicks = 5;

    /// <summary>Maximum entities created per tick, so entering a big map does not cause a spawn burst.</summary>
    private const int MaxSpawnsPerTick = 12;

    /// <summary>Stop trying in a location once this many attempts produced nothing at all.</summary>
    private const int GiveUpAfterAttempts = 40;

    /// <summary>How often to re-check a location whose water data was not ready yet, and how many times.</summary>
    private const int PopulateRetryTicks = 60;
    private const int MaxPopulateRetries = 3;

    private readonly IModHelper helper;
    private readonly FishVisualCache visuals;
    private readonly Random rng = new();
    private readonly List<AquariumFish> fish = new();
    private readonly List<WaterDebris> debris = new();
    private readonly HashSet<string> exclusiveFish = new();

    private WaterGrid? grid;
    private GameLocation? activeLocation;
    private LocationFishData? rules;
    private string ruleKey = "";
    private bool farmOrMine;
    private bool marineLocation;
    private bool spawnTrash;
    private bool drawInFront;
    private bool spawning;
    private int spawnDelay = -1;
    private int populateRetries;
    private int attempts;
    private int additions;
    private int rolls;
    private float retryInMs;

    // Profiling for the populate/fill log line.
    private double gridMs;
    private double setupMs;
    private long fillStarted;
    private bool fillLogged;

    public FishManager(IModHelper helper, FishVisualCache visuals)
    {
        this.helper = helper;
        this.visuals = visuals;
    }

    public bool HasContent => this.fish.Count > 0 || this.debris.Count > 0;

    /// <summary>Whether this location's entities are drawn after the water instead of before it.</summary>
    public bool DrawInFront => this.drawInFront;

    private static ModConfig Config => ModEntry.Instance.Config;

    private int TargetCount =>
        this.grid is null ? 0 : (int)MathF.Min(this.grid.SpawnTileCount * Config.FishDensity, Config.MaximumFish);

    public void ResetForLocation(GameLocation? location)
    {
        this.fish.Clear();
        this.debris.Clear();
        this.exclusiveFish.Clear();
        this.grid = null;
        this.activeLocation = location;
        this.rules = null;
        this.spawning = false;
        this.drawInFront = false;
        this.populateRetries = 0;
        this.attempts = 0;
        this.additions = 0;
        this.rolls = 0;
        this.retryInMs = 0f;
        this.gridMs = 0.0;
        this.setupMs = 0.0;
        this.fillStarted = 0L;
        this.fillLogged = false;
        this.spawnDelay = -1;

        if (location is null)
            return;

        this.ruleKey = location is MineShaft ? "UndergroundMine" : location.Name;
        this.farmOrMine = location is Farm || location is MineShaft;
        this.marineLocation = location.Name.Contains("marine", StringComparison.OrdinalIgnoreCase);
        this.spawnDelay = LocationSettleTicks;
    }

    public void Update(GameTime time)
    {
        if (!Context.IsWorldReady)
            return;

        GameLocation? location = Game1.currentLocation;
        if (location is null)
            return;

        // Some mods swap Game1.currentLocation without a warp; rebuild instead of drawing stale fish.
        if (!ReferenceEquals(location, this.activeLocation))
        {
            this.ResetForLocation(location);
            return;
        }

        if (this.spawnDelay >= 0)
        {
            if (--this.spawnDelay == 0)
            {
                try
                {
                    this.Populate(location);
                }
                catch (Exception ex)
                {
                    // A broken modded location, query or map should not break the update loop.
                    Log.ErrorOnce($"populate:{location.NameOrUniqueName}", $"Could not prepare {location.NameOrUniqueName}.", ex);
                    this.spawning = false;
                }
            }

            return;
        }

        Rectangle viewport = ExpandedViewport(Game1.viewport);

        for (int i = this.fish.Count - 1; i >= 0; i--)
        {
            AquariumFish entity = this.fish[i];
            entity.Update(time, viewport);
            if (entity.IsExpired)
                this.fish.RemoveAt(i);
        }

        for (int i = this.debris.Count - 1; i >= 0; i--)
        {
            WaterDebris entity = this.debris[i];
            entity.Update(time, viewport);
            if (entity.IsExpired)
                this.debris.RemoveAt(i);
        }

        if (this.spawning)
        {
            this.TopUp(location);
        }
        else if (this.retryInMs > 0f)
        {
            // Nothing was catchable when we looked; time, weather or the player's level may have
            // changed since, so try again later instead of leaving the location empty.
            this.retryInMs -= (float)time.ElapsedGameTime.TotalMilliseconds;
            if (this.retryInMs <= 0f)
            {
                this.retryInMs = 0f;
                this.attempts = 0;
                this.additions = 0;
                this.rolls = 0;
                this.fillLogged = false;
                this.fillStarted = Stopwatch.GetTimestamp();
                this.spawning = this.grid is not null;
            }
        }
    }

    public void Draw(SpriteBatch b, Rectangle viewport)
    {
        bool drawFishShadows = Config.DrawFishShadows;
        bool drawItemShadows = Config.DrawItemShadows;

        for (int i = 0; i < this.fish.Count; i++)
            this.fish[i].Draw(b, viewport, 0.5f, drawFishShadows);

        for (int i = 0; i < this.debris.Count; i++)
            this.debris[i].Draw(b, viewport, drawItemShadows);
    }

    /// <summary>Add a persistent decoration, used by the <c>VF.SpawnBasket</c> tile action.</summary>
    public void AddDecoration(string qualifiedItemId, int tileX, int tileY)
    {
        if (this.grid is null)
            return;

        this.AddDebris(qualifiedItemId, tileX, tileY, persistent: true, asFurniture: true);
    }

    /// <summary>Widen the draw/cull area so entities do not pop in at the screen edges.</summary>
    public static Rectangle ExpandedViewport(xTile.Dimensions.Rectangle viewport)
    {
        const int margin = 128;
        return new Rectangle(viewport.X - margin, viewport.Y - margin, viewport.Width + margin * 2, viewport.Height + margin * 2);
    }

    /// <summary>Prepare a location: build its water grid, read its rules and place its fixed items.</summary>
    private void Populate(GameLocation location)
    {
        long start = Stopwatch.GetTimestamp();
        this.grid = WaterGrid.TryBuild(location.waterTiles);
        this.gridMs = Elapsed(start);

        if (this.grid is null || this.grid.SpawnTileCount == 0)
        {
            // Farm ponds and map overrides can finish loading after the warp event, which is why
            // the original mod needed a hotfix for the Beach and 4-Corner farms.
            if (this.populateRetries < MaxPopulateRetries)
            {
                this.populateRetries++;
                this.spawnDelay = PopulateRetryTicks;
                return;
            }

            Log.Trace($"{location.Name}: no water to spawn in.");
            this.spawning = false;
            return;
        }

        this.rules = this.LoadRules(this.ruleKey);
        if (this.rules is not null && !Enum.TryParse(this.rules.SpawnTrash, ignoreCase: true, out TrashMode _))
        {
            Log.WarnOnce(
                $"trashmode:{this.rules.SpawnTrash}",
                $"{this.ruleKey}: unknown SpawnTrash value '{this.rules.SpawnTrash}', using the default."
            );
        }

        this.spawnTrash = location.IsFarm ? Config.TrashFarm : Config.TrashOutside;
        switch (this.rules?.TrashMode ?? TrashMode.Default)
        {
            case TrashMode.Always:
                this.spawnTrash = true;
                break;
            case TrashMode.Never:
                this.spawnTrash = false;
                break;
        }

        this.drawInFront = this.rules?.Underwater ?? false;
        this.spawning = true;

        Log.Trace($"{location.Name}: {this.grid.SpawnTileCount} water tiles, target {this.TargetCount} entities.");

        if (this.rules is not null)
            this.SpawnFixedItems(location, this.rules);

        if (location is MineShaft && this.TargetCount > 5)
        {
            for (int i = 0; i < 5; i++)
            {
                Point tile = this.grid.RandomSpawnTile(this.rng);
                this.AddDebris("(O)CaveJelly", tile.X, tile.Y, persistent: false);
            }
        }

        if (this.ruleKey == "Custom_SBV_SunberryVillage" && this.rng.NextDouble() < 0.01)
            this.AddFish(ModEntry.LegendaryFishItemId, 18, 71, persistent: true);

        this.setupMs = Elapsed(start) - this.gridMs;
        this.fillStarted = Stopwatch.GetTimestamp();
        this.fillLogged = false;
        this.rolls = 0;

        // Logs right away when fixed items already fill the target, e.g. a tiny pond.
        this.LogFill(location.Name, force: false);
    }

    private void SpawnFixedItems(GameLocation location, LocationFishData locationRules)
    {
        foreach (LocationSpawn spawn in locationRules.Spawns.Values)
        {
            if (string.IsNullOrWhiteSpace(spawn.QualifiedItemId))
                continue;

            if (!string.IsNullOrWhiteSpace(spawn.Condition)
                && !GameStateQuery.CheckConditions(spawn.Condition, location, Game1.player))
                continue;

            if (this.grid is null || spawn.X < 0 || spawn.Y < 0 || spawn.X >= this.grid.MapWidth || spawn.Y >= this.grid.MapHeight)
            {
                Log.Warn($"{this.ruleKey}: '{spawn.QualifiedItemId}' is outside the map at ({spawn.X},{spawn.Y}).");
                continue;
            }

            Item? item = TryCreateItem(spawn.QualifiedItemId);
            if (item is null)
                continue;

            if (!spawn.AllowMore)
                this.exclusiveFish.Add(item.QualifiedItemId);

            if (this.visuals.Get(item.ItemId) is not null)
                this.AddFish(item.ItemId, spawn.X, spawn.Y, persistent: true);
            else
                this.AddDebris(item.QualifiedItemId, spawn.X, spawn.Y, persistent: true);
        }
    }

    private void TopUp(GameLocation location)
    {
        if (this.grid is null)
            return;

        int missing = this.TargetCount - (this.fish.Count + this.debris.Count);
        if (missing <= 0)
            return;

        int budget = Math.Min(missing, MaxSpawnsPerTick);
        for (int i = 0; i < budget; i++)
        {
            Point tile = this.grid.RandomSpawnTile(this.rng);
            this.attempts++;

            if (!location.isTileFishable(tile.X, tile.Y))
                continue;

            this.rolls++;
            Item? item = this.RollFish(location, tile);
            if (item is null || this.exclusiveFish.Contains(item.QualifiedItemId))
                continue;

            if (this.AddItem(item, tile.X, tile.Y))
                this.additions++;
        }

        this.LogFill(location.Name, force: false);

        if (this.additions == 0 && this.attempts >= GiveUpAfterAttempts)
        {
            this.LogFill(location.Name, force: true);
            Log.Trace($"{this.ruleKey}: nothing showable here after {this.attempts} attempts, retrying in 30s.");
            this.spawning = false;
            this.retryInMs = 30000f;
        }
    }

    /// <summary>Ask the game which fish can be caught at this tile, exactly like a fishing rod would.</summary>
    private Item? RollFish(GameLocation location, Point tile)
    {
        Farmer? player = Game1.player;
        if (player is null)
            return null;

        Vector2 bobber = new(tile.X, tile.Y);
        int waterDepth = this.grid?.GetDepth(tile.X, tile.Y) ?? 1;

        try
        {
            if (this.farmOrMine)
                return location.getFish(0f, "", waterDepth, player, 0.0, bobber);

            return GameLocation.GetFishFromLocationData(
                location.Name,
                bobber,
                waterDepth,
                player,
                isTutorialCatch: false,
                isInherited: false,
                location
            );
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"roll:{location.NameOrUniqueName}", $"Could not pick a fish for {location.NameOrUniqueName}.", ex);
            return null;
        }
    }

    private bool AddItem(Item item, int tileX, int tileY)
    {
        if (this.fish.Count + this.debris.Count >= Config.MaximumFish)
            return false;

        FishVisual? visual = this.visuals.Get(item.ItemId);
        if (visual is not null && item.Category == SObject.FishCategory)
            return this.AddFish(item.ItemId, tileX, tileY, persistent: false, visual);

        if (item is not SObject || item is Furniture)
        {
            return Config.ShowTreasure && this.AddDebris(item.QualifiedItemId, tileX, tileY, persistent: false);
        }

        return this.ShouldShowTrash(item) && this.AddDebris(item.QualifiedItemId, tileX, tileY, persistent: false);
    }

    private bool ShouldShowTrash(Item item)
    {
        if (!this.spawnTrash)
            return false;

        if (Config.DisableTrashLvl10
            && item.Category == SObject.junkCategory
            && Game1.player is { FishingLevel: >= 10 })
            return false;

        return true;
    }

    private bool AddFish(string itemId, int tileX, int tileY, bool persistent, FishVisual? visual = null)
    {
        if (this.grid is null || this.fish.Count + this.debris.Count >= Config.MaximumFish)
            return false;

        visual ??= this.visuals.Get(itemId);
        if (visual is null)
            return false;

        if (Config.FilterEels && visual.SwimType == FishSwimType.Eel)
            return false;

        this.fish.Add(new AquariumFish(visual, this.grid, this.rng, tileX, tileY, persistent, this.marineLocation));
        return true;
    }

    private bool AddDebris(string qualifiedItemId, int tileX, int tileY, bool persistent, bool asFurniture = false)
    {
        if (this.grid is null || this.fish.Count + this.debris.Count >= Config.MaximumFish)
            return false;

        try
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(qualifiedItemId);
            this.debris.Add(new WaterDebris(data, this.grid, this.rng, tileX, tileY, persistent, asFurniture));
            return true;
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"debris:{qualifiedItemId}", $"Could not show '{qualifiedItemId}' in the water.", ex);
            return false;
        }
    }

    private LocationFishData? LoadRules(string key)
    {
        try
        {
            Dictionary<string, LocationFishData> data =
                this.helper.GameContent.Load<Dictionary<string, LocationFishData>>(ModEntry.LocationDataAsset);

            return data.TryGetValue(key, out LocationFishData? locationRules) ? locationRules : null;
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("LocationData", "Could not load the mod's LocationData asset.", ex);
            return null;
        }
    }

    /// <summary>Get milliseconds elapsed since a <see cref="Stopwatch"/> timestamp.</summary>
    private static double Elapsed(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Log how many entities this location ended up with and how long it took, once per fill.
    /// The counts and timings are what the spawn budget and grid build are actually worth in game.
    /// </summary>
    private void LogFill(string locationName, bool force)
    {
        if (this.fillLogged)
            return;

        int total = this.fish.Count + this.debris.Count;
        if (!force && total < this.TargetCount)
            return;

        this.fillLogged = true;
        Log.Info(
            $"{locationName}: {this.fish.Count} fish + {this.debris.Count} debris on {this.grid?.SpawnTileCount ?? 0} water tiles "
            + $"(target {this.TargetCount}) in {Elapsed(this.fillStarted):0} ms, "
            + $"{this.attempts} tile picks / {this.rolls} fish rolls; grid {this.gridMs:0.00} ms, setup {this.setupMs:0.00} ms"
        );
    }

    private static Item? TryCreateItem(string qualifiedItemId)
    {
        try
        {
            return ItemRegistry.Create(qualifiedItemId, 1, 0);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"item:{qualifiedItemId}", $"Could not create '{qualifiedItemId}' from LocationData.json.", ex);
            return null;
        }
    }
}
