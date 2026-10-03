using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// Immediate-mode HUD: MapleStory-style status bar with quickslots, minimap, message log,
    /// name tags, mob HP bars, damage numbers, buff timers, and the Inventory and Skills windows.
    /// Laid out on a 720-pixel-tall virtual screen.
    /// </summary>
    public sealed class Hud
    {
        const float RefHeight = 720f;
        const float LogLifetime = 8f;
        const float StatusBarHeight = 52f;
        const int InventoryWindowId = 7101;
        const int SkillsWindowId = 7102;

        static readonly string[] HelpLines =
        {
            "Arrows / WASD  -  Move, climb ropes",
            "Space / Alt  -  Jump   (Down + Jump: drop)",
            "Ctrl / X  -  Attack",
            "Q / E / R  -  Power Strike / Slash Blast / Rage",
            "Z  -  Pick up loot",
            "1 / 2  -  Red / Blue Potion",
            "I / K  -  Inventory / Skills    H  -  Hide help",
        };

        static readonly string[] TabNames = { "Use", "Etc" };
        static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.12f, 0.94f);
        static readonly Color TitleBarColor = new Color(0.17f, 0.22f, 0.33f, 1f);
        static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color DimText = new Color(0.7f, 0.75f, 0.8f);

        struct LogLine
        {
            public string Text;
            public Color Color;
            public float Age;
        }

        public string PlayerName = "Leafling";
        public bool ShowHelp = true;
        public bool ShowInventory;
        public bool ShowSkills;

        readonly ArtLibrary art;
        readonly List<LogLine> log = new List<LogLine>();
        float bannerTime, clock;
        Rect inventoryRect, skillsRect;
        ItemKind inventoryTab = ItemKind.Use;
        Game current;
        GUIStyle left, center, right, small, title, popup, button;

        public Hud(ArtLibrary art)
        {
            this.art = art;
        }

        public void Log(string text, Color color)
        {
            log.Add(new LogLine { Text = text, Color = color });
            if (log.Count > 7) log.RemoveAt(0);
        }

        public void ShowMapBanner() => bannerTime = 4f;

        public void Tick(float dt)
        {
            clock += dt;
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
            EnsureStyles();
            current = game;

            float scale = Mathf.Max(0.01f, Screen.height / RefHeight);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float vw = Screen.width / scale, vh = RefHeight;

            // Passive drawing only needs the repaint pass; the windows below also handle clicks.
            if (Event.current.type == EventType.Repaint)
            {
                DrawWorldOverlays(game, scale);
                DrawStatusBar(game.Player, vw, vh);
                DrawMinimap(game);
                DrawLog(vw, vh);
                if (ShowHelp) DrawHelp(vw);
                DrawBuffs(game.Player, vw);
                if (game.Player.Skills.Points > 0) DrawSkillPointHint(game.Player.Skills.Points, vh);
                if (bannerTime > 0f)
                {
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(bannerTime));
                    Outlined(new Rect(0f, 70f, vw, 40f), game.Map.Name, title, Color.white);
                    GUI.color = oldColor;
                }
                if (game.Player.IsDead) DrawFainted(game.Player, vw, vh);
            }

            if (ShowInventory)
            {
                if (inventoryRect.width <= 0f) inventoryRect = new Rect(vw - 650f, 140f, 300f, 360f);
                inventoryRect = KeepOnScreen(GUI.Window(InventoryWindowId, inventoryRect, InventoryWindow, GUIContent.none, GUIStyle.none), vw, vh);
            }
            if (ShowSkills)
            {
                if (skillsRect.width <= 0f) skillsRect = new Rect(vw - 340f, 190f, 330f, 266f);
                skillsRect = KeepOnScreen(GUI.Window(SkillsWindowId, skillsRect, SkillsWindow, GUIContent.none, GUIStyle.none), vw, vh);
            }

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
            small = new GUIStyle(left) { fontSize = 11 };
            title = new GUIStyle(center) { fontSize = 30, fontStyle = FontStyle.Bold };
            popup = new GUIStyle(center) { fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        }

        static Rect KeepOnScreen(Rect r, float vw, float vh)
        {
            r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, vw - r.width));
            r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, vh - StatusBarHeight - r.height));
            return r;
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

        void DrawStatusBar(Player player, float vw, float vh)
        {
            var s = player.Stats;
            float y = vh - StatusBarHeight;
            Fill(new Rect(0f, y, vw, StatusBarHeight), new Color(0.08f, 0.09f, 0.13f, 0.9f));
            Fill(new Rect(0f, y, vw, 2f), new Color(1f, 1f, 1f, 0.15f));

            title.fontSize = 26;
            title.alignment = TextAnchor.MiddleLeft;
            Outlined(new Rect(14f, y + 6f, 80f, 40f), $"Lv.{s.Level}", title, Effects.Gold);
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 30;

            Tinted(new Rect(104f, y + 8f, 110f, 18f), PlayerName, left, Color.white);
            Tinted(new Rect(104f, y + 26f, 110f, 18f), "Warrior", left, DimText);

            Bar(new Rect(220f, y + 8f, 200f, 16f), (float)s.Hp / s.MaxHp, new Color(0.9f, 0.2f, 0.2f), $"HP  {s.Hp} / {s.MaxHp}");
            Bar(new Rect(426f, y + 8f, 200f, 16f), (float)s.Mp / s.MaxMp, new Color(0.25f, 0.5f, 0.95f), $"MP  {s.Mp} / {s.MaxMp}");
            string exp = s.Level >= PlayerStats.MaxLevel
                ? "EXP  MAX"
                : $"EXP  {s.Exp} / {s.ExpNeeded}  [{s.ExpFraction * 100f:0.00}%]";
            Bar(new Rect(220f, y + 28f, 406f, 16f), s.ExpFraction, new Color(0.95f, 0.8f, 0.2f), exp);

            float slotsLeft = vw - 5f * 44f - 8f;
            var attackColor = s.BonusAttack > 0 ? new Color(1f, 0.6f, 0.4f) : Color.white;
            Tinted(new Rect(slotsLeft - 140f, y + 8f, 128f, 18f), $"ATT  {s.MinDamage} ~ {s.MaxDamage}", right, attackColor);
            Tinted(new Rect(slotsLeft - 140f, y + 26f, 128f, 18f), $"{player.Inventory.Mesos:N0} mesos", right, Effects.Gold);
            DrawQuickSlots(player, slotsLeft, y + 6f);
        }

        void DrawQuickSlots(Player player, float x, float y)
        {
            var mp = player.Stats.Mp;
            for (int i = 0; i < SkillDef.All.Length; i++)
            {
                var def = SkillDef.All[i];
                int level = player.Skills.Level(def.Id);
                string corner = level > 0 ? def.MpCost(level).ToString() : "-";
                bool usable = level > 0 && mp >= def.MpCost(level);
                QuickSlot(new Rect(x + i * 44f, y, 40f, 40f), "QER"[i].ToString(), art.SkillIcon(def.Id), corner, usable);
            }
            x += SkillDef.All.Length * 44f;
            var red = player.Inventory.Count(ItemDef.RedPotion);
            var blue = player.Inventory.Count(ItemDef.BluePotion);
            QuickSlot(new Rect(x, y, 40f, 40f), "1", art.ItemSprite(ItemIcon.RedPotion), red.ToString(), red > 0);
            QuickSlot(new Rect(x + 44f, y, 40f, 40f), "2", art.ItemSprite(ItemIcon.BluePotion), blue.ToString(), blue > 0);
        }

        void QuickSlot(Rect r, string key, Sprite icon, string corner, bool usable)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(r.x, r.y, r.width, 1f), new Color(1f, 1f, 1f, 0.2f));
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, usable ? 1f : 0.3f);
            DrawSprite(new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f), icon);
            GUI.color = old;
            Outlined(new Rect(r.x + 3f, r.y + 1f, 20f, 12f), key, small, Color.white);
            small.alignment = TextAnchor.MiddleRight;
            Outlined(new Rect(r.x, r.y + r.height - 13f, r.width - 3f, 12f), corner, small, usable ? Color.white : new Color(1f, 0.5f, 0.5f));
            small.alignment = TextAnchor.MiddleLeft;
        }

        void DrawBuffs(Player player, float vw)
        {
            if (player.RageTimer <= 0f) return;
            float x = ShowHelp ? vw - 354f : vw - 46f;
            var r = new Rect(x, 10f, 34f, 34f);
            var old = GUI.color;
            bool ending = player.RageTimer < 5f && Mathf.FloorToInt(clock * 4f) % 2 == 0;
            GUI.color = new Color(1f, 1f, 1f, ending ? 0.4f : 1f);
            Fill(r, new Color(0f, 0f, 0f, 0.6f));
            DrawSprite(new Rect(r.x + 2f, r.y + 2f, 30f, 30f), art.SkillIcon(SkillId.Rage));
            GUI.color = old;
            Outlined(new Rect(r.x - 10f, r.yMax + 1f, r.width + 20f, 14f), $"{Mathf.CeilToInt(player.RageTimer)}s", center, Color.white);
        }

        void DrawSkillPointHint(int points, float vh)
        {
            string text = $"{points} skill point{(points == 1 ? "" : "s")} available - press K";
            var size = left.CalcSize(new GUIContent(text));
            var box = new Rect(10f, vh - StatusBarHeight - 28f, size.x + 16f, 22f);
            Fill(box, new Color(0f, 0f, 0f, 0.6f));
            float alpha = 0.7f + 0.3f * Mathf.Sin(clock * 4f);
            Outlined(new Rect(box.x + 8f, box.y + 2f, size.x, 18f), text, left, new Color(1f, 0.85f, 0.25f, alpha));
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
                float y = vh - StatusBarHeight - 24f - (log.Count - i) * 18f;
                GUI.color = new Color(1f, 1f, 1f, line.Age < LogLifetime - 2f ? 1f : (LogLifetime - line.Age) / 2f);
                Outlined(new Rect(vw - 520f, y, 504f, 18f), line.Text, right, line.Color);
            }
            GUI.color = oldColor;
        }

        void DrawHelp(float vw)
        {
            float height = 30f + HelpLines.Length * 18f;
            var box = new Rect(vw - 310f, 10f, 300f, height);
            Fill(box, new Color(0f, 0f, 0f, 0.55f));
            Tinted(new Rect(box.x + 10f, box.y + 6f, 280f, 18f), "Controls", left, Effects.Gold);
            for (int i = 0; i < HelpLines.Length; i++)
                Tinted(new Rect(box.x + 10f, box.y + 26f + i * 18f, 280f, 18f), HelpLines[i], left, Color.white);
        }

        void DrawFainted(Player player, float vw, float vh)
        {
            Fill(new Rect(0f, 0f, vw, vh), new Color(0f, 0f, 0f, 0.35f));
            Outlined(new Rect(0f, vh * 0.35f, vw, 40f), "You fainted!", title, Color.white);
            Outlined(new Rect(0f, vh * 0.35f + 40f, vw, 24f), $"Reviving in {Mathf.CeilToInt(Mathf.Max(0f, player.ReviveTimer))}...",
                center, new Color(0.85f, 0.85f, 0.9f));
        }

        // ---------------------------------------------------------------- windows (these handle clicks)

        /// <summary>Background and title bar. Returns true when the close button is clicked.</summary>
        bool WindowFrame(Rect r, string caption)
        {
            Fill(new Rect(0f, 0f, r.width, r.height), PanelColor);
            Fill(new Rect(0f, 0f, r.width, 24f), TitleBarColor);
            Tinted(new Rect(10f, 3f, r.width - 40f, 18f), caption, left, Effects.Gold);
            return GUI.Button(new Rect(r.width - 24f, 3f, 20f, 18f), "x", button);
        }

        void InventoryWindow(int id)
        {
            var r = inventoryRect;
            if (WindowFrame(r, "Inventory")) ShowInventory = false;
            var inventory = current.Player.Inventory;
            inventoryTab = (ItemKind)GUI.Toolbar(new Rect(10f, 32f, r.width - 20f, 22f), (int)inventoryTab, TabNames, button);

            float y = 62f, bottom = r.height - 36f;
            bool any = false;
            foreach (var item in inventory.Items(inventoryTab))
            {
                if (y + 36f > bottom) break;
                any = true;
                Fill(new Rect(10f, y, r.width - 20f, 36f), RowColor);
                DrawSprite(new Rect(14f, y + 3f, 30f, 30f), art.ItemSprite(item.Icon));
                Tinted(new Rect(52f, y + 3f, 150f, 16f), item.Name, left, Color.white);
                Tinted(new Rect(52f, y + 19f, 170f, 14f), item.Description, small, DimText);
                bool usable = item.Kind == ItemKind.Use;
                float countRight = usable ? r.width - 70f : r.width - 18f;
                Tinted(new Rect(countRight - 60f, y + 3f, 60f, 16f), $"x{inventory.Count(item)}", right, Color.white);
                if (usable && GUI.Button(new Rect(r.width - 62f, y + 7f, 46f, 22f), "Use", button)) current.UseItem(item);
                y += 40f;
            }
            if (!any) Tinted(new Rect(10f, y, r.width - 20f, 20f), "Nothing here yet.", center, DimText);

            Fill(new Rect(0f, r.height - 30f, r.width, 30f), new Color(0f, 0f, 0f, 0.3f));
            DrawSprite(new Rect(12f, r.height - 25f, 20f, 20f), art.MesoSprite(10));
            Tinted(new Rect(38f, r.height - 25f, r.width - 50f, 20f), $"{inventory.Mesos:N0} mesos", left, Effects.Gold);
            GUI.DragWindow(new Rect(0f, 0f, r.width - 28f, 24f));
        }

        void SkillsWindow(int id)
        {
            var r = skillsRect;
            if (WindowFrame(r, "Skills")) ShowSkills = false;
            var book = current.Player.Skills;
            Tinted(new Rect(12f, 30f, 200f, 18f), "Warrior  (keys Q / E / R)", left, DimText);
            Tinted(new Rect(r.width - 112f, 30f, 100f, 18f), $"SP  {book.Points}", right, Effects.Gold);

            float y = 54f;
            for (int i = 0; i < SkillDef.All.Length; i++)
            {
                var def = SkillDef.All[i];
                int level = book.Level(def.Id);
                Fill(new Rect(10f, y, r.width - 20f, 62f), RowColor);

                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, level > 0 ? 1f : 0.4f);
                DrawSprite(new Rect(16f, y + 13f, 36f, 36f), art.SkillIcon(def.Id));
                GUI.color = old;

                Tinted(new Rect(60f, y + 4f, 160f, 18f), def.Name, left, Color.white);
                Tinted(new Rect(60f, y + 4f, r.width - 116f, 18f), $"Lv. {level} / {def.MaxLevel}", right, level > 0 ? Effects.Gold : DimText);
                Tinted(new Rect(60f, y + 23f, r.width - 110f, 16f), level > 0 ? def.Describe(level) : "Not learned yet", small, Color.white);
                if (level < def.MaxLevel)
                    Tinted(new Rect(60f, y + 40f, r.width - 110f, 16f), "Next: " + def.Describe(level + 1), small, DimText);

                GUI.enabled = book.CanLearn(def.Id);
                if (GUI.Button(new Rect(r.width - 46f, y + 18f, 28f, 26f), "+", button)) current.LearnSkill(def.Id);
                GUI.enabled = true;
                y += 66f;
            }
            GUI.DragWindow(new Rect(0f, 0f, r.width - 28f, 24f));
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

        static void DrawSprite(Rect r, Sprite sprite)
        {
            if (sprite != null) GUI.DrawTexture(r, sprite.texture, ScaleMode.ScaleToFit);
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
