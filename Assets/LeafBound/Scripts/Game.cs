using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// Root of the game. Builds the world in code, runs the simulation in a fixed order each frame
    /// (player, then mobs, then drops, then effects), then updates views and the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Game : MonoBehaviour
    {
        public const float MaxTickDelta = 1f / 30f;
        public const float MobRespawnTime = 7f;
        public const float ReviveTime = 3f;
        public const float PotionDelay = 0.4f;
        public const float PickupDelay = 0.12f;
        public const float RegenInterval = 5f;
        public const int MaxDropsOnGround = 60;

        static readonly Color WarningColor = new Color(1f, 0.6f, 0.6f);
        static readonly Color HpHealColor = new Color(0.45f, 1f, 0.45f);
        static readonly Color MpHealColor = new Color(0.45f, 0.75f, 1f);
        static readonly Color RageColor = new Color(1f, 0.4f, 0.2f);
        static readonly Color WindColor = new Color(0.75f, 0.95f, 1f);

        sealed class SpawnSlot
        {
            public MobSpawn Spawn;
            public Mob Mob;
            public float Timer;
        }

        public static Game Instance { get; private set; }

        public IGameInput Controls { get; set; } = new KeyboardInput();
        public MapData Map { get; private set; }
        public Player Player { get; private set; }
        public IReadOnlyList<Mob> Mobs => mobs;
        public IReadOnlyList<Drop> Drops => drops;
        public Camera Camera => cam;
        public Effects Effects { get; private set; }
        public Hud Hud { get; private set; }

        readonly List<Mob> mobs = new List<Mob>();
        readonly List<Drop> drops = new List<Drop>();
        readonly List<SpawnSlot> slots = new List<SpawnSlot>();
        readonly List<Mob> targets = new List<Mob>();
        System.Random rng;
        ArtLibrary art;
        Sfx sfx;
        MapView mapView;
        PlayerView playerView;
        AutoPilot autoPilot;
        Transform worldRoot;
        Camera cam;
        bool ownsCamera, cameraPlaced, initialized;
        Vector2 cameraPos;
        float clock, notifyCooldown;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("LeafBound: only one Game can run at a time; disabling the extra one.");
                enabled = false;
                return;
            }
            Instance = this;

            rng = new System.Random();
            art = new ArtLibrary();
            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            SetupCamera();
            sfx = new Sfx(gameObject);
            Effects = new Effects(art, worldRoot, rng);
            Hud = new Hud(art);
            Player = new Player();
            playerView = new PlayerView(art, worldRoot);
            LoadMap(MapData.CreateMossyMeadow());
            Hud.Log($"You have {Player.Skills.Points} skill points. Press K to learn skills.", Effects.Gold);
            autoPilot = AutoPilot.FromCommandLine(this);
            initialized = true;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            sfx?.Dispose();
            art?.Dispose();
            if (ownsCamera && cam != null) Destroy(cam.gameObject);
        }

        void Update()
        {
            if (!initialized) return;
            if (KeyboardInput.HelpTogglePressed()) Hud.ShowHelp = !Hud.ShowHelp;
            if (KeyboardInput.InventoryTogglePressed()) Hud.ShowInventory = !Hud.ShowInventory;
            if (KeyboardInput.SkillsTogglePressed()) Hud.ShowSkills = !Hud.ShowSkills;
            if (KeyboardInput.CloseWindowsPressed()) Hud.ShowInventory = Hud.ShowSkills = false;
            autoPilot?.Update(Time.deltaTime);
            Tick(Mathf.Min(Time.deltaTime, MaxTickDelta));
        }

        void LateUpdate()
        {
            if (!initialized) return;
            float dt = Mathf.Min(Time.deltaTime, MaxTickDelta);
            playerView.Apply(Player, dt, clock);
            foreach (var mob in mobs) mob.UpdateView();
            foreach (var drop in drops) drop.UpdateView(Player.Motor.Position);
            UpdateCamera(dt);
        }

        void OnGUI()
        {
            if (initialized) Hud.Draw(this);
        }

        /// <summary>Advances the simulation. Public so tests can fast-forward.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            clock += dt;
            if (notifyCooldown > 0f) notifyCooldown -= dt;
            TickPlayer(dt);
            TickMobs(dt);
            TickDrops(dt);
            Effects.Tick(dt);
            Hud.Tick(dt);
        }

        public void LoadMap(MapData map)
        {
            mapView?.Destroy();
            foreach (var mob in mobs) DestroyView(mob.View);
            foreach (var drop in drops) DestroyView(drop.View);
            mobs.Clear();
            drops.Clear();
            slots.Clear();
            Effects.Clear();

            Map = map;
            mapView = new MapView(map, art, worldRoot, rng.Next());
            foreach (var spawn in map.Spawns)
            {
                var slot = new SpawnSlot { Spawn = spawn };
                slots.Add(slot);
                slot.Mob = SpawnMob(spawn, slots.Count - 1);
            }
            Player.Motor.Teleport(map.PlayerSpawn);
            cameraPlaced = false;
            Hud.ShowMapBanner();
        }

        /// <summary>Adds a one-off mob that will not respawn.</summary>
        public Mob SpawnMob(MobSpawn spawn) => SpawnMob(spawn, -1);

        /// <summary>Removes every mob and stops respawns (used by tests to set up a fight).</summary>
        public void ClearMobs()
        {
            foreach (var mob in mobs) DestroyView(mob.View);
            mobs.Clear();
            slots.Clear();
        }

        Mob SpawnMob(MobSpawn spawn, int slotIndex)
        {
            var position = new Vector2(spawn.X, Map.Footholds[spawn.Foothold].Y);
            var mob = new Mob(spawn.Def, spawn.Foothold, position)
            {
                SlotIndex = slotIndex,
                Facing = rng.Next(2) == 0 ? -1 : 1,
            };
            var renderer = CreateRenderer(spawn.Def.Name, art.MobSprite(spawn.Def.Look), 20);
            mob.AttachView(renderer.transform, renderer);
            mob.UpdateView();
            mobs.Add(mob);
            return mob;
        }

        SpriteRenderer CreateRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(worldRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        static void DestroyView(Transform view)
        {
            if (view != null) Util.SafeDestroy(view.gameObject);
        }

        void Notify(string text)
        {
            if (notifyCooldown > 0f) return;
            notifyCooldown = 1.5f;
            Hud.Log(text, WarningColor);
        }

        // ---------------------------------------------------------------- player

        void TickPlayer(float dt)
        {
            var p = Player;
            if (p.IsDead)
            {
                p.ReviveTimer -= dt;
                if (p.ReviveTimer <= 0f) Revive();
                return;
            }
            if (p.Invincible > 0f) p.Invincible -= dt;
            if (p.PotionCooldown > 0f) p.PotionCooldown -= dt;
            if (p.PickupCooldown > 0f) p.PickupCooldown -= dt;
            if (p.DashCooldown > 0f) p.DashCooldown -= dt;
            TickBuffs(dt);
            TickRegen(dt);

            if (p.AttackTimer > 0f)
            {
                p.AttackTimer = Mathf.Max(0f, p.AttackTimer - dt);
                if (!p.AttackHitDone && Player.DurationOf(p.Attack) - p.AttackTimer >= Player.HitTimeOf(p.Attack))
                {
                    p.AttackHitDone = true;
                    ResolveAttack(p.Attack);
                }
            }
            else if (p.Motor.State != MotorState.Climb)
            {
                TryStartAction();
            }

            if (p.PotionCooldown <= 0f)
            {
                if (Controls.Held(GameAction.HpPotion)) UseItem(ItemDef.RedPotion);
                else if (Controls.Held(GameAction.MpPotion)) UseItem(ItemDef.BluePotion);
            }
            if (p.PickupCooldown <= 0f && Controls.Held(GameAction.Pickup)) TryPickup();

            p.Motor.Tick(dt, Controls, Map, p.IsAttacking);
            if (p.Attack == AttackKind.Dash && p.Motor.IsDashing) DashSweep();
            if (p.Motor.JumpedThisTick) sfx.Play(sfx.Jump, 0.7f);
            if (p.Motor.Position.y < Map.BottomY - 5f) p.Motor.Teleport(Map.PlayerSpawn);
            CheckMobContact();
        }

        void TryStartAction()
        {
            // Skills take priority over the basic attack when both keys are held.
            if (Controls.Held(GameAction.Dash) && TryCast(SkillId.Dash, AttackKind.Dash)) return;
            if (Controls.Held(GameAction.Skill1) && TryCast(SkillId.PowerStrike, AttackKind.PowerStrike)) return;
            if (Controls.Held(GameAction.Skill2) && TryCast(SkillId.SlashBlast, AttackKind.SlashBlast)) return;
            if (Controls.Held(GameAction.Skill3) && TryCast(SkillId.Rage, AttackKind.Rage)) return;
            if (Controls.Held(GameAction.Attack))
            {
                Player.StartAttack(AttackKind.Basic);
                sfx.Play(sfx.Swing);
            }
        }

        bool TryCast(SkillId id, AttackKind kind)
        {
            var def = SkillDef.Get(id);
            int level = Player.Skills.Level(id);
            if (level <= 0)
            {
                Notify($"You haven't learned {def.Name} yet. Press K to spend skill points.");
                return false;
            }
            if (kind == AttackKind.Dash && Player.DashCooldown > 0f) return false;
            if (!Player.Stats.SpendMp(def.MpCost(level)))
            {
                Notify("Not enough MP.");
                return false;
            }
            Player.StartAttack(kind);
            if (kind == AttackKind.Dash)
            {
                StartDash(level);
                return true;
            }
            sfx.Play(kind == AttackKind.Rage ? sfx.Buff : sfx.Skill);
            if (kind != AttackKind.Rage) sfx.Play(sfx.Swing);
            return true;
        }

        public bool LearnSkill(SkillId id)
        {
            if (!Player.Skills.Learn(id)) return false;
            var def = SkillDef.Get(id);
            Hud.Log($"{def.Name} is now level {Player.Skills.Level(id)}.", Effects.Gold);
            sfx.Play(sfx.Pickup);
            return true;
        }

        void ResolveAttack(AttackKind kind)
        {
            var skills = Player.Skills;
            switch (kind)
            {
                case AttackKind.Basic:
                    HitMobs(1.6f, 0.35f, 1, 100, new Color(1f, 0.95f, 0.7f));
                    break;
                case AttackKind.PowerStrike:
                {
                    int level = skills.Level(SkillId.PowerStrike);
                    HitMobs(1.6f, 0.35f, 1, SkillDef.PowerStrike.DamagePercent(level), Effects.Gold);
                    break;
                }
                case AttackKind.SlashBlast:
                {
                    int level = skills.Level(SkillId.SlashBlast);
                    HitMobs(2.6f, 0.6f, SkillDef.SlashBlast.MaxTargets(level), SkillDef.SlashBlast.DamagePercent(level),
                        new Color(0.6f, 0.85f, 1f));
                    break;
                }
                case AttackKind.Rage:
                    ApplyRage(skills.Level(SkillId.Rage));
                    break;
                case AttackKind.Dash:
                    break; // Wind Dash hits along its path in DashSweep
            }
        }

        /// <summary>Damages up to maxTargets living mobs in front of the player, nearest first.</summary>
        void HitMobs(float reachFront, float reachBack, int maxTargets, int damagePercent, Color sparkColor)
        {
            var m = Player.Motor;
            float left = m.Facing > 0 ? m.Position.x - reachBack : m.Position.x - reachFront;
            var box = new Rect(left, m.Position.y - 0.1f, reachFront + reachBack, 1.7f);

            targets.Clear();
            foreach (var mob in mobs)
                if (!mob.IsDead && box.Overlaps(mob.Hitbox)) targets.Add(mob);
            if (targets.Count == 0) return;
            float px = m.Position.x;
            targets.Sort((a, b) => Mathf.Abs(a.Position.x - px).CompareTo(Mathf.Abs(b.Position.x - px)));

            int count = Mathf.Min(maxTargets, targets.Count);
            for (int i = 0; i < count; i++)
            {
                var target = targets[i];
                int damage = Mathf.Max(1, Mathf.RoundToInt(Player.Stats.RollDamage(rng, out bool critical) * damagePercent / 100f));
                bool killed = target.TakeHit(damage, px);
                Effects.DamageNumber(target.Position + new Vector2(0f, target.Def.Height + 0.2f), damage, critical, onPlayer: false);
                Effects.Burst(target.Position + new Vector2(0f, target.Def.Height * 0.5f), sparkColor, critical ? 10 : 6, 6f, 0.25f, 0f);
                if (killed) OnMobKilled(target);
            }
            sfx.Play(sfx.Hit);
        }

        void StartDash(int level)
        {
            var p = Player;
            var m = p.Motor;
            int h = (Controls.Held(GameAction.Right) ? 1 : 0) - (Controls.Held(GameAction.Left) ? 1 : 0);
            m.StartDash(h != 0 ? h : m.Facing, SkillDef.Dash.DashDistance(level), SkillDef.DashDuration);
            p.DashCooldown = SkillDef.DashCooldown;
            p.DashHits.Clear();
            sfx.Play(sfx.Dash);
            sfx.Play(sfx.DashVoice, 2f);
            Effects.Text(m.Position + new Vector2(0f, PlayerMotor.Height + 0.6f), "HASAGI!", WindColor, 30, 1.1f);
        }

        /// <summary>Cuts every monster the dash passes through, once each, up to the skill's target limit.</summary>
        void DashSweep()
        {
            var p = Player;
            var m = p.Motor;
            int level = p.Skills.Level(SkillId.Dash);
            int maxTargets = SkillDef.Dash.MaxTargets(level);
            int percent = SkillDef.Dash.DamagePercent(level);
            var box = m.Hitbox;
            box.xMin -= 0.4f;
            box.xMax += 0.4f;

            foreach (var mob in mobs)
            {
                if (p.DashHits.Count >= maxTargets) break;
                if (mob.IsDead || p.DashHits.Contains(mob) || !box.Overlaps(mob.Hitbox)) continue;
                p.DashHits.Add(mob);
                int damage = Mathf.Max(1, Mathf.RoundToInt(p.Stats.RollDamage(rng, out bool critical) * percent / 100f));
                bool killed = mob.TakeHit(damage, m.Position.x - m.Facing);
                Effects.DamageNumber(mob.Position + new Vector2(0f, mob.Def.Height + 0.2f), damage, critical, onPlayer: false);
                Effects.Burst(mob.Position + new Vector2(0f, mob.Def.Height * 0.5f), WindColor, 8, 6f, 0.3f, 0f);
                sfx.Play(sfx.Hit);
                if (killed) OnMobKilled(mob);
            }

            float y = 0.2f + (float)rng.NextDouble() * 1.1f;
            Effects.Burst(m.Position + new Vector2(-m.Facing * 0.4f, y), WindColor, 2, 1.5f, 0.3f, 0f);
        }

        void ApplyRage(int level)
        {
            var def = SkillDef.Rage;
            var p = Player;
            p.RageTimer = def.Duration(level);
            p.Stats.BonusAttack = def.AttackBonus(level);
            var center = p.Motor.Position + new Vector2(0f, 0.8f);
            Effects.Burst(center, RageColor, 24, 6f, 0.6f, -2f);
            Effects.Text(p.Motor.Position + new Vector2(0f, PlayerMotor.Height + 0.6f), "Rage!", RageColor, 28, 1.2f);
            Hud.Log($"Rage: +{p.Stats.BonusAttack} attack for {p.RageTimer:0} seconds.", RageColor);
        }

        void TickBuffs(float dt)
        {
            var p = Player;
            if (p.RageTimer <= 0f) return;
            p.RageTimer -= dt;
            p.AuraTimer -= dt;
            if (p.AuraTimer <= 0f)
            {
                p.AuraTimer = 0.2f;
                float x = ((float)rng.NextDouble() - 0.5f) * 0.8f;
                Effects.Burst(p.Motor.Position + new Vector2(x, 0.3f), RageColor, 1, 1.2f, 0.6f, -3f);
            }
            if (p.RageTimer <= 0f) EndRage(announce: true);
        }

        void EndRage(bool announce)
        {
            Player.RageTimer = 0f;
            Player.Stats.BonusAttack = 0;
            if (announce) Hud.Log("Rage has worn off.", new Color(0.8f, 0.8f, 0.85f));
        }

        void TickRegen(float dt)
        {
            var p = Player;
            p.RegenTimer += dt;
            if (p.RegenTimer < RegenInterval) return;
            p.RegenTimer -= RegenInterval;
            p.Stats.Heal(3 + p.Stats.Level / 2);
            p.Stats.RestoreMp(2 + p.Stats.Level / 3);
        }

        /// <summary>Uses one of a consumable item, e.g. from a hotkey or the inventory window.</summary>
        public bool UseItem(ItemDef item)
        {
            var p = Player;
            if (p.IsDead || item.Kind != ItemKind.Use || p.PotionCooldown > 0f) return false;
            if (!p.Inventory.Remove(item))
            {
                Notify($"You don't have any {item.Name}s.");
                return false;
            }
            p.PotionCooldown = PotionDelay;
            int hp = p.Stats.Heal(item.HealHp);
            int mp = p.Stats.RestoreMp(item.HealMp);
            var head = p.Motor.Position + new Vector2(0f, PlayerMotor.Height + 0.3f);
            if (hp > 0) Effects.Text(head, $"+{hp}", HpHealColor, 22, 0.9f);
            if (mp > 0) Effects.Text(head + new Vector2(0f, hp > 0 ? 0.4f : 0f), $"+{mp}", MpHealColor, 22, 0.9f);
            Effects.Burst(p.Motor.Position + new Vector2(0f, 0.8f), item.HealHp > 0 ? HpHealColor : MpHealColor, 8, 2.5f, 0.5f, -3f);
            sfx.Play(sfx.Potion);
            return true;
        }

        void TryPickup()
        {
            var reach = Player.Motor.Hitbox;
            reach.xMin -= 0.25f;
            reach.xMax += 0.25f;

            Drop best = null;
            float bestDist = float.MaxValue;
            foreach (var drop in drops)
            {
                if (!drop.CanBePickedUp || !reach.Overlaps(drop.Hitbox)) continue;
                float dist = Mathf.Abs(drop.Position.x - Player.Motor.Position.x);
                if (dist < bestDist)
                {
                    best = drop;
                    bestDist = dist;
                }
            }
            if (best == null) return;

            Player.PickupCooldown = PickupDelay;
            var inventory = Player.Inventory;
            if (best.Loot.IsMesos)
            {
                inventory.AddMesos(best.Loot.Mesos);
                Hud.Log($"You have gained mesos (+{best.Loot.Mesos}).", Color.white);
            }
            else if (inventory.Add(best.Loot.Item))
            {
                Hud.Log($"You have gained an item ({best.Loot.Item.Name}).", Color.white);
            }
            else
            {
                Notify($"You can't carry any more {best.Loot.Item.Name}s.");
                return;
            }
            best.StartPickup();
            sfx.Play(sfx.Pickup);
        }

        void OnMobKilled(Mob mob)
        {
            sfx.Play(sfx.Kill, 0.8f);
            Effects.Burst(mob.Position + new Vector2(0f, mob.Def.Height * 0.5f), new Color(0.85f, 1f, 0.75f), 12, 4f, 0.5f, 6f);
            SpawnDrops(mob);

            int levels = Player.Stats.GainExp(mob.Def.Exp);
            Hud.Log($"You have gained experience (+{mob.Def.Exp}).", Color.white);
            if (levels <= 0) return;

            int points = levels * Player.SkillPointsPerLevel;
            Player.Skills.AddPoints(points);
            sfx.Play(sfx.LevelUp);
            var head = Player.Motor.Position + new Vector2(0f, PlayerMotor.Height + 0.6f);
            Effects.Text(head, "LEVEL UP!", Effects.Gold, 36, 2f);
            Effects.Burst(Player.Motor.Position + new Vector2(0f, 0.8f), Effects.Gold, 30, 7f, 1f, -2f);
            Hud.Log($"Congratulations! You reached level {Player.Stats.Level}.", Effects.Gold);
            Hud.Log($"You gained {points} skill points. Press K to use them.", Effects.Gold);
        }

        void CheckMobContact()
        {
            var p = Player;
            if (p.Invincible > 0f || p.Motor.IsDashing) return; // dashing slips past monsters
            var box = p.Motor.Hitbox;
            foreach (var mob in mobs)
            {
                if (mob.IsDead || !box.Overlaps(mob.Hitbox)) continue;

                int damage = Mathf.Max(1, mob.Def.TouchDamage + rng.Next(-1, 2));
                bool fainted = p.Stats.TakeDamage(damage);
                Effects.DamageNumber(p.Motor.Position + new Vector2(0f, PlayerMotor.Height + 0.2f), damage, false, onPlayer: true);
                sfx.Play(sfx.Hurt);
                p.Invincible = Player.InvincibleTime;
                if (fainted) Faint();
                else p.Motor.Knockback(mob.Position.x);
                return;
            }
        }

        void Faint()
        {
            var m = Player.Motor;
            Player.ReviveTimer = ReviveTime;
            Player.AttackTimer = 0f;
            EndRage(announce: false);
            m.Velocity = Vector2.zero;
            // Rest the tombstone on whatever is below.
            int below = Map.FindFootholdBelow(m.Position.x, m.Position.y, 100f);
            if (below >= 0) m.Position.y = Map.Footholds[below].Y;
            Hud.Log("You fainted. Reviving at the start of the map...", WarningColor);
        }

        void Revive()
        {
            Player.Stats.HealFull();
            Player.Motor.Teleport(Map.PlayerSpawn);
            Player.Invincible = 2f;
            cameraPlaced = false;
        }

        // ---------------------------------------------------------------- mobs and drops

        void TickMobs(float dt)
        {
            float playerX = Player.Motor.Position.x;
            for (int i = mobs.Count - 1; i >= 0; i--)
            {
                var mob = mobs[i];
                mob.Tick(dt, Map, playerX, rng);
                if (!mob.IsDead || mob.DeathTimer > 0f) continue;

                DestroyView(mob.View);
                mobs.RemoveAt(i);
                if (mob.SlotIndex >= 0 && mob.SlotIndex < slots.Count)
                {
                    var slot = slots[mob.SlotIndex];
                    slot.Mob = null;
                    slot.Timer = MobRespawnTime;
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Mob != null) continue;
                slot.Timer -= dt;
                if (slot.Timer <= 0f) slot.Mob = SpawnMob(slot.Spawn, i);
            }
        }

        /// <summary>Rolls the mob's drop table and fans the loot out in a row, MapleStory style.</summary>
        void SpawnDrops(Mob mob)
        {
            var loot = mob.Def.Drops.Roll(rng);
            var origin = mob.Position + new Vector2(0f, 0.5f);
            for (int i = 0; i < loot.Count; i++)
            {
                float offset = (i - (loot.Count - 1) * 0.5f) * 0.5f;
                SpawnDrop(loot[i], origin, new Vector2(offset * 2.2f, 7f));
            }
        }

        /// <summary>Puts loot into the world. Public so tests can place drops directly.</summary>
        public Drop SpawnDrop(Loot loot, Vector2 position, Vector2 velocity)
        {
            if (drops.Count >= MaxDropsOnGround)
            {
                DestroyView(drops[0].View);
                drops.RemoveAt(0);
            }
            var drop = new Drop(loot, position, velocity, (float)rng.NextDouble() * 6.28f);
            var renderer = CreateRenderer(loot.IsMesos ? "Mesos" : loot.Item.Name, art.LootSprite(loot), 15);
            drop.AttachView(renderer.transform, renderer);
            drop.UpdateView(Player.Motor.Position);
            drops.Add(drop);
            return drop;
        }

        void TickDrops(float dt)
        {
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var drop = drops[i];
                drop.Tick(dt, Map);
                if (!drop.Finished) continue;
                DestroyView(drop.View);
                drops.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------------- camera

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                ownsCamera = true;
            }
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MapView.SkyBottom;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.rotation = Quaternion.identity;
        }

        void UpdateCamera(float dt)
        {
            // Whole-number pixel scaling keeps the pixel art crisp: about 11-12 units of height on screen.
            int screenHeight = Screen.height > 0 ? Screen.height : 720;
            int pixelScale = Mathf.Max(1, Mathf.RoundToInt(screenHeight / 192f));
            float unitsPerScreenPixel = 1f / (ArtLibrary.PixelsPerUnit * pixelScale);
            cam.orthographicSize = screenHeight * 0.5f * unitsPerScreenPixel;
            float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;

            var target = Player.Motor.Position + new Vector2(0f, 1.2f);
            cameraPos = cameraPlaced ? Vector2.Lerp(cameraPos, target, 1f - Mathf.Exp(-6f * dt)) : target;
            cameraPlaced = true;
            cameraPos.x = ClampCentered(cameraPos.x, Map.MinX + halfWidth, Map.MaxX - halfWidth);
            cameraPos.y = ClampCentered(cameraPos.y, Map.BottomY + halfHeight, Map.TopY - halfHeight);

            var snapped = new Vector2(
                Mathf.Round(cameraPos.x / unitsPerScreenPixel) * unitsPerScreenPixel,
                Mathf.Round(cameraPos.y / unitsPerScreenPixel) * unitsPerScreenPixel);
            cam.transform.position = new Vector3(snapped.x, snapped.y, -10f);
            mapView.Update(snapped, halfWidth, halfHeight, dt);
        }

        static float ClampCentered(float value, float min, float max) =>
            min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
