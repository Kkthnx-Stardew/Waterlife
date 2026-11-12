using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Waterlife;

/// <summary>
/// One fish swimming in the water. Holds only its own state. Sprites and animations
/// are shared through <see cref="FishVisual"/>.
/// </summary>
internal sealed class AquariumFish
{
    private enum FadeState
    {
        Appearing,
        Swimming,
        Disappearing,
    }

    private const float MaxAlpha = 0.9f;
    private const float FrameDuration = 0.125f;

    /// <summary>Vertical limit used by vanilla for cephalopods in "marine" locations.</summary>
    private const float MarineCeiling = -694f;

    private readonly FishVisual visual;
    private readonly WaterGrid grid;
    private readonly Random rng;
    private readonly float minimumVelocity;
    private readonly float scale;
    private readonly bool persistent;
    private readonly bool marineLocation;

    /// <summary>
    /// Position in vanilla fish tank space: Y grows upwards, the world position is (X, -Y).
    /// </summary>
    private Vector2 position;

    private Vector2 velocity;
    private bool facingLeft;
    private float alpha;
    private FadeState state;
    private float lifetimeMs;

    private int[]? animation;
    private int frame;
    private int animationFrame;
    private float frameTime;
    private int dartsLeft;
    private float nextDart;

    public AquariumFish(FishVisual visual, WaterGrid grid, Random rng, int tileX, int tileY, bool persistent, bool marineLocation)
    {
        this.visual = visual;
        this.grid = grid;
        this.rng = rng;
        this.persistent = persistent;
        this.marineLocation = marineLocation;

        this.minimumVelocity = rng.NextFloat(visual.MinSpeed, visual.MaxSpeed);
        this.lifetimeMs = persistent ? float.PositiveInfinity : rng.Next(20000, 90000);
        this.alpha = persistent ? MaxAlpha : 0f;
        this.state = persistent ? FadeState.Swimming : FadeState.Appearing;

        this.position = new Vector2(tileX * WaterGrid.TileSize + 32f, -tileY * WaterGrid.TileSize - 32f);
        this.position.X += rng.Next(-28, 16);

        bool openWater = true;
        for (int dx = -1; dx <= 1 && openWater; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!grid.IsWater(tileX + dx, tileY + dy))
                {
                    openWater = false;
                    break;
                }
            }
        }

        if (openWater)
            this.position.Y += rng.Next(-32, 32);

        this.scale = 0.5f + (float)rng.NextDouble() * 0.33f;

        // Nudge the fish away from shore if its sprite would stick out of the water.
        const float push = 32f;
        Rectangle bounds = this.GetBounds();
        if (!grid.IsWater(bounds.Left / WaterGrid.TileSize, bounds.Center.Y / WaterGrid.TileSize + 2))
        {
            this.position.X += push;
        }
        else if (!grid.IsWater(bounds.Right / WaterGrid.TileSize, bounds.Center.Y / WaterGrid.TileSize + 2))
        {
            this.position.X -= push;
        }

        bounds = this.GetBounds();
        if (!grid.IsWater(bounds.Center.X / WaterGrid.TileSize, bounds.Top / WaterGrid.TileSize))
        {
            this.position.Y -= push;
        }
        else if (!grid.IsWater(bounds.Center.X / WaterGrid.TileSize, bounds.Bottom / WaterGrid.TileSize))
        {
            this.position.Y += push;
        }

        this.facingLeft = rng.Next(2) == 1;
        this.velocity = new Vector2(this.facingLeft ? -1f : 1f, 0f) * this.minimumVelocity;
        this.nextDart = rng.NextFloat(0.1f, 10f);
        this.animation = visual.IdleFrames;
        this.frame = visual.IdleFrames is { Length: > 0 } idle ? idle[0] : visual.SpriteIndex;
    }

    public Vector2 WorldPosition => new(this.position.X, -this.position.Y);

    public bool IsExpired => this.alpha < 0f;

    public void Update(GameTime time, Rectangle viewport)
    {
        float deltaSeconds = (float)time.ElapsedGameTime.TotalSeconds;

        if (!this.persistent)
            this.lifetimeMs -= (float)time.ElapsedGameTime.TotalMilliseconds;

        if (this.lifetimeMs < 0f && this.state == FadeState.Swimming)
        {
            this.state = FadeState.Disappearing;
        }
        else if (this.state == FadeState.Appearing && this.alpha > MaxAlpha)
        {
            this.alpha = MaxAlpha;
            this.state = FadeState.Swimming;
        }

        if (this.state == FadeState.Appearing)
            this.alpha += 0.01f;
        else if (this.state == FadeState.Disappearing)
            this.alpha -= 0.01f;

        if (this.alpha < 0f)
            return;

        // Off-screen fish keep their state and are not animated, same as before.
        if (!viewport.Contains((int)this.position.X, (int)(-this.position.Y)))
            return;

        this.AdvanceAnimation(deltaSeconds);

        if (this.visual.SwimType != FishSwimType.Static)
            this.Move(deltaSeconds);

        if (this.visual.SwimType == FishSwimType.Cephalopod)
        {
            this.position.Y -= 0.2f;
            if (this.marineLocation && this.position.Y < MarineCeiling)
                this.position.Y = MarineCeiling;
        }
    }

    public void Draw(SpriteBatch b, Rectangle viewport, float drawLayer, bool drawShadow)
    {
        if (!viewport.Contains((int)this.position.X, (int)(-this.position.Y)))
            return;

        SpriteEffects effects = SpriteEffects.None;
        int offsetX = -12;
        int segmentWidth = this.visual.SwimType == FishSwimType.Eel ? 4 : 8;
        int step = segmentWidth;
        if (this.facingLeft)
        {
            effects = SpriteEffects.FlipHorizontally;
            step = -step;
            offsetX = -offsetX - segmentWidth + 1;
        }

        double seconds = Game1.currentGameTime.TotalGameTime.TotalSeconds;
        float bob = (float)Math.Sin(seconds * 1.25 + this.position.X / 32f) * 2f;
        if (this.visual.SwimType is FishSwimType.Ground or FishSwimType.Crawl or FishSwimType.Static)
            bob = 0f;

        float scaleFactor = 4f * this.scale;
        float waveDivisor = 10f;
        float waveSpeed = 1f;
        if (this.visual.SwimType == FishSwimType.Eel)
        {
            waveDivisor = 20f;
            bob = 0f;
        }

        Texture2D texture = this.visual.Texture;
        int frameX = this.frame % this.visual.FramesPerRow * 24;
        int frameY = this.frame / this.visual.FramesPerRow * 48;
        Vector2 world = this.WorldPosition;
        Color color = Color.White * this.alpha;

        switch (this.visual.SwimType)
        {
            case FishSwimType.Ground:
            case FishSwimType.Crawl:
            case FishSwimType.Static:
                b.Draw(
                    texture,
                    Game1.GlobalToLocal(world + new Vector2(0f, bob) * scaleFactor),
                    new Rectangle(frameX, frameY, 24, 24),
                    color,
                    0f,
                    new Vector2(12f, 12f),
                    scaleFactor,
                    effects,
                    drawLayer
                );
                break;

            case FishSwimType.Cephalopod:
            case FishSwimType.Float:
                b.Draw(
                    texture,
                    Game1.GlobalToLocal(world + new Vector2(0f, bob) * scaleFactor),
                    new Rectangle(frameX, frameY, 24, 24),
                    color,
                    Math.Clamp(this.velocity.X, -0.5f, 0.5f),
                    new Vector2(12f, 12f),
                    scaleFactor,
                    effects,
                    drawLayer
                );
                break;

            default:
                int segments = 24 / segmentWidth;
                float speedFactor = Math.Clamp(this.velocity.Length(), 0.2f, 1f);
                for (int i = 0; i < segments; i++)
                {
                    float taper = 1f - i * segmentWidth / waveDivisor;
                    taper = Math.Clamp(taper, 0f, 1f);
                    float phaseSpeed = waveSpeed;
                    float phaseOffset = 0f;

                    if (this.visual.SwimType == FishSwimType.Eel)
                    {
                        taper = 1f;
                        speedFactor = 1f;
                        phaseSpeed = 0.1f;
                        phaseOffset = 4f;
                    }

                    if (this.facingLeft)
                        phaseOffset *= -1f;

                    float wave = (float)Math.Sin(i * 20 + seconds * 25.0 * phaseSpeed + phaseOffset * this.position.X / 16f)
                        * taper
                        * speedFactor;

                    b.Draw(
                        texture,
                        Game1.GlobalToLocal(world + new Vector2(offsetX + i * step, bob + wave) * scaleFactor),
                        new Rectangle(frameX + i * segmentWidth, frameY, segmentWidth, 24),
                        color,
                        0f,
                        new Vector2(0f, 12f),
                        scaleFactor,
                        effects,
                        drawLayer
                    );
                }

                break;
        }

        if (drawShadow)
        {
            b.Draw(
                Game1.shadowTexture,
                Game1.GlobalToLocal(world + new Vector2(0f, 32f)),
                null,
                Color.White * this.alpha * 0.75f,
                0f,
                new Vector2(Game1.shadowTexture.Width / 2, Game1.shadowTexture.Height / 2),
                new Vector2(scaleFactor, 1f),
                SpriteEffects.None,
                0.5f
            );
        }
    }

    private void Move(float deltaSeconds)
    {
        float horizontal = this.velocity.X;
        if (this.visual.SwimType == FishSwimType.Crawl)
            horizontal = Math.Clamp(horizontal, -0.5f, 0.5f);

        this.position.X += horizontal;

        Rectangle bounds = this.GetBounds();
        if (!this.grid.IsWater(bounds.Left / WaterGrid.TileSize, bounds.Center.Y / WaterGrid.TileSize))
        {
            this.position.X -= horizontal;
            this.position.X += 4f;
            this.velocity.X *= -0.8f;
            this.facingLeft = !this.facingLeft;
        }
        else if (!this.grid.IsWater(bounds.Right / WaterGrid.TileSize, bounds.Center.Y / WaterGrid.TileSize))
        {
            this.position.X -= horizontal;
            this.velocity.X *= -0.8f;
            this.facingLeft = !this.facingLeft;
            this.position.X -= 4f;
        }

        this.position.Y += this.velocity.Y;
        bounds = this.GetBounds();
        // Bounce off the shore vertically too. The old code was missing the negations, so it
        // cancelled every vertical step and fish only ever drifted sideways.
        if (!this.grid.IsWater(bounds.Center.X / WaterGrid.TileSize, bounds.Top / WaterGrid.TileSize)
            || !this.grid.IsWater(bounds.Center.X / WaterGrid.TileSize, bounds.Bottom / WaterGrid.TileSize))
        {
            this.position.Y -= this.velocity.Y;
            this.velocity.Y *= -0.8f;
        }

        float speed = this.velocity.Length();
        if (speed > this.minimumVelocity)
        {
            float drag = this.visual.SwimType is FishSwimType.Crawl or FishSwimType.Ground ? 0.03f : 0.015f;
            speed = RandomExtensions.Lerp(speed, this.minimumVelocity, drag);

            // Guard against normalizing a zero vector, which would poison the position with NaN.
            this.velocity = speed > 0.0001f ? Vector2.Normalize(this.velocity) * speed : Vector2.Zero;

            if (this.animation == this.visual.DartHoldFrames && speed <= this.minimumVelocity + 0.5f)
                this.SetAnimation(this.visual.DartEndFrames ?? this.visual.IdleFrames);
        }

        this.nextDart -= deltaSeconds;
        if (this.nextDart <= 0f)
            this.StartDart();
    }

    private void StartDart()
    {
        if (this.dartsLeft == 0)
        {
            this.dartsLeft = this.rng.Next(1, 4);
            this.nextDart = this.rng.NextFloat(6f, 12f);
            if (this.visual.SwimType == FishSwimType.Cephalopod)
                this.nextDart = this.rng.NextFloat(2f, 5f);

            if (this.rng.NextDouble() < 0.3)
                this.facingLeft = !this.facingLeft;
        }
        else
        {
            this.nextDart = this.rng.NextFloat(0.1f, 0.5f);
            this.dartsLeft--;
            if (this.rng.NextDouble() < 0.05)
                this.facingLeft = !this.facingLeft;
        }

        this.SetAnimation(this.visual.DartStartFrames ?? this.visual.DartHoldFrames);

        this.velocity.X = this.facingLeft ? -1.5f : 1.5f;

        switch (this.visual.SwimType)
        {
            case FishSwimType.Cephalopod:
                this.velocity.Y = this.rng.NextFloat(0.5f, 0.75f);
                break;
            case FishSwimType.Ground:
                this.velocity.X *= 0.5f;
                this.velocity.Y = this.rng.NextFloat(0.25f, 0.5f);
                break;
            default:
                this.velocity.Y = this.rng.NextFloat(-0.5f, 0.5f);
                break;
        }

        if (this.visual.SwimType == FishSwimType.Crawl)
            this.velocity.Y = 0f;
    }

    private void AdvanceAnimation(float deltaSeconds)
    {
        if (this.animation is not { Length: > 0 })
            return;

        this.frameTime += deltaSeconds;
        if (this.frameTime <= FrameDuration)
            return;

        this.animationFrame += (int)(this.frameTime / FrameDuration);
        this.frameTime %= FrameDuration;

        if (this.animationFrame < this.animation.Length)
        {
            this.frame = this.animation[this.animationFrame];
            return;
        }

        if (this.animation == this.visual.IdleFrames)
        {
            this.animationFrame %= this.animation.Length;
            this.frame = this.animation[this.animationFrame];
        }
        else if (this.animation == this.visual.DartStartFrames)
        {
            this.SetAnimation(this.visual.DartHoldFrames ?? this.visual.IdleFrames);
        }
        else if (this.animation == this.visual.DartHoldFrames)
        {
            this.animationFrame %= this.animation.Length;
            this.frame = this.animation[this.animationFrame];
        }
        else if (this.animation == this.visual.DartEndFrames)
        {
            this.SetAnimation(this.visual.IdleFrames);
        }
    }

    private void SetAnimation(int[]? frames)
    {
        if (frames is null || frames.Length == 0 || ReferenceEquals(this.animation, frames))
            return;

        this.animation = frames;
        this.animationFrame = 0;
        this.frameTime = 0f;
        this.frame = frames[0];
    }

    private Rectangle GetBounds()
    {
        Vector2 size = new Vector2(24f, 18f) * (4f * this.scale);
        if (this.visual.SwimType is FishSwimType.Ground or FishSwimType.Crawl or FishSwimType.Static)
        {
            return new Rectangle(
                (int)(this.position.X - size.X / 2f),
                (int)(-this.position.Y - size.Y),
                (int)size.X,
                (int)size.Y
            );
        }

        return new Rectangle(
            (int)(this.position.X - size.X / 2f),
            (int)(-this.position.Y - size.Y / 2f),
            (int)size.X,
            (int)size.Y
        );
    }
}
