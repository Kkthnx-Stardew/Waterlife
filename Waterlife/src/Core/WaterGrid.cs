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

    /// <summary>Deepest water depth the game reports. Anything further from shore is the same value.</summary>
    public const int MaxDepth = 5;

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

    /// <summary>Build the grid for a location's water data, or null if there is none.</summary>
    public static WaterGrid? TryBuild(WaterTiles? tiles)
    {
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

    /// <summary>
    /// Get the tile that contains a pixel coordinate. Plain integer division rounds toward zero,
    /// which would read the 63 pixels just past the left and top map edge as tile 0 and let
    /// entities drift off the map.
    /// </summary>
    public static int TileOf(int pixel)
    {
        int tile = pixel / TileSize;
        return pixel < 0 && pixel % TileSize != 0 ? tile - 1 : tile;
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
    /// Fishing depth of a tile, matching the value a real cast would pass to the fish picker:
    /// the Chebyshev distance to the nearest land tile, capped at 5, minus one. Off-map tiles are
    /// not land, so water at a map edge reads as deep and tiles outside the map read as the maximum.
    /// </summary>
    public int GetDepth(int tileX, int tileY)
    {
        int x = tileX + 1;
        int y = tileY + 1;
        if (x < 0 || y < 0 || x >= this.stride || y >= this.MapHeight + 2)
            return MaxDepth;

        int distance = this.depth[x + y * this.stride];
        if (distance < 0)
            return MaxDepth;

        return Math.Max(0, Math.Min(distance, MaxDepth + 1) - 1);
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
    /// Flood fill the distance from every tile to the nearest land tile with the same definition
    /// the game uses for fishing depth: Chebyshev distance, only tiles on the map count as land,
    /// and anything further than 5 tiles away is indistinguishable from open water.
    /// </summary>
    private void FillDepths(WaterTiles tiles)
    {
        int paddedWidth = this.stride;
        int paddedHeight = this.MapHeight + 2;
        Queue<int> queue = new();

        for (int i = 0; i < this.depth.Length; i++)
            this.depth[i] = -1;

        for (int y = 0; y < this.MapHeight; y++)
        {
            for (int x = 0; x < this.MapWidth; x++)
            {
                if (tiles[x, y])
                    continue;

                int index = (x + 1) + (y + 1) * paddedWidth;
                this.depth[index] = 0;
                queue.Enqueue(index);
            }
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % paddedWidth;
            int y = index / paddedWidth;
            int next = this.depth[index] + 1;

            // Distances past 6 are all reported as the maximum, so stop expanding there.
            if (next > MaxDepth + 1)
                continue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int neighbourX = x + dx;
                    int neighbourY = y + dy;
                    if (neighbourX < 0 || neighbourY < 0 || neighbourX >= paddedWidth || neighbourY >= paddedHeight)
                        continue;

                    int neighbour = neighbourX + neighbourY * paddedWidth;
                    if (this.depth[neighbour] >= 0)
                        continue;

                    this.depth[neighbour] = next;
                    queue.Enqueue(neighbour);
                }
            }
        }
    }
}
