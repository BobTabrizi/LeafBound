using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LeafBound
{
    /// <summary>
    /// Draws every sprite in the game from code, so the project needs no image assets.
    /// Owns the textures it creates; Dispose releases them.
    /// </summary>
    public sealed class ArtLibrary : IDisposable
    {
        public const int PixelsPerUnit = 16;

        static readonly Color32 Line = Rgb(0x2a1e1c);
        static readonly Color32 Skin = Rgb(0xffd9b3);
        static readonly Color32 SkinShade = Rgb(0xeab48a);
        static readonly Color32 Hair = Rgb(0x8a5a2b);
        static readonly Color32 HairLight = Rgb(0xb57a3c);
        static readonly Color32 White = Rgb(0xffffff);
        static readonly Color32 Blush = Rgb(0xf7a1a1);
        static readonly Color32 Tunic = Rgb(0x4f9d4a);
        static readonly Color32 TunicLight = Rgb(0x72c46a);
        static readonly Color32 Belt = Rgb(0x7a4b22);
        static readonly Color32 Gold = Rgb(0xe0b54a);
        static readonly Color32 Pants = Rgb(0x5a4632);
        static readonly Color32 Shoe = Rgb(0x3b2a20);
        static readonly Color32 Blade = Rgb(0xe3ebf2);
        static readonly Color32 BladeShade = Rgb(0x9aa8b5);
        static readonly Color32 Grip = Rgb(0x6b3f1f);
        static readonly Color32 SlimeGreen = Rgb(0x7ed957);
        static readonly Color32 SlimeDark = Rgb(0x52b03c);
        static readonly Color32 SlimeLight = Rgb(0xd2f7b4);
        static readonly Color32 Leaf = Rgb(0x2f8a32);
        static readonly Color32 Cap = Rgb(0xf08a3c);
        static readonly Color32 CapDark = Rgb(0xc4602a);
        static readonly Color32 Spot = Rgb(0xfff1d6);
        static readonly Color32 Stem = Rgb(0xf3dfb8);
        static readonly Color32 StemShade = Rgb(0xd9bf91);
        static readonly Color32 Grass = Rgb(0x5cbf4a);
        static readonly Color32 GrassLight = Rgb(0x92e26c);
        static readonly Color32 GrassDark = Rgb(0x3f9a3a);
        static readonly Color32 Dirt = Rgb(0x9c6b43);
        static readonly Color32 DirtDark = Rgb(0x845837);
        static readonly Color32 DirtDeep = Rgb(0x5e3b25);
        static readonly Color32 Pebble = Rgb(0xb8916a);
        static readonly Color32 RopeLight = Rgb(0xd2aa6c);
        static readonly Color32 RopeDark = Rgb(0x96703e);
        static readonly Color32 RopeLine = Rgb(0x4a3320);
        static readonly Color32 StoneGray = Rgb(0xa3a9ae);
        static readonly Color32 StoneDark = Rgb(0x6f767c);
        static readonly Color32 Trunk = Rgb(0x7a4f2c);
        static readonly Color32 TrunkShade = Rgb(0x5e3b22);
        static readonly Color32 LeafDark = Rgb(0x3e8e41);
        static readonly Color32 LeafMid = Rgb(0x5fb257);
        static readonly Color32 LeafLight = Rgb(0x86d06b);
        static readonly Color32 LeafLine = Rgb(0x24502a);
        static readonly Color32 CloudShade = Rgb(0xe1ecf6);

        readonly List<Object> owned = new List<Object>();

        public Sprite Head { get; }
        public Sprite HeadBack { get; }
        public Sprite Body { get; }
        public Sprite Arm { get; }
        public Sprite Leg { get; }
        public Sprite Sword { get; }
        public Sprite Tombstone { get; }
        public Sprite Slash { get; }
        public Sprite Particle { get; }
        public Sprite Slime { get; }
        public Sprite Mushroom { get; }
        public Sprite Cloud { get; }
        public Sprite Bush { get; }
        public Sprite Tree { get; }
        public Sprite[] Flowers { get; }

        readonly Sprite[] itemSprites;   // indexed by ItemIcon
        readonly Sprite[] skillIcons;    // indexed by SkillId
        readonly Sprite bronzeCoin, goldCoin, coinPile;

        public ArtLibrary()
        {
            itemSprites = new[]
            {
                BuildPotion(Rgb(0xe8413c), Rgb(0xff8a80), "RedPotion"),
                BuildPotion(Rgb(0x3f7fe8), Rgb(0x8fc0ff), "BluePotion"),
                BuildGel(),
                BuildCapIcon(),
            };
            skillIcons = new[] { BuildPowerStrikeIcon(), BuildSlashBlastIcon(), BuildRageIcon() };
            bronzeCoin = BuildCoin(Rgb(0xc8803c), Rgb(0xe8a868), Rgb(0x8e5524), "BronzeCoin");
            goldCoin = BuildCoin(Rgb(0xf2c94c), Rgb(0xfff09a), Rgb(0xb88a1c), "GoldCoin");
            coinPile = BuildCoinPile();

            Head = BuildHead(back: false);
            HeadBack = BuildHead(back: true);
            Body = BuildBody();
            Arm = BuildArm();
            Leg = BuildLeg();
            Sword = BuildSword();
            Tombstone = BuildTombstone();
            Slash = BuildSlash();
            Particle = BuildParticle();
            Slime = BuildSlime();
            Mushroom = BuildMushroom();
            Cloud = BuildCloud();
            Bush = BuildBush();
            Tree = BuildTree();
            Flowers = new[] { BuildFlower(Rgb(0xff8fb1)), BuildFlower(Rgb(0xfff27a)), BuildFlower(Rgb(0xa9c8ff)) };
        }

        public Sprite MobSprite(MobLook look) => look == MobLook.Mushroom ? Mushroom : Slime;
        public Sprite ItemSprite(ItemIcon icon) => itemSprites[(int)icon];
        public Sprite SkillIcon(SkillId id) => skillIcons[(int)id];

        /// <summary>Bigger piles of mesos look richer, as in MapleStory.</summary>
        public Sprite MesoSprite(int amount) => amount < 10 ? bronzeCoin : amount < 50 ? goldCoin : coinPile;

        public Sprite LootSprite(Loot loot) => loot.IsMesos ? MesoSprite(loot.Mesos) : ItemSprite(loot.Item.Icon);

        public static Color32 Rgb(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        /// <summary>Every sprite this library has made, for the editor's art preview.</summary>
        public IEnumerable<Sprite> AllSprites()
        {
            foreach (var o in owned)
                if (o is Sprite sprite) yield return sprite;
        }

        public void Dispose()
        {
            foreach (var o in owned) Util.SafeDestroy(o);
            owned.Clear();
        }

        Sprite MakeSprite(PixelCanvas canvas, float pivotX, float pivotY, string name)
        {
            var texture = canvas.ToTexture(name);
            var sprite = Sprite.Create(texture, new Rect(0, 0, canvas.Width, canvas.Height),
                new Vector2(pivotX / canvas.Width, pivotY / canvas.Height), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }

        // ---------------------------------------------------------------- hero (faces right)

        Sprite BuildHead(bool back)
        {
            var c = new PixelCanvas(16, 16);
            c.FillEllipse(8f, 7f, 5.5f, 5f, back ? Hair : Skin);
            c.FillEllipse(7.5f, 9.5f, 6.5f, 5f, Hair, minY: 9);
            c.FillRect(5, 13, 3, 1, HairLight);
            if (!back)
            {
                c.FillRect(2, 4, 3, 5, Hair); // hair behind the ear
                c[6, 8] = Hair;               // bangs
                c[9, 8] = Hair;
                c[10, 8] = Hair;
                c[5, 6] = SkinShade;          // ear
                c[5, 7] = SkinShade;
                c.FillRect(10, 5, 2, 3, Line); // eye
                c[11, 7] = White;
                c[12, 4] = Blush;
            }
            c.Outline(Line);
            return MakeSprite(c, 8f, 2f, back ? "HeadBack" : "Head");
        }

        Sprite BuildBody()
        {
            var c = new PixelCanvas(10, 10);
            c.FillRect(2, 1, 6, 8, Tunic);
            c.FillRect(2, 8, 6, 1, TunicLight);
            c.FillRect(2, 2, 6, 1, Belt);
            c[5, 2] = Gold;
            c.Outline(Line);
            return MakeSprite(c, 5f, 1f, "Body");
        }

        Sprite BuildArm()
        {
            var c = new PixelCanvas(5, 9);
            c.FillRect(1, 4, 3, 4, Tunic);
            c.FillRect(1, 1, 3, 3, Skin);
            c.Outline(Line);
            return MakeSprite(c, 2.5f, 7.5f, "Arm");
        }

        Sprite BuildLeg()
        {
            var c = new PixelCanvas(6, 8);
            c.FillRect(1, 3, 3, 4, Pants);
            c.FillRect(1, 1, 4, 2, Shoe);
            c.Outline(Line);
            return MakeSprite(c, 2.5f, 7f, "Leg");
        }

        Sprite BuildSword()
        {
            var c = new PixelCanvas(7, 20);
            c.FillRect(3, 1, 1, 3, Grip);
            c.FillRect(1, 4, 5, 1, Gold);
            c.FillRect(2, 5, 3, 12, Blade);
            c.FillRect(4, 5, 1, 12, BladeShade);
            c[3, 17] = Blade;
            c.Outline(Line);
            return MakeSprite(c, 3.5f, 2.5f, "Sword");
        }

        Sprite BuildTombstone()
        {
            var c = new PixelCanvas(12, 15);
            c.FillRect(2, 1, 8, 9, StoneGray);
            c.FillEllipse(6f, 10f, 4f, 3.5f, StoneGray);
            c.FillRect(5, 3, 2, 8, StoneDark);
            c.FillRect(3, 7, 6, 2, StoneDark);
            c.Outline(Line);
            return MakeSprite(c, 6f, 0f, "Tombstone");
        }

        Sprite BuildSlash()
        {
            var c = new PixelCanvas(16, 30);
            const float cx = 1f, cy = 15f;
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                    // The crescent thins toward its tips.
                    float inner = 10f + angle / 80f * 2.5f;
                    if (angle > 80f || r < inner || r > 13.5f) continue;
                    byte alpha = (byte)Mathf.RoundToInt(255f * (1f - angle / 80f * 0.6f));
                    c[x, y] = new Color32(255, 255, 255, alpha);
                }
            }
            return MakeSprite(c, cx, cy, "Slash");
        }

        Sprite BuildParticle()
        {
            var c = new PixelCanvas(2, 2);
            c.FillRect(0, 0, 2, 2, White);
            return MakeSprite(c, 1f, 1f, "Particle");
        }

        // ---------------------------------------------------------------- monsters (face right)

        Sprite BuildSlime()
        {
            var c = new PixelCanvas(20, 16);
            c.FillEllipse(10f, 6.5f, 8.5f, 6f, SlimeGreen, minY: 1);
            c.FillRect(4, 1, 12, 1, SlimeGreen);
            c.ShadeBelow(2, SlimeDark);
            c.FillEllipse(6.5f, 9.5f, 2f, 1.4f, SlimeLight);
            c.FillRect(11, 6, 2, 3, Line);
            c.FillRect(14, 6, 2, 3, Line);
            c[11, 8] = White;
            c[14, 8] = White;
            c[13, 4] = SlimeDark;
            c[10, 12] = Leaf;
            c[10, 13] = Leaf;
            c[9, 14] = Leaf;
            c[8, 14] = Leaf;
            c[11, 14] = Leaf;
            c[12, 14] = Leaf;
            c.Outline(Line);
            return MakeSprite(c, 10f, 0f, "Slime");
        }

        Sprite BuildMushroom()
        {
            var c = new PixelCanvas(20, 17);
            c.FillRect(6, 1, 8, 7, Stem);
            c.FillRect(6, 1, 1, 7, StemShade);
            c.FillRect(10, 3, 1, 2, Line);
            c.FillRect(12, 3, 1, 2, Line);
            c.FillEllipse(10f, 8.5f, 8.5f, 6.5f, Cap, minY: 7);
            for (int x = 0; x < c.Width; x++)
                if (c.IsOpaque(x, 7)) c[x, 7] = CapDark;
            c.FillEllipse(6f, 11.5f, 1.6f, 1.3f, Spot);
            c.FillEllipse(13.5f, 12.5f, 1.8f, 1.3f, Spot);
            c.FillEllipse(10f, 14f, 1.2f, 0.8f, Spot);
            c.Outline(Line);
            return MakeSprite(c, 10f, 0f, "Mushroom");
        }

        // ---------------------------------------------------------------- loot (pivot at the bottom, so it rests on footholds)

        Sprite BuildPotion(Color32 liquid, Color32 shine, string name)
        {
            var c = new PixelCanvas(14, 16);
            c.FillEllipse(7f, 6f, 5f, 5f, liquid);
            c.FillRect(5, 10, 4, 3, Rgb(0xdfeef5)); // glass neck
            c.FillRect(5, 13, 4, 2, Belt);          // cork
            c[4, 7] = shine;
            c[4, 8] = shine;
            c[5, 9] = shine;
            c.Outline(Line);
            return MakeSprite(c, 7f, 0f, name);
        }

        Sprite BuildGel()
        {
            var c = new PixelCanvas(14, 12);
            c.FillEllipse(7f, 5f, 5.5f, 4f, SlimeGreen, minY: 1);
            c.FillRect(6, 9, 2, 1, SlimeGreen);
            c.ShadeBelow(2, SlimeDark);
            c[4, 6] = SlimeLight;
            c[5, 7] = SlimeLight;
            c.Outline(Line);
            return MakeSprite(c, 7f, 0f, "SlimeGel");
        }

        Sprite BuildCapIcon()
        {
            var c = new PixelCanvas(16, 11);
            c.FillEllipse(8f, 2f, 7f, 7.5f, Cap, minY: 2);
            c.FillRect(1, 1, 14, 1, CapDark);
            c.FillEllipse(5f, 6f, 1.4f, 1.1f, Spot);
            c.FillEllipse(11f, 6.5f, 1.5f, 1.1f, Spot);
            c.Outline(Line);
            return MakeSprite(c, 8f, 0f, "CapshroomCap");
        }

        Sprite BuildCoin(Color32 face, Color32 shine, Color32 rim, string name)
        {
            var c = new PixelCanvas(11, 11);
            c.FillEllipse(5.5f, 5.5f, 4.5f, 4.5f, rim);
            c.FillEllipse(5.5f, 5.5f, 3.5f, 3.5f, face);
            c[4, 7] = shine;
            c[3, 6] = shine;
            c.FillRect(5, 4, 1, 3, rim);
            c.Outline(Line);
            return MakeSprite(c, 5.5f, 0f, name);
        }

        Sprite BuildCoinPile()
        {
            var c = new PixelCanvas(16, 12);
            var face = Rgb(0xf2c94c);
            var rim = Rgb(0xb88a1c);
            c.FillEllipse(5f, 4f, 4f, 3f, rim);
            c.FillEllipse(11f, 4f, 4f, 3f, rim);
            c.FillEllipse(8f, 7f, 4f, 3f, rim);
            c.FillEllipse(5f, 4.5f, 3f, 2f, face);
            c.FillEllipse(11f, 4.5f, 3f, 2f, face);
            c.FillEllipse(8f, 7.5f, 3f, 2f, face);
            c[7, 9] = Rgb(0xfff09a);
            c.Outline(Line);
            return MakeSprite(c, 8f, 0f, "CoinPile");
        }

        // ---------------------------------------------------------------- skill icons (18x18 tiles)

        PixelCanvas IconTile(Color32 background)
        {
            var c = new PixelCanvas(18, 18);
            c.FillRect(1, 1, 16, 16, background);
            return c;
        }

        Sprite BuildPowerStrikeIcon()
        {
            var c = IconTile(Rgb(0x6b2d1f));
            c.Line(4, 4, 12, 12, Blade);  // blade
            c.Line(5, 4, 12, 11, BladeShade);
            c.Line(3, 7, 7, 3, Gold);     // guard
            c.Line(2, 2, 3, 3, Grip);
            c.Line(13, 13, 15, 15, Rgb(0xfff09a)); // glint at the tip
            c[15, 13] = Gold;
            c[13, 15] = Gold;
            c.Outline(Line);
            return MakeSprite(c, 9f, 9f, "PowerStrikeIcon");
        }

        Sprite BuildSlashBlastIcon()
        {
            var c = IconTile(Rgb(0x1f3a6b));
            for (int y = 0; y < 18; y++)
            {
                for (int x = 0; x < 18; x++)
                {
                    float dx = x + 0.5f - 4f, dy = y + 0.5f - 9f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dx > 0f && r >= 8f && r <= 11f && x < 17 && y > 0 && y < 17)
                        c[x, y] = r < 9.5f ? Rgb(0xbfe3ff) : Rgb(0x6fb2ff);
                }
            }
            c.Line(3, 3, 9, 9, Blade);
            c.Line(2, 6, 6, 2, Gold);
            c.Outline(Line);
            return MakeSprite(c, 9f, 9f, "SlashBlastIcon");
        }

        Sprite BuildRageIcon()
        {
            var c = IconTile(Rgb(0x5a1414));
            c.FillEllipse(9f, 6.5f, 5f, 4.5f, Rgb(0xe8412c));
            c.FillEllipse(9f, 9f, 3.5f, 6f, Rgb(0xe8412c));
            c.FillEllipse(9f, 6f, 3f, 3f, Rgb(0xff9a2c));
            c.FillEllipse(9f, 5.5f, 1.5f, 1.8f, Rgb(0xffe27a));
            c[6, 14] = Rgb(0xff9a2c);
            c[12, 13] = Rgb(0xff9a2c);
            c.Outline(Line);
            return MakeSprite(c, 9f, 9f, "RageIcon");
        }

        // ---------------------------------------------------------------- scenery

        /// <summary>A platform image whose top edge (pivot) is the walking surface. Grass pokes above it.</summary>
        public Sprite Platform(float widthUnits, float depthUnits, bool solid, int seed)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(widthUnits * PixelsPerUnit));
            int body = Mathf.Max(8, Mathf.RoundToInt(depthUnits * PixelsPerUnit));
            const int bladeRows = 3;
            var c = new PixelCanvas(w, body + bladeRows);
            var rng = new System.Random(seed);

            for (int y = 0; y < body; y++)
            {
                int depth = body - 1 - y; // 0 is the surface row
                Color32 color;
                if (depth == 0) color = GrassLight;
                else if (depth <= 3) color = Grass;
                else if (depth == 4) color = GrassDark;
                else color = depth % 7 == 0 ? DirtDark : Dirt;
                c.FillRect(0, y, w, 1, color);
            }

            // Grass dripping over the dirt.
            for (int x = 0; x < w; x++)
            {
                int drip = rng.Next(4);
                for (int d = 0; d < drip; d++) c[x, body - 6 - d] = GrassDark;
            }

            if (body > 12)
            {
                int pebbles = w * (body - 10) / 90;
                for (int i = 0; i < pebbles; i++)
                {
                    float px = rng.Next(w), py = rng.Next(0, body - 10);
                    c.FillEllipse(px, py, 1.2f + (float)rng.NextDouble() * 1.3f, 0.8f + (float)rng.NextDouble() * 0.6f, Pebble);
                }
            }

            if (!solid)
            {
                c.FillRect(0, 0, w, 1, DirtDeep);
                c.FillRect(0, 1, w, 1, DirtDark);
                c.FillRect(0, 0, 1, body - 5, DirtDeep);
                c.FillRect(w - 1, 0, 1, body - 5, DirtDeep);
                c[0, 0] = default;
                c[w - 1, 0] = default;
            }

            for (int x = 0; x < w; x++)
            {
                int roll = rng.Next(12);
                if (roll < 4) c[x, body] = Grass;
                if (roll == 0) c[x, body + 1] = GrassLight;
            }

            return MakeSprite(c, 0f, body, "Platform");
        }

        /// <summary>A rope image hanging down from its pivot at the top.</summary>
        public Sprite Rope(float lengthUnits)
        {
            int h = Mathf.Max(4, Mathf.RoundToInt(lengthUnits * PixelsPerUnit) + 1);
            var c = new PixelCanvas(4, h);
            for (int y = 0; y < h; y++)
            {
                c[0, y] = RopeLine;
                c[3, y] = RopeLine;
                for (int x = 1; x <= 2; x++) c[x, y] = (y / 2 + x) % 2 == 0 ? RopeLight : RopeDark;
            }
            return MakeSprite(c, 2f, h, "Rope");
        }

        /// <summary>A rolling silhouette for a parallax layer. Pivot is bottom-left.</summary>
        public Sprite Hills(float widthUnits, float heightUnits, Color32 fill, Color32 rim, int seed, float bumpiness)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(widthUnits * PixelsPerUnit));
            int h = Mathf.Max(4, Mathf.RoundToInt(heightUnits * PixelsPerUnit));
            var c = new PixelCanvas(w, h);
            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f, p3 = (float)rng.NextDouble() * 6.28f;
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)PixelsPerUnit;
                float top = h * (0.62f + 0.17f * Mathf.Sin(t * 0.21f + p1) + 0.1f * Mathf.Sin(t * 0.53f + p2)
                                 + bumpiness * 0.06f * Mathf.Sin(t * 1.9f + p3));
                int ti = Mathf.Clamp(Mathf.RoundToInt(top), 3, h - 1);
                c.FillRect(x, 0, 1, ti, fill);
                c.FillRect(x, ti - 2, 1, 2, rim);
            }
            return MakeSprite(c, 0f, 0f, "Hills");
        }

        /// <summary>A vertical gradient, filtered smoothly so it can be stretched over the view.</summary>
        public Sprite Sky(Color top, Color bottom)
        {
            const int h = 64;
            var texture = new Texture2D(2, h, TextureFormat.RGBA32, false)
            {
                name = "Sky",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                var color = Color.Lerp(bottom, top, y / (h - 1f));
                pixels[y * 2] = color;
                pixels[y * 2 + 1] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "Sky";
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }

        Sprite BuildCloud()
        {
            var c = new PixelCanvas(40, 18);
            c.FillEllipse(12f, 7f, 8f, 5f, White);
            c.FillEllipse(21f, 9f, 9f, 7f, White);
            c.FillEllipse(30f, 7f, 7f, 5f, White);
            c.FillRect(6, 2, 28, 5, White);
            c.ShadeBelow(3, CloudShade);
            return MakeSprite(c, 20f, 0f, "Cloud");
        }

        Sprite BuildBush()
        {
            var c = new PixelCanvas(26, 13);
            c.FillEllipse(8f, 5f, 6f, 4.5f, GrassDark);
            c.FillEllipse(14f, 6.5f, 7f, 5.5f, GrassDark);
            c.FillEllipse(20f, 5f, 5.5f, 4.5f, GrassDark);
            c.FillEllipse(12f, 9f, 3f, 1.5f, Grass);
            c.FillEllipse(19f, 7f, 2f, 1.2f, Grass);
            c.Outline(LeafLine);
            return MakeSprite(c, 13f, 1f, "Bush");
        }

        Sprite BuildTree()
        {
            var c = new PixelCanvas(44, 58);
            c.FillRect(19, 1, 6, 30, Trunk);
            c.FillRect(19, 1, 2, 30, TrunkShade);
            c.FillEllipse(22f, 42f, 16f, 13f, LeafDark);
            c.FillEllipse(14f, 36f, 9f, 7f, LeafDark);
            c.FillEllipse(30f, 37f, 10f, 7f, LeafDark);
            c.FillEllipse(17f, 46f, 7f, 5f, LeafMid);
            c.FillEllipse(27f, 49f, 5f, 3f, LeafLight);
            c.Outline(LeafLine);
            return MakeSprite(c, 22f, 1f, "Tree");
        }

        Sprite BuildFlower(Color32 petal)
        {
            var c = new PixelCanvas(5, 7);
            c.FillRect(2, 1, 1, 3, Leaf);
            c[1, 5] = petal;
            c[3, 5] = petal;
            c[2, 4] = petal;
            c[2, 6] = petal;
            c[2, 5] = Gold;
            return MakeSprite(c, 2.5f, 1f, "Flower");
        }
    }

    public static class Util
    {
        /// <summary>Destroy that also works outside play mode (edit-mode tests, editor tools).</summary>
        public static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
