using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatMatch3.Editor
{
    public static class M0FoundationSetup
    {
        private const string MenuPath = "CatMatch3/Setup/Apply M0 Foundation";
        private const string ScenePath = "Assets/_CatMatch3/Scenes/Gameplay.unity";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogError("CatMatch3 M0: exit Play Mode and wait for compilation before applying setup.");
                return;
            }

            try
            {
                EnsureSceneFolder();
                bool created = EnsureGameplayScene();
                EnsureBuildEntry();

                // This supported mobile setting applies to Android without switching targets.
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                AssetDatabase.SaveAssets();

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                EditorGUIUtility.PingObject(sceneAsset);
                Debug.Log($"CatMatch3 M0 applied. Gameplay scene {(created ? "created" : "preserved")}: " +
                    $"{ScenePath}. Build entry enabled; mobile orientation set to Portrait. " +
                    "Open Gameplay on its own to check Play Mode; use a 9:16 Game view. " +
                    "Existing scenes and build entry order were preserved. Build target was not changed.");
            }
            catch (Exception exception)
            {
                Debug.LogError("CatMatch3 M0 setup could not finish. Any completed steps can safely be rerun. " +
                    exception.Message);
                Debug.LogException(exception);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanApply()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        private static void EnsureSceneFolder()
        {
            const string root = "Assets/_CatMatch3";
            if (!AssetDatabase.IsValidFolder(root))
                throw new InvalidOperationException("The _CatMatch3 folder is missing or has not finished importing.");

            if (!AssetDatabase.IsValidFolder(root + "/Scenes"))
            {
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(root, "Scenes")))
                    throw new IOException("Could not create the CatMatch3 Scenes folder.");
            }
        }

        private static bool EnsureGameplayScene()
        {
            // An existing scene belongs to the user. Never rebuild or resave it on repeat runs.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return false;

            // A previous attempt may have saved the scene before asset import completed.
            if (File.Exists(ScenePath))
            {
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                    return false;
            }

            if (File.Exists(ScenePath) || Directory.Exists(ScenePath) || File.Exists(ScenePath + ".meta"))
                throw new IOException($"{ScenePath} has an unimported file, conflicting folder or orphaned metadata. " +
                    "Resolve the conflict in the Project window before rerunning; nothing was overwritten.");

            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                    throw new InvalidOperationException("Unity did not create a valid, loaded Editor scene for Gameplay.");

                // Create directly in this scene; changing the active scene is unnecessary.
                var cameraObject = ObjectFactory.CreateGameObject(scene, HideFlags.None,
                    "Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);

                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.15f, 0.20f, 1f);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException($"Unity could not save {ScenePath}. Check project write access.");
            }
            finally
            {
                // Close only our scene, even if creation/configuration/saving failed.
                // Restoration must not prevent cleanup if an activation API throws.
                try
                {
                    if (scene.IsValid() && !EditorSceneManager.CloseScene(scene, true))
                        throw new IOException("Unity could not close the temporary Gameplay scene.");
                }
                finally
                {
                    if (previousActiveScene.IsValid() && previousActiveScene.isLoaded &&
                        !EditorSceneManager.IsPreviewScene(previousActiveScene) &&
                        SceneManager.GetActiveScene() != previousActiveScene &&
                        !SceneManager.SetActiveScene(previousActiveScene))
                        throw new InvalidOperationException("Unity could not restore the previously active scene.");
                }
            }

            return true;
        }

        private static void EnsureBuildEntry()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            bool changed = false;
            foreach (var entry in scenes)
            {
                if (entry.path != ScenePath)
                    continue;

                found = true;
                if (!entry.enabled)
                {
                    entry.enabled = true;
                    changed = true;
                }
            }

            if (!found)
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                changed = true;
            }

            if (changed)
                EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
