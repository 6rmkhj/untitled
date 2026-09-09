#if UNITY_EDITOR
using System;
using System.Linq;
using SignalHaul;
using UnityEditor;
using UnityEngine;

namespace SignalHaul.Editor
{
    [InitializeOnLoad]
    public static class VoicePrefabSetup
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        static VoicePrefabSetup()
        {
            EditorApplication.delayCall += EnsureVoiceComponent;
        }

        [MenuItem("Tools/Signal Haul/Ensure Voice Chat On Player Prefab")]
        public static void EnsureVoiceComponent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<ProximityVoiceChat>() != null)
                    return;

                root.AddComponent<ProximityVoiceChat>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("SIGNAL HAUL: Added ProximityVoiceChat to NetworkPlayer.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    public sealed class VoicePrefabPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(path => string.Equals(path, VoicePrefabSetup.PlayerPrefabPath, StringComparison.OrdinalIgnoreCase)))
                EditorApplication.delayCall += VoicePrefabSetup.EnsureVoiceComponent;
        }
    }
}
#endif
