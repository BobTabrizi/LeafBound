using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>Builds a map's scenery (sky, parallax hills, clouds, platforms, ropes, decor) and scrolls it.</summary>
    public sealed class MapView
    {
        public static readonly Color SkyTop = new Color(0.42f, 0.70f, 0.96f);
        public static readonly Color SkyBottom = new Color(0.82f, 0.93f, 1f);

        // Room for the widest view a window is likely to have (about 3.5:1).
        const float ViewMargin = 20f;
        const float CloudParallax = 0.1f;

        sealed class Layer
        {
            public Transform Transform;
            public float Parallax, LeftX, BaseY;
        }

        sealed class Cloud
        {
            public Transform Transform;
            public float LocalX, Y, Speed;
        }

        readonly Transform root, sky;
        readonly Vector2 skySpriteSize;
        readonly List<Layer> layers = new List<Layer>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly float cloudMinX, cloudMaxX;

        public MapView(MapData map, ArtLibrary art, Transform parent, int seed)
        {
            root = new GameObject("Map").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed);

            var skySprite = art.Sky(SkyTop, SkyBottom);
            sky = Place("Sky", skySprite, Vector2.zero, -1000);
            skySpriteSize = skySprite.bounds.size;

            AddLayer(map, art.Hills(LayerWidth(map, 0.15f), 10f, ArtLibrary.Rgb(0xa9d9cf), ArtLibrary.Rgb(0xc4e8de), seed, 0f), 0.15f, -900);
            AddLayer(map, art.Hills(LayerWidth(map, 0.4f), 7.5f, ArtLibrary.Rgb(0x7cc47f), ArtLibrary.Rgb(0x9fdc92), seed + 1, 1f), 0.4f, -800);

            cloudMinX = map.MinX * CloudParallax - ViewMargin;
            cloudMaxX = map.MaxX * CloudParallax + ViewMargin;
            for (int i = 0; i < 7; i++)
            {
                var cloud = new Cloud
                {
                    LocalX = Mathf.Lerp(cloudMinX, cloudMaxX, (float)rng.NextDouble()),
                    Y = 8f + (float)rng.NextDouble() * 5f,
                    Speed = 0.15f + (float)rng.NextDouble() * 0.25f,
                };
                cloud.Transform = Place("Cloud", art.Cloud, new Vector2(cloud.LocalX, cloud.Y), -850);
                clouds.Add(cloud);
            }

            foreach (float x in new[] { 4f, 19f, 36f, 52f })
                Place("Tree", art.Tree, new Vector2(x, 0f), -50);

            for (int i = 0; i < map.Footholds.Count; i++)
            {
                var f = map.Footholds[i];
                float depth = f.Solid ? f.Y - map.BottomY : 0.75f;
                Place("Foothold", art.Platform(f.Width, depth, f.Solid, seed + 100 + i), new Vector2(f.X1, f.Y), f.Solid ? 0 : 1);

                int flowers = Mathf.RoundToInt(f.Width / (f.Solid ? 2.5f : 4f));
                for (int k = 0; k < flowers; k++)
                {
                    float fx = Mathf.Lerp(f.X1 + 0.5f, f.X2 - 0.5f, (float)rng.NextDouble());
                    Place("Flower", art.Flowers[rng.Next(art.Flowers.Length)], new Vector2(fx, f.Y), 3);
                }
            }

            // Ropes start one pixel under the platform surface so the platform hides the knot.
            foreach (var rope in map.Ropes)
                Place("Rope", art.Rope(rope.Top - rope.Bottom), new Vector2(rope.X, rope.Top - 1f / ArtLibrary.PixelsPerUnit), -1);

            foreach (float x in new[] { 10.5f, 23.5f, 30f, 45.5f, 57.5f })
                Place("Bush", art.Bush, new Vector2(x, 0f), 2);
        }

        static float LayerWidth(MapData map, float parallax) => map.Width * parallax + ViewMargin * 2f;

        void AddLayer(MapData map, Sprite sprite, float parallax, int order)
        {
            var layer = new Layer
            {
                Parallax = parallax,
                LeftX = map.MinX * parallax - ViewMargin,
                BaseY = map.BottomY,
            };
            layer.Transform = Place("Hills", sprite, new Vector2(layer.LeftX, layer.BaseY), order);
            layers.Add(layer);
        }

        Transform Place(string name, Sprite sprite, Vector2 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go.transform;
        }

        /// <summary>Keeps the sky behind the camera and slides the far layers for parallax.</summary>
        public void Update(Vector2 cameraPos, float halfWidth, float halfHeight, float dt)
        {
            sky.position = cameraPos;
            sky.localScale = new Vector3((halfWidth * 2f + 2f) / skySpriteSize.x, (halfHeight * 2f + 2f) / skySpriteSize.y, 1f);

            foreach (var layer in layers)
                layer.Transform.position = new Vector2(cameraPos.x * (1f - layer.Parallax) + layer.LeftX, layer.BaseY);

            foreach (var cloud in clouds)
            {
                cloud.LocalX += cloud.Speed * dt;
                if (cloud.LocalX > cloudMaxX) cloud.LocalX = cloudMinX;
                cloud.Transform.position = new Vector2(cameraPos.x * (1f - CloudParallax) + cloud.LocalX, cloud.Y);
            }
        }

        public void Destroy() => Util.SafeDestroy(root.gameObject);
    }
}
