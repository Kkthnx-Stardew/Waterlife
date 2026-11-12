using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Waterlife;

/// <summary>How a fish moves and is drawn. Mirrors <c>Data/AquariumFish</c>'s type field.</summary>
internal enum FishSwimType
{
    Normal,
    Eel,
    Cephalopod,
    Float,
    Ground,
    Crawl,
    Static,
}

/// <summary>Everything needed to draw one fish species, loaded once and shared by all its fish.</summary>
internal sealed class FishVisual
{
    public FishVisual(
        Texture2D texture,
        int spriteIndex,
        FishSwimType swimType,
        int[]? idleFrames,
        int[]? dartStartFrames,
        int[]? dartHoldFrames,
        int[]? dartEndFrames,
        float minSpeed,
        float maxSpeed
    )
    {
        this.Texture = texture;
        this.SpriteIndex = spriteIndex;
        this.SwimType = swimType;
        this.IdleFrames = idleFrames;
        this.DartStartFrames = dartStartFrames;
        this.DartHoldFrames = dartHoldFrames;
        this.DartEndFrames = dartEndFrames;
        this.MinSpeed = minSpeed;
        this.MaxSpeed = maxSpeed;
        this.FramesPerRow = Math.Max(1, texture.Width / 24);
    }

    public Texture2D Texture { get; }

    public int SpriteIndex { get; }

    public FishSwimType SwimType { get; }

    public int[]? IdleFrames { get; }

    public int[]? DartStartFrames { get; }

    public int[]? DartHoldFrames { get; }

    public int[]? DartEndFrames { get; }

    public float MinSpeed { get; }

    public float MaxSpeed { get; }

    /// <summary>How many 24px wide frames fit in one row of the sprite sheet.</summary>
    public int FramesPerRow { get; }
}

/// <summary>
/// Loads and caches <see cref="FishVisual"/>s from <c>Data/AquariumFish</c> and <c>Data/Fish</c>.
/// The vanilla fish tank parses this data for every single fish it creates. We parse it once per species.
/// </summary>
internal sealed class FishVisualCache
{
    private const string AquariumDataAsset = "Data/AquariumFish";
    private const string FishDataAsset = "Data/Fish";
    private const string AquariumTextureAsset = "LooseSprites/AquariumFish";

    private readonly IModHelper helper;
    private readonly Dictionary<string, FishVisual?> cache = new();
    private readonly HashSet<string> textureAssets = new();
    private Dictionary<string, string>? aquariumData;
    private Dictionary<string, string>? fishData;
    private Texture2D? aquariumTexture;

    public FishVisualCache(IModHelper helper)
    {
        this.helper = helper;
        helper.Events.Content.AssetsInvalidated += this.OnAssetsInvalidated;
    }

    /// <summary>Get the visual data for a fish, or null if it has no aquarium sprite.</summary>
    public FishVisual? Get(string itemId)
    {
        if (this.cache.TryGetValue(itemId, out FishVisual? cached))
            return cached;

        FishVisual? visual = this.Build(itemId);
        this.cache[itemId] = visual;
        return visual;
    }

    private FishVisual? Build(string itemId)
    {
        try
        {
            return this.BuildVisual(itemId);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"visual:{itemId}", $"Could not build the sprite for fish '{itemId}'.", ex);
            return null;
        }
    }

    private FishVisual? BuildVisual(string itemId)
    {
        if (!this.GetAquariumData().TryGetValue(itemId, out string? raw))
            return null;

        string[] fields = raw.Split('/');
        int spriteIndex = int.TryParse(ArgUtility.Get(fields, 0), out int index) ? index : 0;
        FishSwimType swimType = ParseSwimType(ArgUtility.Get(fields, 1));

        Texture2D texture = this.LoadTexture(ArgUtility.Get(fields, 6, null, allowBlank: false)) ?? this.GetAquariumTexture();

        (float minSpeed, float maxSpeed) = this.GetSpeedRange(itemId, swimType);

        return new FishVisual(
            texture,
            spriteIndex,
            swimType,
            ParseFrames(ArgUtility.Get(fields, 2, null, allowBlank: false)),
            ParseFrames(ArgUtility.Get(fields, 3, null, allowBlank: false)),
            ParseFrames(ArgUtility.Get(fields, 4, null, allowBlank: false)),
            ParseFrames(ArgUtility.Get(fields, 5, null, allowBlank: false)),
            minSpeed,
            maxSpeed
        );
    }

    /// <summary>
    /// Replicates the swim speed vanilla picks for a tank fish. The layout mirrors
    /// <c>TankFish</c>: a random speed from <c>Data/Fish</c>'s motion type, then overridden per
    /// aquarium type. <c>Static</c> and <c>Float</c> deliberately keep the data value.
    /// </summary>
    private (float Min, float Max) GetSpeedRange(string itemId, FishSwimType swimType)
    {
        // Vanilla's field starts at zero, so an item with aquarium data but no Data/Fish entry
        // (modded fish, the extra legendary) does not drift on its own.
        (float min, float max) = (0f, 0f);

        if (this.GetFishData().TryGetValue(itemId, out string? raw))
        {
            string[] fields = raw.Split('/');
            if (ArgUtility.Get(fields, 1) != "trap")
            {
                (min, max) = (0.25f, 0.35f);
                switch (ArgUtility.Get(fields, 2))
                {
                    case "smooth":
                        (min, max) = (0.5f, 0.6f);
                        break;
                    case "dart":
                        (min, max) = (0f, 0f);
                        break;
                }
            }
        }

        return swimType switch
        {
            // Vanilla clamps fishScale (0.75) into 0.3..0.4 for eels, which is always 0.4.
            FishSwimType.Eel => (0.4f, 0.4f),
            FishSwimType.Cephalopod or FishSwimType.Ground or FishSwimType.Crawl => (0f, 0f),
            _ => (min, max),
        };
    }

    private static FishSwimType ParseSwimType(string? value)
    {
        return value switch
        {
            "eel" => FishSwimType.Eel,
            "cephalopod" => FishSwimType.Cephalopod,
            "ground" => FishSwimType.Ground,
            "static" => FishSwimType.Static,
            "crawl" or "front_crawl" => FishSwimType.Crawl,
            "float" => FishSwimType.Float,
            _ => FishSwimType.Normal,
        };
    }

    private static int[]? ParseFrames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string[] parts = ArgUtility.SplitBySpace(value);
        int[] frames = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out frames[i]))
            {
                Log.Warn($"Ignored invalid fish animation frame '{parts[i]}' in {AquariumDataAsset}.");
                return null;
            }
        }

        return frames;
    }

    private Texture2D? LoadTexture(string? assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        this.textureAssets.Add(assetName);

        try
        {
            return this.helper.GameContent.Load<Texture2D>(assetName);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"texture:{assetName}", $"Could not load fish texture '{assetName}'; falling back to the aquarium sprite sheet.", ex);
            return null;
        }
    }

    private Texture2D GetAquariumTexture()
    {
        if (this.aquariumTexture is not null)
            return this.aquariumTexture;

        this.textureAssets.Add(AquariumTextureAsset);
        return this.aquariumTexture = this.helper.GameContent.Load<Texture2D>(AquariumTextureAsset);
    }

    private Dictionary<string, string> GetAquariumData()
    {
        return this.aquariumData ??= this.LoadData(AquariumDataAsset);
    }

    private Dictionary<string, string> GetFishData()
    {
        return this.fishData ??= this.LoadData(FishDataAsset);
    }

    private Dictionary<string, string> LoadData(string assetName)
    {
        try
        {
            return this.helper.GameContent.Load<Dictionary<string, string>>(assetName);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"data:{assetName}", $"Could not load '{assetName}'.", ex);
            return new Dictionary<string, string>();
        }
    }

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (!this.IsAffected(e))
            return;

        this.cache.Clear();
        this.textureAssets.Clear();
        this.aquariumData = null;
        this.fishData = null;
        this.aquariumTexture = null;
    }

    /// <summary>Get whether any invalidated asset is data or a sprite that the cached visuals came from.</summary>
    private bool IsAffected(AssetsInvalidatedEventArgs e)
    {
        foreach (IAssetName name in e.Names)
        {
            if (name.IsEquivalentTo(AquariumDataAsset) || name.IsEquivalentTo(FishDataAsset))
                return true;

            foreach (string assetName in this.textureAssets)
            {
                if (name.IsEquivalentTo(assetName))
                    return true;
            }
        }

        return false;
    }
}
