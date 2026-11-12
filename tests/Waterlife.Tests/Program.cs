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

Console.WriteLine();
Console.WriteLine("Statue map");

XDocument statue = XDocument.Load(FindRepoFile("Waterlife/assets/statue.tmx"));
Check("root element", statue.Root?.Name.LocalName, "map");
Check("tileset image source", statue.Descendants("image").First().Attribute("source")?.Value, ".VFStatue.png");
Check("map properties stripped", statue.Descendants("property").Count(p => p.Parent?.Name.LocalName == "properties" && p.Parent.Parent?.Name.LocalName == "map"), 0);

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURES");
return failures == 0 ? 0 : 1;

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
