using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    public enum GameAction { Left, Right, Up, Down, Jump, Attack }

    /// <summary>
    /// Source of player intent. The keyboard drives real play; tests and the autopilot script it.
    /// Everything is "held" state: like MapleStory, holding jump or attack repeats it.
    /// </summary>
    public interface IGameInput
    {
        bool Held(GameAction action);
    }

    public sealed class KeyboardInput : IGameInput
    {
        // Indexed by GameAction.
        static readonly KeyCode[][] Bindings =
        {
            new[] { KeyCode.LeftArrow, KeyCode.A },
            new[] { KeyCode.RightArrow, KeyCode.D },
            new[] { KeyCode.UpArrow, KeyCode.W },
            new[] { KeyCode.DownArrow, KeyCode.S },
            new[] { KeyCode.Space, KeyCode.LeftAlt, KeyCode.RightAlt },
            new[] { KeyCode.LeftControl, KeyCode.RightControl, KeyCode.X },
        };

        public bool Held(GameAction action)
        {
            foreach (var key in Bindings[(int)action])
                if (Input.GetKey(key)) return true;
            return false;
        }

        public static bool HelpTogglePressed() => Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1);
    }

    public sealed class ScriptedInput : IGameInput
    {
        readonly HashSet<GameAction> held = new HashSet<GameAction>();

        public bool Held(GameAction action) => held.Contains(action);

        public void Set(GameAction action, bool down)
        {
            if (down) held.Add(action);
            else held.Remove(action);
        }

        public void Clear() => held.Clear();
    }
}
