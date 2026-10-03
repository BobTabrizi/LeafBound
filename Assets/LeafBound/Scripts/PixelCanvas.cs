using UnityEngine;

namespace LeafBound
{
    /// <summary>A tiny paint program for drawing pixel art in code. (0, 0) is bottom-left.</summary>
    public sealed class PixelCanvas
    {
        public readonly int Width, Height;
        readonly Color32[] pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
        }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Color32 this[int x, int y]
        {
            get => Contains(x, y) ? pixels[y * Width + x] : default;
            set
            {
                if (Contains(x, y)) pixels[y * Width + x] = value;
            }
        }

        public bool IsOpaque(int x, int y) => this[x, y].a > 0;

        public void FillRect(int x, int y, int w, int h, Color32 color)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    this[i, j] = color;
        }

        /// <summary>Fills pixels whose centers fall inside the ellipse, limited to rows minY..maxY.</summary>
        public void FillEllipse(float cx, float cy, float rx, float ry, Color32 color,
            int minY = int.MinValue, int maxY = int.MaxValue)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.Max(minY, Mathf.FloorToInt(cy - ry)), y1 = Mathf.Min(maxY, Mathf.CeilToInt(cy + ry));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) this[x, y] = color;
                }
            }
        }

        /// <summary>A straight one-pixel line (Bresenham), endpoints included.</summary>
        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                this[x0, y0] = color;
                if (x0 == x1 && y0 == y1) return;
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>Recolors opaque pixels in rows at or below maxY.</summary>
        public void ShadeBelow(int maxY, Color32 color)
        {
            for (int y = 0; y <= maxY && y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (IsOpaque(x, y)) this[x, y] = color;
        }

        /// <summary>Draws a one-pixel border around everything opaque. Leave a pixel of margin for it.</summary>
        public void Outline(Color32 color)
        {
            var source = (Color32[])pixels.Clone();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (source[y * Width + x].a > 0) continue;
                    if (OpaqueIn(source, x - 1, y) || OpaqueIn(source, x + 1, y)
                        || OpaqueIn(source, x, y - 1) || OpaqueIn(source, x, y + 1))
                        pixels[y * Width + x] = color;
                }
            }
        }

        bool OpaqueIn(Color32[] source, int x, int y) => Contains(x, y) && source[y * Width + x].a > 0;

        public Texture2D ToTexture(string name)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }
    }
}
