using System;
using System.IO;
using System.Linq;
using CatMatch3.Core;
using CatMatch3.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatMatch3.Editor
{
    public static class M4PuzzleSetup
    {
        private const string Menu = "CatMatch3/Setup/Apply M4 Puzzle Revision";

        [MenuItem(Menu)]
        public static void Apply()
        {
            if (!CanApply()) return;
            int undoGroup = -1;
            try
            {
                var scene = SceneManager.GetSceneByPath("Assets/_CatMatch3/Scenes/Gameplay.unity");
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                    throw new InvalidOperationException("Save or revert unsaved Gameplay edits before puzzle setup.");
                var assets = new LevelDefinition[2];
                var update = new bool[2];
                // Preflight BOTH assets and proofs before changing either. Partially upgraded pairs are valid.
                for (int i = 0; i < 2; i++)
                {
                    string path = "Assets/_CatMatch3/Data/Level" + (i + 1) + ".asset";
                    var existing = AssetDatabase.LoadMainAssetAtPath(path);
                    if (existing == null && File.Exists(path))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                        existing = AssetDatabase.LoadMainAssetAtPath(path);
                    }
                    if (existing == null && (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta")))
                        throw new InvalidOperationException(path + " has unimported/conflicting content or orphaned metadata; preserved.");
                    if (existing != null)
                    {
                        assets[i] = existing as LevelDefinition;
                        if (assets[i] == null || EditorUtility.IsDirty(assets[i]))
                            throw new InvalidOperationException(path + " has a conflicting type or unsaved changes; preserved.");
                        if (!assets[i].MatchesConfiguration(AuthoredLevels.Create(i + 1)))
                        {
                            if (!assets[i].MatchesConfiguration(AuthoredLevels.CreatePreviousM4(i + 1)))
                                throw new InvalidOperationException(path + " has custom content outside the known M4 baseline; preserved. No level was changed.");
                            update[i] = true;
                        }
                    }
                    var proof = PuzzleVerifier.Verify(AuthoredLevels.Create(i + 1));
                    if (proof.Status != PuzzleVerificationStatus.Unique || !proof.Exhaustive)
                        throw new InvalidOperationException("Level " + (i + 1) + " uniqueness not proved: " + proof.Status + ". " + proof.Limitation);
                    LogProof(i + 1, proof);
                }
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Upgrade known M4 levels to verified puzzles");
                for (int i = 0; i < 2; i++)
                {
                    if (!update[i]) continue;
                    Undo.RecordObject(assets[i], "Upgrade Level " + (i + 1));
                    assets[i].InitializeDefaults(i + 1);
                    assets[i].ToConfiguration();
                    EditorUtility.SetDirty(assets[i]);
                }
                // Supported asset APIs retain GUIDs and therefore existing scene references.
                for (int i = 0; i < 2; i++) if (update[i]) AssetDatabase.SaveAssetIfDirty(assets[i]);
                Undo.CollapseUndoOperations(undoGroup);
                undoGroup = -1;
                // Completed migrations remain valid if later infrastructure setup fails; reruns reuse them.
                M4GameplaySetup.ApplyOrThrow();
                Debug.Log("CatMatch3 M4 Puzzle Revision applied. Two exhaustive unique proofs passed; known generated assets upgraded, " +
                    "current puzzles reused. Production disables reshuffling. Open Gameplay alone and enter Play Mode.");
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("CatMatch3 puzzle setup did not finish. Safe completed steps may be rerun. " + exception.Message);
                Debug.LogException(exception);
            }
        }

        [MenuItem(Menu, true)]
        private static bool CanApply() { return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling; }

        [MenuItem("CatMatch3/Diagnostics/Verify Authored Puzzle Assets")]
        public static void VerifyAssets()
        {
            if (!CanApply()) return;
            try
            {
                for (int i = 1; i <= 2; i++)
                {
                    string path = "Assets/_CatMatch3/Data/Level" + i + ".asset";
                    var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                    if (asset == null || EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Missing or unsaved " + path);
                    LogProof(i, PuzzleVerifier.Verify(asset.ToConfiguration()));
                }
            }
            catch (Exception exception) { Debug.LogError("Puzzle verification: " + exception.Message); }
        }

        private static void LogProof(int level, PuzzleVerificationResult result)
        {
            Debug.Log($"Puzzle {level}: {result.Status}; exhaustive={result.Exhaustive}; routes={result.WinningRoutes}; " +
                $"states={result.States}; transitions={result.Transitions}; replayed moves={result.ReplayedMoves}; " +
                $"resolution errors={result.ResolutionErrors}; ms={result.ElapsedMilliseconds}. " +
                string.Join(" | ", result.Routes.Select(r => string.Join("; ", r))) + " " + result.Limitation);
        }
    }
}
