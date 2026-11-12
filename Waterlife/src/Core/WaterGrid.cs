using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Waterlife;

/// <summary>
/// The water tiles of one location, prepared once per location entry.
/// The grid is padded by one tile on every side so entities may test positions
/// slightly outside the map without special cases.
/// </summary>
internal sealed class WaterGrid
{
    public const int TileSize = 64;

    private readonly bool[] water;
    private readonly int[] depth;
    private readonly int stride;
    private readonly List<Point> spawnTiles = new();

    public int MapWidth { get; }

    public int MapHeight { get; }

    public int SpawnTileCount => this.spawnTiles.Count;

    private WaterGrid(int mapWidth, int mapHeight)
    {
        this.MapWidth = mapWidth;
        this.MapHeight = mapHeight;
        this.stride = mapWidth + 2;

        int cells = this.stride * (mapHeight + 2);
        this.water = new bool[cells];
        this.depth = new int[cells];
    }

    /// <summary>Build the grid for a location, or null if it has no water data.</summary>
    public static WaterGrid? TryBuild(GameLocation location)
    {
        WaterTiles? tiles = location.waterTiles;
        if (tiles?.waterTiles is null)
            return null;

        int width = tiles.waterTiles.GetLength(0);
        int height = tiles.waterTiles.GetLength(1);
        if (width <= 0 || height <= 0)
            return null;

        WaterGrid grid = new(width, height);
        grid.Fill(tiles);
        return grid;
    }

    /// <summary>Get whether a map tile is fishable water. Out-of-bounds tiles are never water.</summary>
    public bool IsWater(int tileX, int tileY)
    {
        int x = tileX + 1;
        int y = tileY + 1;
        if (x < 0 || y < 0 || x >= this.stride || y >= this.MapHeight + 2)
            return false;

        return this.water[x + y * this.stride];
    }

    /// <summary>
    /// Fishing depth of a tile: the number of tiles to the nearest shore, which is what
    /// <c>Data/Fish</c> means by water depth. Never returns less than 1.
    /// </summary>
    public int GetDepth(int tileX, int tileY)
    {
        int x = tileX + 1;
        int y = tileY + 1;
        if (x < 0 || y < 0 || x >= this.stride || y >= this.MapHeight + 2)
            return 1;

        return Math.Max(1, this.depth[x + y * this.stride]);
    }

    public Point RandomSpawnTile(Random rng)
    {
        return this.spawnTiles[rng.Next(this.spawnTiles.Count)];
    }

    /// <summary>
    /// Mark every water tile that is fully surrounded by water and not part of a
    /// one or two tile wide channel, so fish never spawn where they cannot move.
    /// Tiles outside the map count as water, which keeps shore-adjacent fish possible.
    /// </summary>
    private void Fill(WaterTiles tiles)
    {
        int width = this.MapWidth;
        int height = this.MapHeight;

        bool Source(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return true;

            return tiles[x, y];
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!tiles[x, y])
                    continue;

                if (!Source(x + 1, y) || !Source(x - 1, y) || !Source(x, y + 1) || !Source(x, y - 1))
                    continue;

                if (!Source(x - 2, y) && !Source(x + 2, y))
                    continue;

                this.water[(x + 1) + (y + 1) * this.stride] = true;
                this.spawnTiles.Add(new Point(x, y));
            }
        }

        this.FillDepths(tiles);
    }

    /// <summary>
    /// Flood fill the distance to the nearest shore for every water tile. The padding ring and
    /// every non-water tile are depth 0, so map edges behave like shore.
    /// </summary>
    private void FillDepths(WaterTiles tiles)
    {
        int paddedWidth = this.stride;
        int paddedHeight = this.MapHeight + 2;
        Queue<int> queue = new();

        for (int i = 0; i < this.depth.Length; i++)
            this.depth[i] = -1;

        for (int y = 0; y < paddedHeight; y++)
        {
            for (int x = 0; x < paddedWidth; x++)
            {
                int mapX = x - 1;
                int mapY = y - 1;
                bool isWater = mapX >= 0 && mapY >= 0 && mapX < this.MapWidth && mapY < this.MapHeight && tiles[mapX, mapY];
                if (isWater)
                    continue;

                this.depth[x + y * paddedWidth] = 0;
                queue.Enqueue(x + y * paddedWidth);
            }
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % paddedWidth;
            int y = index / paddedWidth;
            int next = this.depth[index] + 1;

            if (x > 0)
                Relax(index - 1, next);
            if (x + 1 < paddedWidth)
                Relax(index + 1, next);
            if (y > 0)
                Relax(index - paddedWidth, next);
            if (y + 1 < paddedHeight)
                Relax(index + paddedWidth, next);
        }

        void Relax(int index, int value)
        {
            if (this.depth[index] >= 0)
                return;

            this.depth[index] = value;
            queue.Enqueue(index);
        }
    }
}
