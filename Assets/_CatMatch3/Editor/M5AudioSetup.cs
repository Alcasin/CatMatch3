using System;
using System.IO;
using System.Linq;
using CatMatch3.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatMatch3.Editor
{
    public static class M5AudioSetup
    {
        private const string Menu = "CatMatch3/Setup/Apply M5-D2 Audio";
        private const string ScenePath = "Assets/_CatMatch3/Scenes/Gameplay.unity";
        private static readonly string[] Names = { "Select", "Invalid", "Match", "Striped", "Combo", "Win", "Fail" };
        private static readonly string[] Fields = { "selectClip", "invalidClip", "matchClip", "stripedClip", "comboClip", "winClip", "failClip" };

        [MenuItem(Menu)]
        public static void Apply()
        {
            if (!CanApply()) return;
            var previous = SceneManager.GetActiveScene();
            Scene scene = default;
            bool openedHere = false;
            int undoGroup = -1;
            try
            {
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                    throw new InvalidOperationException("Save or revert unsaved Gameplay edits before audio setup.");
                var clips = new AudioClip[Names.Length];
                for (int i = 0; i < clips.Length; i++)
                {
                    string path = "Assets/_CatMatch3/Art/Audio/SFX/SFX_" + Names[i] + ".wav";
                    if (!File.Exists(path)) throw new FileNotFoundException("Generate the original cues with Tools/generate_sfx.py first.", path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) throw new InvalidOperationException("Audio import not ready: " + path);
                    var settings = importer.defaultSampleSettings;
                    if (settings.loadType != AudioClipLoadType.DecompressOnLoad || settings.compressionFormat != AudioCompressionFormat.PCM ||
                        settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate || !settings.preloadAudioData ||
                        importer.forceToMono || importer.loadInBackground)
                    {
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        settings.compressionFormat = AudioCompressionFormat.PCM;
                        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                        settings.preloadAudioData = true;
                        importer.defaultSampleSettings = settings;
                        importer.forceToMono = false; // Generated files are mono already; preserve their authored levels.
                        importer.loadInBackground = false;
                        importer.SaveAndReimport();
                    }
                    clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clips[i] == null || !EditorUtility.IsPersistent(clips[i]) || clips[i].channels != 1 || clips[i].length > 1f)
                        throw new InvalidOperationException("Expected an imported, short mono AudioClip at " + path);
                }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new InvalidOperationException("Existing Gameplay scene is missing.");
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    openedHere = true;
                }
                if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                    throw new InvalidOperationException("Gameplay must be a valid, loaded Editor scene.");
                var roots = scene.GetRootGameObjects();
                var views = roots.SelectMany(r => r.GetComponentsInChildren<BoardView>(true)).ToArray();
                var audio = roots.SelectMany(r => r.GetComponentsInChildren<BoardAudio>(true)).ToArray();
                if (views.Length != 1 || audio.Length > 1 || (audio.Length == 1 && audio[0].gameObject != views[0].gameObject))
                    throw new InvalidOperationException("Expected one BoardView and at most one BoardAudio on that object; custom content preserved.");
                var view = new SerializedObject(views[0]);
                var reference = view.FindProperty("audioFeedback");
                var existing = audio.Length == 1 ? audio[0] : null;
                if (reference == null || (reference.objectReferenceValue != null && reference.objectReferenceValue != existing))
                    throw new InvalidOperationException("Conflicting or missing BoardView audio binding; preserved.");
                bool changed = existing == null || reference.objectReferenceValue == null;
                if (existing != null)
                {
                    var serialized = new SerializedObject(existing);
                    for (int i = 0; i < clips.Length; i++)
                    {
                        var slot = serialized.FindProperty(Fields[i]);
                        if (slot == null || (slot.objectReferenceValue != null && slot.objectReferenceValue != clips[i]))
                            throw new InvalidOperationException("Custom or missing audio slot " + Fields[i] + "; preserved.");
                        changed |= slot.objectReferenceValue != clips[i];
                    }
                }
                if (changed)
                {
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Bind CatMatch3 M5-D2 audio");
                    var target = existing != null ? existing : Undo.AddComponent<BoardAudio>(views[0].gameObject);
                    Undo.RecordObject(target, "Bind seven sound clips");
                    var serialized = new SerializedObject(target);
                    for (int i = 0; i < clips.Length; i++) serialized.FindProperty(Fields[i]).objectReferenceValue = clips[i];
                    serialized.ApplyModifiedProperties();
                    Undo.RecordObject(views[0], "Bind audio presentation");
                    reference.objectReferenceValue = target;
                    view.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity could not save Gameplay audio references.");
                    Undo.CollapseUndoOperations(undoGroup);
                    undoGroup = -1;
                }
                Debug.Log("CatMatch3 M5-D2: seven PCM cues are saved in Gameplay. Setup is safe to rerun. " +
                    "The existing listener is reused; two 2D sources are created once in Play Mode. Use SFX ON/OFF beside Restart.");
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("CatMatch3 audio setup did not finish: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                try
                {
                    if (openedHere && scene.IsValid() && !EditorSceneManager.CloseScene(scene, true))
                        Debug.LogError("CatMatch3 audio setup could not close Gameplay opened here.");
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded && !EditorSceneManager.IsPreviewScene(previous) &&
                        SceneManager.GetActiveScene() != previous && !SceneManager.SetActiveScene(previous))
                        Debug.LogError("CatMatch3 audio setup could not restore the active scene.");
                }
            }
        }

        [MenuItem(Menu, true)]
        private static bool CanApply()
        { return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling; }
    }
}
