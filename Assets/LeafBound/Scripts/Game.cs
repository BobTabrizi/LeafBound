using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// Root of the game. Builds the world in code, runs the simulation in a fixed order each frame
    /// (player, then mobs, then effects), then updates views and the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Game : MonoBehaviour
    {
        public const float MaxTickDelta = 1f / 30f;
        public const float MobRespawnTime = 7f;
        public const float ReviveTime = 3f;

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
        public Camera Camera => cam;
        public Effects Effects { get; private set; }
        public Hud Hud { get; private set; }

        readonly List<Mob> mobs = new List<Mob>();
        readonly List<SpawnSlot> slots = new List<SpawnSlot>();
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
        float clock;

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
            Hud = new Hud();
            Player = new Player();
            playerView = new PlayerView(art, worldRoot);
            LoadMap(MapData.CreateMossyMeadow());
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
            autoPilot?.Update(Time.deltaTime);
            Tick(Mathf.Min(Time.deltaTime, MaxTickDelta));
        }

        void LateUpdate()
        {
            if (!initialized) return;
            float dt = Mathf.Min(Time.deltaTime, MaxTickDelta);
            playerView.Apply(Player, dt, clock);
            foreach (var mob in mobs) mob.UpdateView();
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
            TickPlayer(dt);
            TickMobs(dt);
            Effects.Tick(dt);
            Hud.Tick(dt);
        }

        public void LoadMap(MapData map)
        {
            mapView?.Destroy();
            foreach (var mob in mobs) DestroyView(mob);
            mobs.Clear();
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
            foreach (var mob in mobs) DestroyView(mob);
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
            var go = new GameObject(spawn.Def.Name);
            go.transform.SetParent(worldRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = art.MobSprite(spawn.Def.Look);
            renderer.sortingOrder = 20;
            mob.AttachView(go.transform, renderer);
            mob.UpdateView();
            mobs.Add(mob);
            return mob;
        }

        static void DestroyView(Mob mob)
        {
            if (mob.View != null) Util.SafeDestroy(mob.View.gameObject);
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

            if (p.AttackTimer > 0f)
            {
                p.AttackTimer = Mathf.Max(0f, p.AttackTimer - dt);
                if (!p.AttackHitDone && Player.AttackDuration - p.AttackTimer >= Player.AttackHitTime)
                {
                    p.AttackHitDone = true;
                    ResolveAttack();
                }
            }
            else if (Controls.Held(GameAction.Attack) && p.Motor.State != MotorState.Climb)
            {
                p.AttackTimer = Player.AttackDuration;
                p.AttackHitDone = false;
                sfx.Play(sfx.Swing);
            }

            p.Motor.Tick(dt, Controls, Map, p.IsAttacking);
            if (p.Motor.JumpedThisTick) sfx.Play(sfx.Jump, 0.7f);
            if (p.Motor.Position.y < Map.BottomY - 5f) p.Motor.Teleport(Map.PlayerSpawn);
            CheckMobContact();
        }

        void ResolveAttack()
        {
            const float reachFront = 1.6f, reachBack = 0.35f;
            var m = Player.Motor;
            float left = m.Facing > 0 ? m.Position.x - reachBack : m.Position.x - reachFront;
            var box = new Rect(left, m.Position.y - 0.1f, reachFront + reachBack, 1.5f);

            Mob target = null;
            float best = float.MaxValue;
            foreach (var mob in mobs)
            {
                if (mob.IsDead || !box.Overlaps(mob.Hitbox)) continue;
                float dist = Mathf.Abs(mob.Position.x - m.Position.x);
                if (dist < best)
                {
                    best = dist;
                    target = mob;
                }
            }
            if (target == null) return;

            int damage = Player.Stats.RollDamage(rng, out bool critical);
            bool killed = target.TakeHit(damage, m.Position.x);
            var center = target.Position + new Vector2(0f, target.Def.Height * 0.5f);
            Effects.DamageNumber(target.Position + new Vector2(0f, target.Def.Height + 0.2f), damage, critical, onPlayer: false);
            Effects.Burst(center, new Color(1f, 0.95f, 0.7f), critical ? 10 : 6, 6f, 0.25f, 0f);
            sfx.Play(sfx.Hit);
            if (killed) OnMobKilled(target);
        }

        void OnMobKilled(Mob mob)
        {
            sfx.Play(sfx.Kill, 0.8f);
            Effects.Burst(mob.Position + new Vector2(0f, mob.Def.Height * 0.5f), new Color(0.85f, 1f, 0.75f), 12, 4f, 0.5f, 6f);
            int levels = Player.Stats.GainExp(mob.Def.Exp);
            Hud.Log($"You have gained experience (+{mob.Def.Exp})", Color.white);
            if (levels <= 0) return;

            sfx.Play(sfx.LevelUp);
            var head = Player.Motor.Position + new Vector2(0f, PlayerMotor.Height + 0.6f);
            Effects.Text(head, "LEVEL UP!", Effects.Gold, 36, 2f);
            Effects.Burst(Player.Motor.Position + new Vector2(0f, 0.8f), Effects.Gold, 30, 7f, 1f, -2f);
            Hud.Log($"Congratulations! You reached level {Player.Stats.Level}.", Effects.Gold);
        }

        void CheckMobContact()
        {
            var p = Player;
            if (p.Invincible > 0f) return;
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
            m.Velocity = Vector2.zero;
            // Rest the tombstone on whatever is below.
            int below = Map.FindFootholdBelow(m.Position.x, m.Position.y, 100f);
            if (below >= 0) m.Position.y = Map.Footholds[below].Y;
            Hud.Log("You fainted. Reviving at the start of the map...", new Color(1f, 0.6f, 0.6f));
        }

        void Revive()
        {
            Player.Stats.HealFull();
            Player.Motor.Teleport(Map.PlayerSpawn);
            Player.Invincible = 2f;
            cameraPlaced = false;
        }

        // ---------------------------------------------------------------- mobs

        void TickMobs(float dt)
        {
            float playerX = Player.Motor.Position.x;
            for (int i = mobs.Count - 1; i >= 0; i--)
            {
                var mob = mobs[i];
                mob.Tick(dt, Map, playerX, rng);
                if (!mob.IsDead || mob.DeathTimer > 0f) continue;

                DestroyView(mob);
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
