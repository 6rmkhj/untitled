using UnityEngine;

namespace SignalHaul
{
    public sealed class SignalHaulSceneMarker : MonoBehaviour
    {
        public const int CurrentVersion = 5;

        [SerializeField, HideInInspector] private int sceneVersion = CurrentVersion;
        public int SceneVersion => sceneVersion;

        public void InitializeVersion()
        {
            sceneVersion = CurrentVersion;
        }
    }
}
