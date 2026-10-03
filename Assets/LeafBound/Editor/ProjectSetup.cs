using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LeafBound.EditorTools
{
    /// <summary>Menu commands (also run from the command line with -executeMethod) for setting up and building.</summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/LeafBound/Scenes/Main.unity";
        const string BuildPath = "Builds/Windows/LeafBound.exe";
        const string ArtPreviewDir = "ArtPreview";

        /// <summary>Writes the one scene the game needs: an empty scene holding the Game component.</summary>
        [MenuItem("LeafBound/Recreate Main Scene")]
        public static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("LeafBound").AddComponent<Game>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("LeafBound: wrote " + ScenePath);
        }

        [MenuItem("LeafBound/Build Windows Player")]
        public static void BuildWindows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"LeafBound: build {summary.result} with {summary.totalErrors} errors, {summary.totalWarnings} warnings -> {BuildPath}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Saves every generated sprite, enlarged, to ArtPreview/ for reviewing the pixel art.</summary>
        [MenuItem("LeafBound/Export Art Preview")]
        public static void ExportArtPreview()
        {
            var art = new ArtLibrary();
            try
            {
                art.Platform(6f, 0.75f, false, 1);
                art.Platform(8f, 3f, true, 2);
                art.Rope(3f);
                art.Hills(12f, 6f, ArtLibrary.Rgb(0x7cc47f), ArtLibrary.Rgb(0x9fdc92), 3, 1f);

                Directory.CreateDirectory(ArtPreviewDir);
                var background = new Color32(96, 104, 120, 255);
                int index = 0;
                foreach (var sprite in art.AllSprites())
                {
                    var source = sprite.texture;
                    int k = Mathf.Clamp(256 / Mathf.Max(source.width, source.height), 1, 12);
                    var src = source.GetPixels32();
                    int w = source.width * k, h = source.height * k;
                    var dst = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            var c = src[(y / k) * source.width + x / k];
                            dst[y * w + x] = c.a == 0 ? background : Color32.Lerp(background, c, c.a / 255f);
                        }
                    }
                    var scaled = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    scaled.SetPixels32(dst);
                    scaled.Apply(false);
                    File.WriteAllBytes(Path.Combine(ArtPreviewDir, $"{index:00}_{sprite.name}.png"), scaled.EncodeToPNG());
                    Object.DestroyImmediate(scaled);
                    index++;
                }
                Debug.Log($"LeafBound: exported {index} sprites to {Path.GetFullPath(ArtPreviewDir)}");
            }
            finally
            {
                art.Dispose();
            }
        }
    }

    /// <summary>Opens the game scene the first time the project is opened, instead of an empty untitled scene.</summary>
    [InitializeOnLoad]
    static class OpenMainSceneOnStartup
    {
        const string SessionKey = "LeafBound.OpenedMainScene";

        static OpenMainSceneOnStartup()
        {
            EditorApplication.delayCall += Open;
        }

        static void Open()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty || !File.Exists(ProjectSetup.ScenePath)) return;
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
        }
    }
}
