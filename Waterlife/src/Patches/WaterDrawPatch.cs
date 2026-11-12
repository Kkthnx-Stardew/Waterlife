using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Waterlife;

/// <summary>
/// Draws the water entities from inside <see cref="GameLocation.drawWater"/> so they line up with
/// the water layer. Most locations draw before the water (behind it); locations flagged
/// <c>Underwater</c> in LocationData.json draw after it (in front).
/// </summary>
[HarmonyPatch(typeof(GameLocation), nameof(GameLocation.drawWater))]
internal static class WaterDrawPatch
{
    private static void Prefix(GameLocation __instance, SpriteBatch b)
    {
        Draw(__instance, b, inFront: false);
    }

    private static void Postfix(GameLocation __instance, SpriteBatch b)
    {
        Draw(__instance, b, inFront: true);
    }

    private static void Draw(GameLocation location, SpriteBatch b, bool inFront)
    {
        try
        {
            if (location != Game1.currentLocation)
                return;

            FishManager? manager = ModEntry.Managers.Value;
            if (manager is null || manager.DrawInFront != inFront || !manager.HasContent)
                return;

            manager.Draw(b, FishManager.ExpandedViewport(Game1.viewport));
        }
        catch (Exception ex)
        {
            Log.ErrorOnce("draw", "Drawing fish in the water failed.", ex);
        }
    }
}
