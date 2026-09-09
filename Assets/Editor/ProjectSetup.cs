#if UNITY_EDITOR
using System.IO;
using SignalHaul;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalHaul.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string RootName = "SIGNAL_HAUL";

        static ProjectSetup()
        {
            EditorApplication.delayCall += EnsureScene;
        }

        private static void EnsureScene()
        {
            if (Application.isPlaying) return;

            Directory.CreateDirectory("Assets/Scenes");

            Scene scene;
            if (!File.Exists(ScenePath))
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else if (SceneManager.GetActiveScene().path == ScenePath)
            {
                scene = SceneManager.GetActiveScene();
            }
            else
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            bool changed = false;
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                changed = true;
            }

            if (root.GetComponent<GameManager>() == null)
            {
                root.AddComponent<GameManager>();
                changed = true;
            }

            if (root.GetComponent<SignalHaulScenePreview>() == null)
            {
                root.AddComponent<SignalHaulScenePreview>();
                changed = true;
            }

            if (changed || scene.path != ScenePath)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }
    }
}
#endif
