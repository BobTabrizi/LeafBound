using System;
using System.IO;
using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// Plays the game by itself when the player is launched with
    /// <c>-leafbound-autopilot &lt;folder&gt;</c>: hunts on the ground, climbs a rope, hunts again,
    /// saves screenshots to the folder and logs its state, then quits. Used to smoke-test builds.
    /// </summary>
    public sealed class AutoPilot
    {
        public const string Flag = "-leafbound-autopilot";
        const float RopeX = 27f;

        static readonly float[] ShotTimes = { 1.5f, 5f, 10f, 15f, 19f, 23f, 27f, 32f, 37f };

        readonly Game game;
        readonly ScriptedInput input = new ScriptedInput();
        readonly string outputDir;
        float time, nextLog;
        int shotIndex;
        float quitAt = -1f;
        int lastLevel = 1;
        float levelShotAt = -1f;
        bool climbShotTaken, swingShotTaken;

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
                var m = game.Player.Motor;
                var s = game.Player.Stats;
                int alive = 0;
                foreach (var mob in game.Mobs)
                    if (!mob.IsDead) alive++;
                Debug.Log($"[autopilot] t={time:0.0} pos=({m.Position.x:0.00},{m.Position.y:0.00}) state={m.State} " +
                          $"lv={s.Level} exp={s.Exp}/{s.ExpNeeded} hp={s.Hp}/{s.MaxHp} mobs={alive} fps={1f / Mathf.Max(dt, 1e-4f):0}");
            }

            CaptureEvents();
            if (shotIndex < ShotTimes.Length && time >= ShotTimes[shotIndex])
            {
                Capture($"shot_{shotIndex:00}");
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
            var player = game.Player;
            if (player.Stats.Level > lastLevel)
            {
                lastLevel = player.Stats.Level;
                levelShotAt = time + 0.3f;
            }
            if (levelShotAt > 0f && time >= levelShotAt)
            {
                levelShotAt = -1f;
                Capture($"levelup_{lastLevel}");
            }
            if (!climbShotTaken && player.Motor.State == MotorState.Climb && player.Motor.Position.y > 2f)
            {
                climbShotTaken = true;
                Capture("climb");
            }
            if (!swingShotTaken && player.AttackProgress > 0.42f && player.AttackProgress < 0.5f)
            {
                swingShotTaken = true;
                Capture("swing");
            }
        }

        void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, name + ".png"));

        void Drive()
        {
            input.Clear();
            var m = game.Player.Motor;
            if (time < 20f)
            {
                if (time > 3f && time < 3.1f) input.Set(GameAction.Jump, true);
                Hunt(m);
            }
            else if (time < 30f && !(m.State == MotorState.Ground && m.Position.y > 4f))
            {
                ClimbRope(m);
            }
            else
            {
                Hunt(m);
            }
        }

        /// <summary>Walks to the nearest monster on the same level and attacks it.</summary>
        void Hunt(PlayerMotor m)
        {
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
            if (target == null) return;

            float dx = target.Position.x - m.Position.x;
            int dir = dx > 0f ? 1 : -1;
            if (Mathf.Abs(dx) > 1.2f || m.Facing != dir) input.Set(dir > 0 ? GameAction.Right : GameAction.Left, true);
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
