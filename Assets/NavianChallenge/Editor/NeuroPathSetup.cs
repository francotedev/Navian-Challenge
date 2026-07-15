using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NavianChallenge.EditorTools
{
    /// <summary>
    /// Wires NeuroPath into the base scene by adding a single <see cref="NeuroPathBootstrap"/>
    /// component (which builds the whole app at runtime). Idempotent — safe to run repeatedly.
    /// Available from the menu, or in batch via <see cref="SetupBatch"/> (which also serves as a
    /// compile gate: -executeMethod only runs after the project compiles cleanly).
    /// </summary>
    public static class NeuroPathSetup
    {
        const string ScenePath = "Assets/NavianChallenge/Scenes/NavianChallenge_Main.unity";
        const string HostName = "NeuroPath";

        [MenuItem("Navian/Setup NeuroPath (add to scene)")]
        public static void SetupMenu()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AddBootstrap();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NeuroPath] Setup complete on " + ScenePath);
        }

        // Entry point for -executeMethod (batch): compile-gated, wires the scene, saves, exits.
        public static void SetupBatch()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                AddBootstrap();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("###NEUROPATH_SETUP_OK###");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("###NEUROPATH_SETUP_FAIL### " + e);
                EditorApplication.Exit(1);
            }
        }

        static void AddBootstrap()
        {
            var existing = Object.FindFirstObjectByType<NeuroPathBootstrap>();
            if (existing != null)
            {
                Debug.Log("[NeuroPath] Bootstrap already present on '" + existing.gameObject.name + "'.");
                return;
            }

            var host = GameObject.Find(HostName);
            if (host == null) host = new GameObject(HostName);
            host.AddComponent<NeuroPathBootstrap>();
            Debug.Log("[NeuroPath] Added NeuroPathBootstrap to '" + host.name + "'.");
        }
    }
}
