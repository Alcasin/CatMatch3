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
    public static class M5CatSpritesSetup
    {
        private const string Menu = "CatMatch3/Setup/Apply M5-A Cat Sprites";
        private const string ScenePath = "Assets/_CatMatch3/Scenes/Gameplay.unity";
        private static readonly string[] CatNames = { "Orange", "Black", "White", "Gray", "Siamese" };
        private static readonly string[] Properties =
            { "orangeCatSprite", "blackCatSprite", "whiteCatSprite", "grayCatSprite", "siameseCatSprite" };

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
                // Inspect the existing imports; never change PNGs or their importer settings.
                var sprites = new Sprite[CatNames.Length];
                for (int i = 0; i < CatNames.Length; i++)
                {
                    string path = "Assets/_CatMatch3/Art/Sprites/Cats/Cat_" + CatNames[i] + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                        importer.spriteImportMode != SpriteImportMode.Single || !importer.alphaIsTransparency)
                        throw new InvalidOperationException(path + " must already be imported as a transparent Single Sprite.");
                    sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprites[i] == null || !EditorUtility.IsPersistent(sprites[i]))
                        throw new InvalidOperationException("Missing persistent sprite at " + path + ". Wait for import and rerun setup.");
                }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new InvalidOperationException("Existing Gameplay scene is missing. M5-A does not recreate gameplay infrastructure.");
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                    throw new InvalidOperationException("Save or revert unsaved Gameplay edits before binding cat sprites.");
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    openedHere = true;
                }
                if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                    throw new InvalidOperationException("Gameplay must be a valid, loaded Editor scene.");
                var views = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoardView>(true)).ToArray();
                var controllers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoardPrototype>(true)).ToArray();
                if (views.Length != 1 || controllers.Length != 1 ||
                    new SerializedObject(controllers[0]).FindProperty("view").objectReferenceValue != views[0])
                    throw new InvalidOperationException("Gameplay requires its existing single BoardView wired to its single BoardPrototype.");
                var view = views[0];
                var serialized = new SerializedObject(view);
                bool changed = false;
                // Preflight every slot before modifying any. Preserve unexpected custom bindings.
                for (int i = 0; i < sprites.Length; i++)
                {
                    var slot = serialized.FindProperty(Properties[i]);
                    if (slot == null) throw new InvalidOperationException("Missing sprite field " + Properties[i]);
                    if (slot.objectReferenceValue != null && slot.objectReferenceValue != sprites[i])
                        throw new InvalidOperationException(Properties[i] + " has a custom binding; preserved. Resolve it before rerunning.");
                    changed |= slot.objectReferenceValue != sprites[i];
                }
                if (changed)
                {
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Bind CatMatch3 cat sprites");
                    Undo.RecordObject(view, "Bind five cat sprites");
                    for (int i = 0; i < sprites.Length; i++)
                        serialized.FindProperty(Properties[i]).objectReferenceValue = sprites[i];
                    serialized.ApplyModifiedProperties();
                    view.ValidateCatSprites();
                    EditorUtility.SetDirty(view);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity could not save Gameplay sprite references.");
                    Undo.CollapseUndoOperations(undoGroup);
                    undoGroup = -1;
                }
                else view.ValidateCatSprites();
                Debug.Log("CatMatch3 M5-A: all five cat sprites are persistently bound in Gameplay. " +
                    "Open Gameplay in 9:16 and enter Play Mode. Setup is safe to rerun.");
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("CatMatch3 M5-A sprite setup did not finish: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                try
                {
                    if (openedHere && scene.IsValid() && !EditorSceneManager.CloseScene(scene, true))
                        Debug.LogError("CatMatch3 M5-A: could not close Gameplay opened by setup.");
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded && !EditorSceneManager.IsPreviewScene(previous) &&
                        SceneManager.GetActiveScene() != previous && !SceneManager.SetActiveScene(previous))
                        Debug.LogError("CatMatch3 M5-A: could not restore the previously active scene.");
                }
            }
        }

        [MenuItem(Menu, true)]
        private static bool CanApply()
        { return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling; }
    }
}
