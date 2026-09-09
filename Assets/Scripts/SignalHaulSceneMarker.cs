using UnityEngine;

namespace SignalHaul
{
    public sealed class SignalHaulSceneMarker : MonoBehaviour
    {
        public const int CurrentVersion = 6;

        [SerializeField, HideInInspector] private int sceneVersion = CurrentVersion;
        public int SceneVersion => sceneVersion;

        public void InitializeVersion()
        {
            sceneVersion = CurrentVersion;
        }
    }
}
