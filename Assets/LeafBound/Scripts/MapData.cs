using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// A horizontal surface things stand on. Non-solid footholds are one-way platforms:
    /// you can jump up through them and drop down through them.
    /// </summary>
    public readonly struct Foothold
    {
        public readonly float X1, X2, Y;
        public readonly bool Solid;

        public Foothold(float x1, float x2, float y, bool solid = false)
        {
            X1 = x1;
            X2 = x2;
            Y = y;
            Solid = solid;
        }

        public float Width => X2 - X1;
        public bool Covers(float x) => x >= X1 && x <= X2;
    }

    /// <summary>A vertical climbable rope. Top normally sits exactly on a foothold.</summary>
    public readonly struct Rope
    {
        public readonly float X, Bottom, Top;

        public Rope(float x, float bottom, float top)
        {
            X = x;
            Bottom = bottom;
            Top = top;
        }
    }

    public readonly struct MobSpawn
    {
        public readonly MobDef Def;
        public readonly int Foothold;
        public readonly float X;

        public MobSpawn(MobDef def, int foothold, float x)
        {
            Def = def;
            Foothold = foothold;
            X = x;
        }
    }

    /// <summary>Layout of one map. Positions are in world units (16 pixels each), y up.</summary>
    public sealed class MapData
    {
        public const float Epsilon = 0.001f;
        public const float RopeReach = 0.45f;

        public string Name = "";
        public float MinX, MaxX, BottomY, TopY;
        public Vector2 PlayerSpawn;
        public readonly List<Foothold> Footholds = new List<Foothold>();
        public readonly List<Rope> Ropes = new List<Rope>();
        public readonly List<MobSpawn> Spawns = new List<MobSpawn>();

        public float Width => MaxX - MinX;
        public float Height => TopY - BottomY;

        /// <summary>
        /// The highest foothold crossed while moving down from <paramref name="prevY"/> to
        /// <paramref name="newY"/> at <paramref name="x"/>, or -1.
        /// </summary>
        public int FindLanding(float x, float prevY, float newY, int ignore)
        {
            int best = -1;
            for (int i = 0; i < Footholds.Count; i++)
            {
                if (i == ignore) continue;
                var f = Footholds[i];
                if (!f.Covers(x) || prevY < f.Y - Epsilon || newY > f.Y) continue;
                if (best < 0 || f.Y > Footholds[best].Y) best = i;
            }
            return best;
        }

        /// <summary>The foothold under x whose height is within tolerance of y, or -1.</summary>
        public int FindFootholdNear(float x, float y, float tolerance)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Footholds.Count; i++)
            {
                var f = Footholds[i];
                float dist = Mathf.Abs(f.Y - y);
                if (!f.Covers(x) || dist > tolerance || dist >= bestDist) continue;
                best = i;
                bestDist = dist;
            }
            return best;
        }

        /// <summary>The highest foothold under x at or below y, no more than maxDrop down, or -1.</summary>
        public int FindFootholdBelow(float x, float y, float maxDrop)
        {
            int best = -1;
            for (int i = 0; i < Footholds.Count; i++)
            {
                var f = Footholds[i];
                if (!f.Covers(x) || f.Y > y + Epsilon || f.Y < y - maxDrop) continue;
                if (best < 0 || f.Y > Footholds[best].Y) best = i;
            }
            return best;
        }

        /// <summary>A rope someone at (x, feetY) can grab by pressing up, or -1.</summary>
        public int FindGrabbableRope(float x, float feetY)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Ropes.Count; i++)
            {
                var r = Ropes[i];
                float dist = Mathf.Abs(x - r.X);
                if (dist > RopeReach || dist >= bestDist) continue;
                if (feetY < r.Bottom - 0.6f || feetY > r.Top - 0.25f) continue;
                best = i;
                bestDist = dist;
            }
            return best;
        }

        /// <summary>A rope hanging from the foothold someone at (x, feetY) stands on, or -1.</summary>
        public int FindRopeHangingFrom(float x, float feetY)
        {
            for (int i = 0; i < Ropes.Count; i++)
            {
                var r = Ropes[i];
                if (Mathf.Abs(x - r.X) <= RopeReach && Mathf.Abs(feetY - r.Top) <= 0.3f) return i;
            }
            return -1;
        }

        /// <summary>The hunting ground: ground floor, five one-way platforms, four ropes.</summary>
        public static MapData CreateMossyMeadow()
        {
            var map = new MapData
            {
                Name = "Mossy Meadow",
                MinX = 0f,
                MaxX = 60f,
                BottomY = -2f,
                TopY = 14f,
                PlayerSpawn = new Vector2(2.5f, 0f),
            };

            map.Footholds.Add(new Foothold(0f, 60f, 0f, solid: true)); // 0: ground
            map.Footholds.Add(new Foothold(6f, 16f, 2.5f));            // 1: low left (jumpable)
            map.Footholds.Add(new Foothold(21f, 33f, 5f));             // 2: middle
            map.Footholds.Add(new Foothold(37f, 47f, 2.5f));           // 3: low right (jumpable)
            map.Footholds.Add(new Foothold(41f, 55f, 7.5f));           // 4: high right
            map.Footholds.Add(new Foothold(6f, 17f, 8f));              // 5: high left
            map.Footholds.Add(new Foothold(25f, 35f, 10.5f));          // 6: top

            map.Ropes.Add(new Rope(27f, 0.4f, 5f));    // ground -> middle
            map.Ropes.Add(new Rope(9f, 2.9f, 8f));     // low left -> high left
            map.Ropes.Add(new Rope(44f, 2.9f, 7.5f));  // low right -> high right
            map.Ropes.Add(new Rope(31f, 5.4f, 10.5f)); // middle -> top

            var slime = MobDef.SproutSlime;
            var shroom = MobDef.Capshroom;
            foreach (float x in new[] { 8f, 14f, 20f, 33f, 38f, 50f, 56f })
                map.Spawns.Add(new MobSpawn(slime, 0, x));
            map.Spawns.Add(new MobSpawn(slime, 1, 11f));
            map.Spawns.Add(new MobSpawn(slime, 3, 42f));
            map.Spawns.Add(new MobSpawn(shroom, 2, 24f));
            map.Spawns.Add(new MobSpawn(shroom, 4, 48f));
            map.Spawns.Add(new MobSpawn(shroom, 4, 52f));
            map.Spawns.Add(new MobSpawn(shroom, 5, 13f));
            map.Spawns.Add(new MobSpawn(shroom, 6, 29f));
            return map;
        }
    }
}
