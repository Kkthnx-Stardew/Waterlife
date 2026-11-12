using System;

namespace Waterlife;

internal static class RandomExtensions
{
    /// <summary>Get a random float in the given range. Works with a flipped range, like vanilla's Utility.RandomFloat.</summary>
    internal static float NextFloat(this Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    internal static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }
}
