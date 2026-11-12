using System;
using System.Collections.Generic;

namespace Waterlife;

internal enum TrashMode
{
    Default,
    Always,
    Never,
}

/// <summary>Per-location configuration from <c>assets/LocationData.json</c>.</summary>
#pragma warning disable CS0649 // Fields are assigned by the content serializer.
internal sealed class LocationFishData
{
    /// <summary>Raw JSON value, one of <see cref="TrashMode"/>. Unknown values fall back to Default.</summary>
    public string SpawnTrash = "Default";

    /// <summary>Draw this location's fish in front of the water instead of behind it.</summary>
    public bool Underwater;

    /// <summary>Fish per water tile for this location, or null to use the global config value.</summary>
    public float? FishDensity;

    /// <summary>Maximum fish and items in this location, or null to use the global config value.</summary>
    public int? MaximumFish;

    public Dictionary<string, LocationSpawn> Spawns = new();

    public TrashMode TrashMode
    {
        get
        {
            if (Enum.TryParse(this.SpawnTrash, ignoreCase: true, out TrashMode mode))
                return mode;
            return TrashMode.Default;
        }
    }
}

/// <summary>A single item that is always present at a fixed tile, e.g. a legendary fish or a statue.</summary>
public sealed class LocationSpawn
{
    public int X;

    public int Y;

    public string QualifiedItemId = "";

    /// <summary>Game state query that must pass for the item to appear. Empty means always.</summary>
    public string? Condition;

    /// <summary>Allow the normal fish spawns of this item to appear as well.</summary>
    public bool AllowMore;
}
#pragma warning restore CS0649
