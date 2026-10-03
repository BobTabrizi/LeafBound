using System;
using System.IO;
using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// Plays the game by itself when the player is launched with
    /// <c>-leafbound-autopilot &lt;folder&gt;</c>: learns skills, hunts with skills and loots on the
    /// ground, climbs a rope, hunts again, opens the windows, saves screenshots to the folder and
    /// logs its state, then quits. Used to smoke-test builds.
    /// </summary>
    public sealed class AutoPilot
    {
        public const string Flag = "-leafbound-autopilot";
        const float RopeX = 27f;

        static readonly float[] ShotTimes = { 1.5f, 5f, 10f, 15f, 19f, 23f, 27f, 32f, 36f };

        readonly Game game;
        readonly ScriptedInput input = new ScriptedInput();
        readonly string outputDir;
        float time, nextLog;
        int shotIndex, lastLevel = 1, lastCaptureFrame = -1;
        float quitAt = -1f, levelShotAt = -1f;
        bool learned, climbShot, swingShot, powerStrikeShot, slashBlastShot, rageShot, dropsShot, pickupShot;

        public static AutoPilot FromCommandLine(Game game)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == Flag) return new AutoPilot(game, args[i + 1]);
            return null;
        }

        AutoPilot(Game game, string outputDir)
        {
            this.game = game;
            this.outputDir = outputDir;
            Directory.CreateDirectory(outputDir);
            game.Controls = input;
            Application.runInBackground = true;
            Debug.Log($"[autopilot] started, writing to {outputDir}");
        }

        public void Update(float dt)
        {
            time += dt;
            Drive();

            if (time >= nextLog)
            {
                nextLog = time + 1f;
                var p = game.Player;
                var m = p.Motor;
                var s = p.Stats;
                int alive = 0;
                foreach (var mob in game.Mobs)
                    if (!mob.IsDead) alive++;
                Debug.Log($"[autopilot] t={time:0.0} pos=({m.Position.x:0.00},{m.Position.y:0.00}) state={m.State} " +
                          $"lv={s.Level} exp={s.Exp}/{s.ExpNeeded} hp={s.Hp}/{s.MaxHp} mp={s.Mp}/{s.MaxMp} " +
                          $"mesos={p.Inventory.Mesos} gel={p.Inventory.Count(ItemDef.SlimeGel)} cap={p.Inventory.Count(ItemDef.CapshroomCap)} " +
                          $"red={p.Inventory.Count(ItemDef.RedPotion)} blue={p.Inventory.Count(ItemDef.BluePotion)} " +
                          $"rage={p.RageTimer:0} sp={p.Skills.Points} drops={game.Drops.Count} mobs={alive} fps={1f / Mathf.Max(dt, 1e-4f):0}");
            }

            CaptureEvents();
            if (shotIndex < ShotTimes.Length && time >= ShotTimes[shotIndex] && Capture($"shot_{shotIndex:00}"))
            {
                shotIndex++;
                if (shotIndex == ShotTimes.Length) quitAt = time + 1f;
            }

            if (quitAt > 0f && time >= quitAt)
            {
                quitAt = -1f;
                Debug.Log("[autopilot] done");
                Application.Quit();
            }
        }

        /// <summary>Extra screenshots of moments the timed shots tend to miss.</summary>
        void CaptureEvents()
        {
            var p = game.Player;
            if (p.Stats.Level > lastLevel)
            {
                lastLevel = p.Stats.Level;
                levelShotAt = time + 0.3f;
            }
            if (levelShotAt > 0f && time >= levelShotAt && Capture($"levelup_{lastLevel}")) levelShotAt = -1f;

            float progress = p.AttackProgress;
            bool midSwing = progress > 0.42f && progress < 0.55f;
            if (!climbShot && p.Motor.State == MotorState.Climb && p.Motor.Position.y > 2f) climbShot = Capture("climb");
            if (!swingShot && midSwing && p.Attack == AttackKind.Basic) swingShot = Capture("swing");
            if (!powerStrikeShot && midSwing && p.Attack == AttackKind.PowerStrike) powerStrikeShot = Capture("power_strike");
            if (!slashBlastShot && midSwing && p.Attack == AttackKind.SlashBlast) slashBlastShot = Capture("slash_blast");
            if (!rageShot && p.IsAttacking && p.Attack == AttackKind.Rage && progress > 0.45f) rageShot = Capture("rage");

            int resting = 0, flying = 0;
            foreach (var drop in game.Drops)
            {
                if (drop.CanBePickedUp) resting++;
                if (drop.IsBeingPickedUp) flying++;
            }
            if (!dropsShot && resting >= 2) dropsShot = Capture("drops");
            if (!pickupShot && flying > 0) pickupShot = Capture("pickup");
        }

        /// <summary>One screenshot per frame; returns false so the caller retries next frame.</summary>
        bool Capture(string name)
        {
            if (Time.frameCount == lastCaptureFrame) return false;
            lastCaptureFrame = Time.frameCount;
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, name + ".png"));
            return true;
        }

        void Drive()
        {
            input.Clear();
            var p = game.Player;
            var m = p.Motor;

            if (!learned && time > 2f)
            {
                learned = true;
                game.LearnSkill(SkillId.PowerStrike);
                game.LearnSkill(SkillId.SlashBlast);
                game.LearnSkill(SkillId.Rage);
            }
            game.Hud.ShowInventory = time > 17.5f && time < 21f;
            game.Hud.ShowSkills = time > 34f && time < 37f;

            if (p.Stats.Hp < p.Stats.MaxHp * 0.4f) input.Set(GameAction.HpPotion, true);
            else if (p.Stats.Mp < 8) input.Set(GameAction.MpPotion, true);

            int rageCost = SkillDef.Rage.MpCost(p.Skills.Level(SkillId.Rage));
            if (learned && time > 2.5f && p.RageTimer <= 0f && p.Stats.Mp >= rageCost && m.State == MotorState.Ground)
            {
                input.Set(GameAction.Skill3, true);
                return;
            }

            if (time < 20f)
            {
                if (time > 3f && time < 3.1f) input.Set(GameAction.Jump, true);
                Hunt(p);
            }
            else if (time < 30f && !(m.State == MotorState.Ground && m.Position.y > 4f))
            {
                ClimbRope(m);
            }
            else
            {
                Hunt(p);
            }
        }

        /// <summary>Loots nearby drops when safe, otherwise fights the nearest monster on the same level.</summary>
        void Hunt(Player p)
        {
            var m = p.Motor;
            Mob target = null;
            float best = float.MaxValue;
            foreach (var mob in game.Mobs)
            {
                if (mob.IsDead || Mathf.Abs(mob.Position.y - m.Position.y) > 0.5f) continue;
                float dist = Mathf.Abs(mob.Position.x - m.Position.x);
                if (dist < best)
                {
                    best = dist;
                    target = mob;
                }
            }

            Drop loot = null;
            float lootDist = 4f;
            foreach (var drop in game.Drops)
            {
                if (!drop.CanBePickedUp || Mathf.Abs(drop.Position.y - m.Position.y) > 0.5f) continue;
                float dist = Mathf.Abs(drop.Position.x - m.Position.x);
                if (dist < lootDist)
                {
                    lootDist = dist;
                    loot = drop;
                }
            }
            if (loot != null && best > 2f)
            {
                float ddx = loot.Position.x - m.Position.x;
                if (Mathf.Abs(ddx) > 0.3f) input.Set(ddx > 0f ? GameAction.Right : GameAction.Left, true);
                else input.Set(GameAction.Pickup, true);
                return;
            }
            if (target == null) return;

            float dx = target.Position.x - m.Position.x;
            int dir = dx > 0f ? 1 : -1;
            if (Mathf.Abs(dx) > 1.2f || m.Facing != dir)
            {
                input.Set(dir > 0 ? GameAction.Right : GameAction.Left, true);
                return;
            }

            int inFront = 0;
            foreach (var mob in game.Mobs)
            {
                float ahead = (mob.Position.x - m.Position.x) * dir;
                if (!mob.IsDead && Mathf.Abs(mob.Position.y - m.Position.y) < 0.5f && ahead > -0.6f && ahead < 2.6f) inFront++;
            }
            int mp = p.Stats.Mp;
            int blastCost = SkillDef.SlashBlast.MpCost(p.Skills.Level(SkillId.SlashBlast));
            int strikeCost = SkillDef.PowerStrike.MpCost(p.Skills.Level(SkillId.PowerStrike));
            // Slash Blast for groups, otherwise alternate between the two attack skills while MP lasts.
            bool preferBlast = inFront >= 2 || Mathf.FloorToInt(time) % 2 == 0;
            if (learned && preferBlast && mp >= blastCost + 4) input.Set(GameAction.Skill2, true);
            else if (learned && mp >= strikeCost + 6) input.Set(GameAction.Skill1, true);
            else input.Set(GameAction.Attack, true);
        }

        void ClimbRope(PlayerMotor m)
        {
            if (m.State == MotorState.Climb || Mathf.Abs(m.Position.x - RopeX) < 0.3f)
            {
                input.Set(GameAction.Up, true);
                return;
            }
            input.Set(m.Position.x < RopeX ? GameAction.Right : GameAction.Left, true);
        }
    }
}
