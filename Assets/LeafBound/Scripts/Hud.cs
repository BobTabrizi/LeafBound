using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// Immediate-mode HUD: MapleStory-style status bar, minimap, message log, name tags,
    /// mob HP bars and damage numbers. Laid out on a 720-pixel-tall virtual screen.
    /// </summary>
    public sealed class Hud
    {
        const float RefHeight = 720f;
        const float LogLifetime = 8f;

        static readonly string[] HelpLines =
        {
            "Arrows / WASD  -  Move, climb ropes",
            "Space / Alt  -  Jump",
            "Down + Jump  -  Drop through a platform",
            "Ctrl / X  -  Attack (hold to keep swinging)",
            "H  -  Hide this help",
        };

        struct LogLine
        {
            public string Text;
            public Color Color;
            public float Age;
        }

        public string PlayerName = "Leafling";
        public bool ShowHelp = true;

        readonly List<LogLine> log = new List<LogLine>();
        float bannerTime;
        GUIStyle left, center, right, title, popup;

        public void Log(string text, Color color)
        {
            log.Add(new LogLine { Text = text, Color = color });
            if (log.Count > 7) log.RemoveAt(0);
        }

        public void ShowMapBanner() => bannerTime = 4f;

        public void Tick(float dt)
        {
            bannerTime -= dt;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                var line = log[i];
                line.Age += dt;
                if (line.Age > LogLifetime) log.RemoveAt(i);
                else log[i] = line;
            }
        }

        public void Draw(Game game)
        {
            // Nothing here is interactive, so only the repaint pass matters.
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();

            float scale = Mathf.Max(0.01f, Screen.height / RefHeight);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float vw = Screen.width / scale, vh = RefHeight;

            DrawWorldOverlays(game, scale);
            DrawStatusBar(game.Player.Stats, vw, vh);
            DrawMinimap(game);
            DrawLog(vw, vh);
            if (ShowHelp) DrawHelp(vw);
            if (bannerTime > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(bannerTime));
                Outlined(new Rect(0f, 70f, vw, 40f), game.Map.Name, title, Color.white);
                GUI.color = oldColor;
            }
            if (game.Player.IsDead) DrawFainted(game.Player, vw, vh);

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        void EnsureStyles()
        {
            if (left != null) return;
            left = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            left.normal.textColor = Color.white; // GUI.color does the tinting
            center = new GUIStyle(left) { alignment = TextAnchor.MiddleCenter };
            right = new GUIStyle(left) { alignment = TextAnchor.MiddleRight };
            title = new GUIStyle(center) { fontSize = 30, fontStyle = FontStyle.Bold };
            popup = new GUIStyle(center) { fontStyle = FontStyle.Bold };
        }

        // ---------------------------------------------------------------- in-world overlays

        void DrawWorldOverlays(Game game, float scale)
        {
            var cam = game.Camera;

            foreach (var mob in game.Mobs)
            {
                if (mob.IsDead) continue;
                if (mob.HpBarTimer > 0f)
                {
                    var top = WorldToGui(cam, mob.Position + new Vector2(0f, mob.Def.Height + 0.35f), scale);
                    var bar = new Rect(top.x - 22f, top.y - 4f, 44f, 7f);
                    Fill(bar, new Color(0f, 0f, 0f, 0.7f));
                    Fill(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * mob.Hp / mob.Def.MaxHp, bar.height - 2f),
                        new Color(0.95f, 0.25f, 0.25f));
                }
                if (mob.HpBarTimer > 0f || mob.IsAggro)
                    NameTag(WorldToGui(cam, mob.Position, scale), $"Lv.{mob.Def.Level} {mob.Def.Name}", new Color(1f, 0.9f, 0.6f));
            }

            if (!game.Player.IsDead)
                NameTag(WorldToGui(cam, game.Player.Motor.Position, scale), PlayerName, Color.white);

            var oldColor = GUI.color;
            foreach (var p in game.Effects.Popups)
            {
                float k = p.Age / p.Life;
                float rise = 1.1f * (1f - (1f - k) * (1f - k));
                var pos = WorldToGui(cam, p.Position + new Vector2(0f, rise), scale);
                GUI.color = new Color(1f, 1f, 1f, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                popup.fontSize = p.Size;
                Outlined(new Rect(pos.x - 150f, pos.y - 25f, 300f, 50f), p.Text, popup, p.Color, 2f);
            }
            GUI.color = oldColor;
        }

        static Vector2 WorldToGui(Camera cam, Vector2 world, float scale)
        {
            var screen = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
        }

        void NameTag(Vector2 feet, string text, Color color)
        {
            var size = left.CalcSize(new GUIContent(text));
            var box = new Rect(feet.x - size.x * 0.5f - 4f, feet.y + 3f, size.x + 8f, size.y + 2f);
            Fill(box, new Color(0f, 0f, 0f, 0.55f));
            Tinted(box, text, center, color);
        }

        // ---------------------------------------------------------------- screen-space panels

        void DrawStatusBar(PlayerStats s, float vw, float vh)
        {
            const float height = 52f;
            float y = vh - height;
            Fill(new Rect(0f, y, vw, height), new Color(0.08f, 0.09f, 0.13f, 0.9f));
            Fill(new Rect(0f, y, vw, 2f), new Color(1f, 1f, 1f, 0.15f));

            title.fontSize = 26;
            title.alignment = TextAnchor.MiddleLeft;
            Outlined(new Rect(14f, y + 6f, 80f, 40f), $"Lv.{s.Level}", title, Effects.Gold);
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 30;

            Tinted(new Rect(104f, y + 8f, 110f, 18f), PlayerName, left, Color.white);
            Tinted(new Rect(104f, y + 26f, 110f, 18f), "Beginner", left, new Color(0.7f, 0.75f, 0.8f));

            Bar(new Rect(220f, y + 8f, 250f, 16f), (float)s.Hp / s.MaxHp, new Color(0.9f, 0.2f, 0.2f), $"HP  {s.Hp} / {s.MaxHp}");
            string exp = s.Level >= PlayerStats.MaxLevel
                ? "EXP  MAX"
                : $"EXP  {s.Exp} / {s.ExpNeeded}  [{s.ExpFraction * 100f:0.00}%]";
            Bar(new Rect(220f, y + 28f, 250f, 16f), s.ExpFraction, new Color(0.95f, 0.8f, 0.2f), exp);

            Tinted(new Rect(vw - 216f, y + 8f, 200f, 18f), $"ATT  {s.MinDamage} ~ {s.MaxDamage}", right, Color.white);
            Tinted(new Rect(vw - 216f, y + 26f, 200f, 18f), $"STR {s.Str}   DEX {s.Dex}", right, new Color(0.7f, 0.75f, 0.8f));
        }

        void DrawMinimap(Game game)
        {
            var map = game.Map;
            const float width = 200f;
            float height = Mathf.Clamp(width * map.Height / map.Width, 40f, 160f);
            Fill(new Rect(10f, 10f, width + 12f, height + 32f), new Color(0f, 0f, 0f, 0.55f));
            Tinted(new Rect(16f, 12f, width, 20f), map.Name, left, Color.white);
            var area = new Rect(16f, 34f, width, height);
            Fill(area, new Color(0.2f, 0.35f, 0.5f, 0.55f));

            Vector2 ToMini(Vector2 p) => new Vector2(
                area.x + (p.x - map.MinX) / map.Width * area.width,
                area.y + (map.TopY - p.y) / map.Height * area.height);

            foreach (var f in map.Footholds)
            {
                var a = ToMini(new Vector2(f.X1, f.Y));
                var b = ToMini(new Vector2(f.X2, f.Y));
                Fill(new Rect(a.x, a.y - 1f, b.x - a.x, 2f), new Color(0.6f, 0.9f, 0.5f));
            }
            foreach (var r in map.Ropes)
            {
                var top = ToMini(new Vector2(r.X, r.Top));
                var bottom = ToMini(new Vector2(r.X, r.Bottom));
                Fill(new Rect(top.x - 0.5f, top.y, 1f, bottom.y - top.y), new Color(0.85f, 0.7f, 0.45f));
            }
            foreach (var mob in game.Mobs)
                if (!mob.IsDead) Dot(ToMini(mob.Position + new Vector2(0f, 0.3f)), 3f, new Color(1f, 0.35f, 0.3f));
            Dot(ToMini(game.Player.Motor.Position + new Vector2(0f, 0.3f)), 5f, new Color(1f, 0.95f, 0.3f));
        }

        void DrawLog(float vw, float vh)
        {
            var oldColor = GUI.color;
            for (int i = 0; i < log.Count; i++)
            {
                var line = log[i];
                float y = vh - 76f - (log.Count - 1 - i) * 18f;
                GUI.color = new Color(1f, 1f, 1f, line.Age < LogLifetime - 2f ? 1f : (LogLifetime - line.Age) / 2f);
                Outlined(new Rect(vw - 520f, y, 504f, 18f), line.Text, right, line.Color);
            }
            GUI.color = oldColor;
        }

        void DrawHelp(float vw)
        {
            float height = 30f + HelpLines.Length * 18f;
            var box = new Rect(vw - 300f, 10f, 290f, height);
            Fill(box, new Color(0f, 0f, 0f, 0.55f));
            Tinted(new Rect(box.x + 10f, box.y + 6f, 270f, 18f), "Controls", left, Effects.Gold);
            for (int i = 0; i < HelpLines.Length; i++)
                Tinted(new Rect(box.x + 10f, box.y + 26f + i * 18f, 270f, 18f), HelpLines[i], left, Color.white);
        }

        void DrawFainted(Player player, float vw, float vh)
        {
            Fill(new Rect(0f, 0f, vw, vh), new Color(0f, 0f, 0f, 0.35f));
            Outlined(new Rect(0f, vh * 0.35f, vw, 40f), "You fainted!", title, Color.white);
            Outlined(new Rect(0f, vh * 0.35f + 40f, vw, 24f), $"Reviving in {Mathf.CeilToInt(Mathf.Max(0f, player.ReviveTimer))}...",
                center, new Color(0.85f, 0.85f, 0.9f));
        }

        // ---------------------------------------------------------------- primitives

        void Bar(Rect r, float fraction, Color color, string text)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.65f));
            var inner = new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * Mathf.Clamp01(fraction), r.height - 2f);
            Fill(inner, color);
            Fill(new Rect(inner.x, inner.y, inner.width, inner.height * 0.4f), new Color(1f, 1f, 1f, 0.25f));
            Outlined(r, text, center, Color.white);
        }

        static void Fill(Rect r, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Dot(Vector2 center, float size, Color color) =>
            Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), color);

        static void Tinted(Rect r, string text, GUIStyle style, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        /// <summary>Text with a dark outline so it reads over any background.</summary>
        static void Outlined(Rect r, string text, GUIStyle style, Color color, float thickness = 1f)
        {
            var shadow = new Color(0f, 0f, 0f, 0.9f);
            Tinted(new Rect(r.x - thickness, r.y, r.width, r.height), text, style, shadow);
            Tinted(new Rect(r.x + thickness, r.y, r.width, r.height), text, style, shadow);
            Tinted(new Rect(r.x, r.y - thickness, r.width, r.height), text, style, shadow);
            Tinted(new Rect(r.x, r.y + thickness, r.width, r.height), text, style, shadow);
            Tinted(r, text, style, color);
        }
    }
}
