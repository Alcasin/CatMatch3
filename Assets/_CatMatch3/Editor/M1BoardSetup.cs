using System;
using System.Collections.Generic;
using System.Linq;
using CatMatch3.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatMatch3.Editor
{
    public static class M1BoardSetup
    {
        private const string MenuPath = "CatMatch3/Setup/Apply M1 Board";
        private const string ScenePath = "Assets/_CatMatch3/Scenes/Gameplay.unity";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const string RootName = "M1 Board Prototype";
        private const string InputName = "M1 UI Input";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!CanApply()) return;
            var previousActive = SceneManager.GetActiveScene();
            Scene scene = default;
            bool openedHere = false;
            int undoGroup = -1;
            try
            {
                // Use persistent importer-created action references, never transient default actions.
                var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
                if (input == null) throw new InvalidOperationException($"Missing existing template input asset: {InputPath}");
                var actions = LoadUIReferences();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    M0FoundationSetup.Apply();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new InvalidOperationException("Gameplay is unavailable. See the M0 setup error in the Console.");

                scene = SceneManager.GetSceneByPath(ScenePath);
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                    throw new InvalidOperationException("Save or revert your unsaved Gameplay edits before applying M1 setup.");
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    openedHere = true;
                }
                if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                    throw new InvalidOperationException("Gameplay must be a valid, loaded Editor scene.");

                var roots = scene.GetRootGameObjects();
                if (!roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                    .Any(c => c.enabled && c.gameObject.activeInHierarchy && c.orthographic))
                    throw new InvalidOperationException("Gameplay needs its enabled M0 orthographic camera. No camera was replaced.");

                var boards = roots.SelectMany(r => r.GetComponentsInChildren<BoardPrototype>(true)).ToArray();
                if (boards.Length > 1) throw new InvalidOperationException("Multiple board prototypes found; resolve them before setup.");
                var root = boards.Length == 1 ? boards[0].gameObject : null;
                if (root == null && roots.Any(r => r.name == RootName))
                    throw new InvalidOperationException($"'{RootName}' exists without its BoardPrototype component. Content was preserved.");
                if (root != null) ValidateExistingRoot(root);

                var eventSystems = roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).ToArray();
                if (eventSystems.Length > 1) throw new InvalidOperationException("Multiple EventSystems in Gameplay; resolve them before setup.");
                var existingInput = eventSystems.SingleOrDefault();
                bool ownedInput = existingInput != null && root != null &&
                    existingInput.transform.IsChildOf(root.transform) && existingInput.name == InputName;
                if (existingInput != null) ValidateExistingInput(existingInput, ownedInput);
                if (root != null && existingInput == null && root.transform.Find(InputName) != null)
                    throw new InvalidOperationException("The reserved M1 input object has conflicting content. Nothing was removed.");

                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply CatMatch3 M1 Board");
                if (root == null)
                    root = CreateObject(scene, RootName, typeof(RectTransform));
                var canvas = root.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = Undo.AddComponent<Canvas>(root);
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 10;
                }
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler == null)
                {
                    scaler = Undo.AddComponent<CanvasScaler>(root);
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(900f, 1600f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0f;
                }
                EnsureComponent<GraphicRaycaster>(root);
                var view = EnsureComponent<BoardView>(root);
                var prototype = EnsureComponent<BoardPrototype>(root);
                var serialized = new SerializedObject(prototype);
                serialized.FindProperty("view").objectReferenceValue = view;
                serialized.ApplyModifiedProperties();

                if (existingInput == null)
                {
                    var inputObject = CreateObject(scene, InputName);
                    Undo.SetTransformParent(inputObject.transform, root.transform, "Parent M1 input");
                    existingInput = Undo.AddComponent<EventSystem>(inputObject);
                    ownedInput = true;
                }
                if (ownedInput)
                {
                    var module = EnsureComponent<InputSystemUIInputModule>(existingInput.gameObject);
                    Undo.RecordObject(module, "Wire M1 UI actions");
                    module.actionsAsset = input;
                    module.point = actions["Point"];
                    module.leftClick = actions["Click"];
                    module.rightClick = actions["RightClick"];
                    module.middleClick = actions["MiddleClick"];
                    module.scrollWheel = actions["ScrollWheel"];
                    module.move = actions["Navigate"];
                    module.submit = actions["Submit"];
                    module.cancel = actions["Cancel"];
                    module.trackedDevicePosition = actions["TrackedDevicePosition"];
                    module.trackedDeviceOrientation = actions["TrackedDeviceOrientation"];
                    module.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
                    EditorUtility.SetDirty(module);
                }

                EditorUtility.SetDirty(prototype);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unity could not save Gameplay.");
                Undo.CollapseUndoOperations(undoGroup);
                undoGroup = -1;
                Debug.Log("CatMatch3 M1 applied. Open Gameplay alone and enter Play Mode in a 9:16 Game view. " +
                    "The board UI and resolution presentation are generated at runtime. " +
                    "The M0 camera, build list, input asset and portrait settings were preserved.");
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("CatMatch3 M1 setup did not finish: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                // Never activate the scene being edited; scene-specific creation is sufficient.
                try
                {
                    if (openedHere && scene.IsValid() && !EditorSceneManager.CloseScene(scene, true))
                        Debug.LogError("CatMatch3 M1: could not close the scene opened by setup.");
                }
                finally
                {
                    if (previousActive.IsValid() && previousActive.isLoaded &&
                        !EditorSceneManager.IsPreviewScene(previousActive) && SceneManager.GetActiveScene() != previousActive &&
                        !SceneManager.SetActiveScene(previousActive))
                        Debug.LogError("CatMatch3 M1: could not restore the previously active scene.");
                }
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanApply()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        private static GameObject CreateObject(Scene scene, string name, params Type[] components)
        {
            var result = ObjectFactory.CreateGameObject(scene, HideFlags.None, name, components);
            Undo.RegisterCreatedObjectUndo(result, "Create " + name);
            // Ignore any user default-parent preference; this helper creates scene roots.
            result.transform.SetParent(null, false);
            return result;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            return target.GetComponent<T>() ?? Undo.AddComponent<T>(target);
        }

        private static void ValidateExistingRoot(GameObject root)
        {
            if (root.transform.parent != null || root.GetComponent<RectTransform>() == null || !root.activeSelf ||
                !root.GetComponent<BoardPrototype>().enabled)
                throw new InvalidOperationException("Existing board must be an enabled scene root with a RectTransform.");
            var canvas = root.GetComponent<Canvas>();
            if (canvas != null && (!canvas.enabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay))
                throw new InvalidOperationException("Existing board Canvas has conflicting settings; it was not overwritten.");
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null && (!scaler.enabled || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.referenceResolution != new Vector2(900f, 1600f) ||
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight ||
                scaler.matchWidthOrHeight != 0f))
                throw new InvalidOperationException("Existing board CanvasScaler has conflicting settings; it was not overwritten.");
            var raycaster = root.GetComponent<GraphicRaycaster>();
            if (raycaster != null && !raycaster.enabled)
                throw new InvalidOperationException("Existing board GraphicRaycaster is disabled; it was not overwritten.");
            var view = root.GetComponent<BoardView>();
            if (view != null && !view.enabled)
                throw new InvalidOperationException("Existing BoardView is disabled; it was not overwritten.");
            var reference = new SerializedObject(root.GetComponent<BoardPrototype>()).FindProperty("view").objectReferenceValue;
            if (reference != null && reference != view)
                throw new InvalidOperationException("Existing board points to another BoardView; reference was preserved.");
        }

        private static void ValidateExistingInput(EventSystem system, bool owned)
        {
            if (!system.enabled || !system.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The existing Gameplay EventSystem is inactive; it was preserved.");
            var module = system.GetComponent<InputSystemUIInputModule>();
            if ((!owned && (module == null || module.point == null || module.leftClick == null)) ||
                (module != null && !module.enabled) ||
                system.GetComponents<BaseInputModule>().Any(m => m.enabled && !(m is InputSystemUIInputModule)))
                throw new InvalidOperationException("The existing EventSystem has incompatible input wiring; it was preserved.");
        }

        private static Dictionary<string, InputActionReference> LoadUIReferences()
        {
            var references = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>()
                .Where(r => r.action != null && r.action.actionMap.name == "UI").ToArray();
            string[] names = { "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel", "Navigate",
                "Submit", "Cancel", "TrackedDevicePosition", "TrackedDeviceOrientation" };
            var result = new Dictionary<string, InputActionReference>();
            foreach (var name in names)
            {
                var reference = references.FirstOrDefault(r => r.action.name == name);
                if (reference == null) throw new InvalidOperationException($"Template input asset is missing UI/{name}.");
                result.Add(name, reference);
            }
            return result;
        }
    }
}
