using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace Waterlife;

/// <summary>
/// An item floating in the water: junk, algae, a statue or furniture.
/// </summary>
internal sealed class WaterDebris
{
    private const int ShorePadding = 15;
    private const float FadeMilliseconds = 2000f;

    private readonly WaterGrid grid;
    private readonly Texture2D texture;
    private readonly Rectangle source;
    private readonly Vector2 origin;
    private readonly float scale;
    private readonly bool isFurniture;
    private readonly bool persistent;

    /// <summary>Position in vanilla fish tank space: Y grows upwards, the world position is (X, -Y).</summary>
    private Vector2 position;

    private Vector2 velocity;
    private float rotation;
    private float angularVelocity;
    private float bobPhase;
    private float lifetimeMs;
    private float alpha = 1f;

    public WaterDebris(
        ParsedItemData data,
        WaterGrid grid,
        Random rng,
        int tileX,
        int tileY,
        bool persistent,
        bool asFurniture
    )
    {
        this.grid = grid;
        this.texture = data.GetTexture();
        this.source = data.GetSourceRect();
        this.persistent = persistent;
        this.isFurniture = asFurniture;

        this.position = new Vector2(tileX * WaterGrid.TileSize + 32f, -tileY * WaterGrid.TileSize - 32f);
        this.position.X += rng.Next(-28, 16);
        this.position.Y += rng.Next(-16, 16);

        this.origin = new Vector2(Math.Max(1, this.source.Width) / 2f, Math.Max(1, this.source.Height) / 2f);
        this.angularVelocity = (float)((rng.NextDouble() - 0.5) * 0.005);
        this.scale = asFurniture ? 2f : (float)(rng.NextDouble() + 2.0);
        this.bobPhase = rng.NextFloat(0f, 9000f);

        Vector2 direction = new((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
        this.velocity = direction == Vector2.Zero
            ? Vector2.Zero
            : Vector2.Normalize(direction) * (float)(rng.NextDouble() * 0.02);

        this.lifetimeMs = persistent || asFurniture ? float.PositiveInfinity : rng.Next(120000, 300000);

        switch (data.SpriteIndex)
        {
            case 172:
                // Soggy newspaper: always at a random angle, but does not spin.
                this.angularVelocity = 0f;
                this.rotation = rng.NextFloat(0f, (float)(Math.PI * 2));
                break;

            case 152:
            case 153:
            case 157:
                // Algae and seaweed: upright unless the player asked for rotation.
                if (!ModEntry.Instance.Config.RotateAlgae)
                {
                    this.angularVelocity = 0f;
                    this.rotation = 0f;
                }

                break;
        }

        this.PushIntoWater(32f);
    }

    public bool IsExpired => this.alpha <= 0f;

    public Vector2 WorldPosition => new(this.position.X, -this.position.Y);

    public void Update(GameTime time, Rectangle viewport)
    {
        float elapsedMs = (float)time.ElapsedGameTime.TotalMilliseconds;
        this.bobPhase += elapsedMs;

        if (!this.persistent)
        {
            this.lifetimeMs -= elapsedMs;
            if (this.lifetimeMs <= 0f)
            {
                this.alpha = 0f;
                return;
            }

            if (this.lifetimeMs < FadeMilliseconds)
                this.alpha = this.lifetimeMs / FadeMilliseconds;
        }

        if (!viewport.Contains((int)this.position.X, (int)(-this.position.Y)))
            return;

        this.rotation += this.angularVelocity;

        float horizontal = this.velocity.X;
        this.position.X += horizontal;

        Rectangle bounds = this.GetBounds();
        if (!this.grid.IsWater((bounds.Left - ShorePadding) / WaterGrid.TileSize, bounds.Center.Y / WaterGrid.TileSize))
        {
            this.position.X -= horizontal;
            this.position.X += 4f;
            this.velocity.X *= -0.8f;
        }
        else if (!this.grid.IsWater((bounds.Right + ShorePadding) / WaterGrid.TileSize, bounds.Center.Y / WaterGrid.TileSize))
        {
            this.position.X -= horizontal;
            this.velocity.X *= -0.8f;
            this.position.X -= 4f;
        }

        this.position.Y += this.velocity.Y;
        bounds = this.GetBounds();
        if (!this.grid.IsWater(bounds.Center.X / WaterGrid.TileSize, (bounds.Top - ShorePadding) / WaterGrid.TileSize)
            || !this.grid.IsWater(bounds.Center.X / WaterGrid.TileSize, bounds.Bottom / WaterGrid.TileSize))
        {
            this.position.Y -= this.velocity.Y;
            this.velocity.Y *= -0.8f;
        }
    }

    public void Draw(SpriteBatch b, Rectangle viewport, bool drawShadow)
    {
        if (!viewport.Contains((int)this.position.X, (int)(-this.position.Y)))
            return;

        Vector2 world = this.WorldPosition;
        float drift = (float)(5.0 * Math.Sin(this.bobPhase * 0.001));
        Vector2 drawPosition = Game1.GlobalToLocal(world + new Vector2(drift, (float)(5.0 * Math.Sin(this.bobPhase * 0.002))));

        if (this.isFurniture)
        {
            b.Draw(this.texture, drawPosition, this.source, Color.White, this.rotation, this.origin, this.scale, SpriteEffects.None, 0.8f);
        }
        else
        {
            b.Draw(this.texture, drawPosition, this.source, Color.White * this.alpha, this.rotation, this.origin, this.scale, SpriteEffects.None, 1f);
        }

        if (drawShadow)
        {
            b.Draw(
                Game1.shadowTexture,
                Game1.GlobalToLocal(world + new Vector2(drift, 32f)),
                null,
                Color.White * this.alpha * 0.75f,
                0f,
                new Vector2(Game1.shadowTexture.Width / 2, Game1.shadowTexture.Height / 2),
                new Vector2(this.scale, 1f),
                SpriteEffects.None,
                0.5f
            );
        }
    }

    private Rectangle GetBounds()
    {
        float width = Math.Max(8f, this.source.Width * this.scale);
        float height = Math.Max(8f, this.source.Height * this.scale);
        return new Rectangle(
            (int)(this.position.X - width / 2f),
            (int)(-this.position.Y - height / 2f),
            (int)width,
            (int)height
        );
    }

    /// <summary>Move the debris away from shore until its sprite is over water.</summary>
    private void PushIntoWater(float push)
    {
        Rectangle bounds = this.GetBounds();
        if (!this.grid.IsWater(bounds.Left / WaterGrid.TileSize - 1, bounds.Center.Y / WaterGrid.TileSize))
        {
            this.position.X += push;
        }
        else if (!this.grid.IsWater(bounds.Right / WaterGrid.TileSize + 1, bounds.Center.Y / WaterGrid.TileSize))
        {
            this.position.X -= push;
        }

        bounds = this.GetBounds();
        if (!this.grid.IsWater(bounds.Center.X / WaterGrid.TileSize, bounds.Top / WaterGrid.TileSize))
        {
            this.position.Y -= push;
        }
        else if (!this.grid.IsWater(bounds.Center.X / WaterGrid.TileSize, bounds.Bottom / WaterGrid.TileSize))
        {
            this.position.Y += push;
        }
    }
}
