# LeafBound

A small 2D side-scrolling action RPG in the style of MapleStory, built in Unity 6.3 LTS
(6000.3.25f1). Everything is drawn and synthesized in code. The project has no image or
audio assets, and all names and art are original.

This first version is one map, **Mossy Meadow**:

- One-way platforms you can jump up through, plus down + jump to drop through them
- Ropes to climb, and you can jump off them sideways
- Sprout Slimes (Lv.1) and Capshrooms (Lv.4) that wander, chase you after being hit, and respawn
- Sword combat with damage numbers, critical hits and knockback; touching a monster hurts you
- EXP, levels and stat growth; fainting revives you at the start of the map
- Classic bottom status bar (level, HP, EXP %), minimap, message log, name tags, mob HP bars

## Controls

| Key | Action |
| --- | --- |
| Arrows / WASD | Move, climb ropes |
| Space / Alt | Jump (hold to keep jumping) |
| Down + Jump | Drop through a platform |
| Ctrl / X | Attack (hold to keep swinging) |
| H / F1 | Show or hide the controls panel |
| Alt+Enter / Alt+F4 | Toggle fullscreen / quit (built game) |

## Playing

Open the folder in Unity Hub (**Add > Add project from disk**) with editor 6000.3.25f1. The first
time the project opens it loads `Assets/LeafBound/Scenes/Main.unity`; press Play.

To build a standalone game, use **LeafBound > Build Windows Player**. The output goes to `Builds/Windows/LeafBound.exe`.

## Project layout

```
Assets/LeafBound/
  Scripts/          Game code (assembly LeafBound)
    Game.cs           Root MonoBehaviour: builds the world, runs the tick order, camera
    PlayerMotor.cs    Movement physics over footholds and ropes (pure logic, unit tested)
    PlayerStats.cs    Level, EXP, HP, damage (pure logic)
    Mob.cs            Monster definitions and AI
    MapData.cs        Map layout and collision queries; Mossy Meadow is defined here
    MapView.cs        Scenery: sky, parallax hills, clouds, platforms, ropes, decor
    Player.cs         Player state and the part-based animated character view
    ArtLibrary.cs     All sprites, drawn with PixelCanvas
    Hud.cs            IMGUI heads-up display
    Effects.cs        Damage numbers and particles
    Sfx.cs            Synthesized sound effects
    AutoPilot.cs      Self-playing smoke test for builds (see below)
  Editor/           Menu commands: recreate scene, build, export art preview
  Tests/            Edit-mode and play-mode tests (Unity Test Framework)
  Scenes/Main.unity The only scene: one GameObject with the Game component
```

The simulation runs in plain C# classes, and MonoBehaviours only render it. That's why
movement, combat and AI can be tested without loading a scene.

## Tests

Run them from **Window > General > Test Runner**, or headless:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode.xml
```

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode.xml
```

A built player can also play itself and save screenshots:

```bash
Builds/Windows/LeafBound.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -leafbound-autopilot shots
```

## Notes

- Input uses Unity's legacy Input Manager, which is this project's setting. If you switch
  **Active Input Handling** to "Input System Package (New)" only, the keyboard code in
  `GameInput.cs` must be ported first.
- The pixel art uses 16 pixels per unit, and the camera picks a whole-number scale for the window
  height, so sprites stay crisp.
