using System.Text.Json;
using System.Xml.Linq;
using StardewValley;
using Waterlife;

int failures = 0;

void Check<T>(string name, T actual, T expected)
{
    bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok)
        failures++;

    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}: expected {Format(expected)}, got {Format(actual)}");
}

static string Format<T>(T value) => value is null ? "null" : value.ToString() ?? "null";

static bool[,] Map(int width, int height, Func<int, int, bool> isWater)
{
    bool[,] map = new bool[width, height];
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
            map[x, y] = isWater(x, y);
    }

    return map;
}

static WaterGrid Grid(bool[,] map) => WaterGrid.TryBuild(new WaterTiles(map))!;

Console.WriteLine("Water grid");

// A 7x7 map with one tile of land around a 5x5 pond.
WaterGrid pond = Grid(Map(7, 7, (x, y) => x > 0 && y > 0 && x < 6 && y < 6));
Check("pond spawn tiles", pond.SpawnTileCount, 9);
Check("pond center depth", pond.GetDepth(3, 3), 2);
Check("pond inner corner depth", pond.GetDepth(2, 2), 1);
Check("pond shore depth", pond.GetDepth(1, 1), 0);
Check("pond land depth", pond.GetDepth(0, 0), 0);

// A large lake: depth caps at 5 and off-map tiles are not land.
WaterGrid lake = Grid(Map(22, 22, (x, y) => x > 0 && y > 0 && x < 21 && y < 21));
Check("lake center depth", lake.GetDepth(11, 11), 5);
Check("lake 5 tiles from land", lake.GetDepth(5, 11), 4);
Check("lake 6 tiles from land", lake.GetDepth(6, 11), 5);
Check("lake shore depth", lake.GetDepth(1, 1), 0);
Check("off-map depth", lake.GetDepth(-5, 99), 5);

// No land anywhere: the game reports maximum depth everywhere.
WaterGrid open = Grid(Map(10, 10, (x, y) => true));
Check("open water depth", open.GetDepth(5, 5), 5);
Check("open water spawn tiles", open.SpawnTileCount, 100);

// Channels too narrow for a fish to move in are not spawn tiles.
Check("1-wide channel spawn tiles", Grid(Map(9, 5, (x, y) => y == 2 && x >= 1 && x <= 7)).SpawnTileCount, 0);
Check("3-wide channel spawn tiles", Grid(Map(9, 7, (x, y) => y >= 2 && y <= 4 && x >= 1 && x <= 7)).SpawnTileCount, 5);

// Pixels left of or above the map must land in tile -1, not tile 0, or entities slip off the edge.
Check("pixel 0 is tile 0", WaterGrid.TileOf(0), 0);
Check("pixel 63 is tile 0", WaterGrid.TileOf(63), 0);
Check("pixel 64 is tile 1", WaterGrid.TileOf(64), 1);
Check("pixel -1 is tile -1", WaterGrid.TileOf(-1), -1);
Check("pixel -64 is tile -1", WaterGrid.TileOf(-64), -1);
Check("pixel -65 is tile -2", WaterGrid.TileOf(-65), -2);
Check("pixel off the map edge is not water", pond.IsWater(WaterGrid.TileOf(-30), 3), false);

Console.WriteLine();
Console.WriteLine("Location data");

JsonSerializerOptions options = new()
{
    IncludeFields = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
};

LocationFishData overrides = JsonSerializer.Deserialize<LocationFishData>(
    "{\"FishDensity\":0.08,\"MaximumFish\":240}",
    options
)!;
Check("density override", overrides.FishDensity, 0.08f);
Check("cap override", overrides.MaximumFish, 240);

LocationFishData defaults = JsonSerializer.Deserialize<LocationFishData>("{}", options)!;
Check("density absent", defaults.FishDensity, null);
Check("cap absent", defaults.MaximumFish, null);

string dataPath = FindRepoFile("Waterlife/assets/LocationData.json");
Dictionary<string, LocationFishData> locations =
    JsonSerializer.Deserialize<Dictionary<string, LocationFishData>>(File.ReadAllText(dataPath), options)!;
Check("location count", locations.Count, 9);
Check("shipped Forest density unset", locations["Forest"].FishDensity, null);
Check("shipped Forest cap unset", locations["Forest"].MaximumFish, null);
Check("Forest fixed spawns", locations["Forest"].Spawns.Count, 3);

// The game only offers the Angler to a player standing in rows 0 to 14 of the Town map (its own
// PlayerPosition rule), so the fish has to be shown up there. It used to sit at the bottom left.
foreach (string anglerKey in new[] { "Angler", "Ms_Angler" })
{
    Check($"{anglerKey} is in the north river", locations["Town"].Spawns[anglerKey].Y is >= 0 and <= 14, true);
    Check($"{anglerKey} is not in the old south west spot", locations["Town"].Spawns[anglerKey].X != 10, true);
}

// These two items are only catchable once the game says so, so the fixed copy needs the same gate.
Check("Fossilized spine condition", locations["IslandNorth"].Spawns["Fossilized_Spine"].Condition, "IS_ISLAND_NORTH_BRIDGE_FIXED");
Check("Snake skull condition", locations["IslandWest"].Spawns["Snake_Skull"].Condition, "PLAYER_HAS_MAIL Current islandNorthCaveOpened");

Check("Woods basket waits for SVE", locations["Woods"].Spawns["Wall_Basket"].Condition, "Kkthnx.Waterlife_MOD_LOADED FlashShifter.StardewValleyExpandedCP");
Check("Radioactive Carp is listed", locations["Sewer"].Spawns.ContainsKey("Radioactive_Carp"), true);

int spawnsWithoutItem = locations.Values.SelectMany(l => l.Spawns.Values).Count(s => string.IsNullOrWhiteSpace(s.QualifiedItemId));
Check("every fixed spawn names an item", spawnsWithoutItem, 0);

Console.WriteLine();
Console.WriteLine("Bundled files");

// Nothing from the old mod's file set may come back.
foreach (string legacy in new[] { "Waterlife/assets/.VFStatue.png", "Waterlife/assets/misc.txt" })
{
    bool present = false;
    try
    {
        FindRepoFile(legacy);
        present = true;
    }
    catch (FileNotFoundException)
    {
    }

    Check($"{legacy} is gone", present, false);
}

// The statue map and its sheet have to agree, or the tiles would be cut in the wrong places.
XDocument statue = XDocument.Load(FindRepoFile("Waterlife/assets/statue.tmx"));
Check("statue tileset image", statue.Descendants("image").First().Attribute("source")?.Value, "WaterlifeStatue.png");
Check("statue sheet size", PngSize(FindRepoFile("Waterlife/assets/WaterlifeStatue.png")), (32, 48));
Check("statue map size in tiles", (statue.Root?.Attribute("width")?.Value, statue.Root?.Attribute("height")?.Value), ("2", "3"));
Check("statue tile actions", statue.Descendants("property").Count(p => p.Attribute("value")?.Value == "Kkthnx.Waterlife_SpawnBasket"), 2);

// The golden fish is one frame, 32 pixels wide, which is what the renderer is told to expect.
Check("golden fish sheet size", PngSize(FindRepoFile("Waterlife/assets/GoldenFish.png")), (32, 24));

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURES");
return failures == 0 ? 0 : 1;

// Read the width and height from a PNG header (bytes 16 to 23), so no imaging library is needed.
static (int, int) PngSize(string path)
{
    byte[] header = File.ReadAllBytes(path);
    int Read(int offset) => (header[offset] << 24) | (header[offset + 1] << 16) | (header[offset + 2] << 8) | header[offset + 3];
    return (Read(16), Read(20));
}

static string FindRepoFile(string relativePath)
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        string candidate = Path.Combine(directory.FullName, relativePath);
        if (File.Exists(candidate))
            return candidate;

        directory = directory.Parent;
    }

    throw new FileNotFoundException($"Could not find '{relativePath}' above '{AppContext.BaseDirectory}'.");
}
