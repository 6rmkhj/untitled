using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    public sealed class ExtractionPad : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            SignalCore core = other.GetComponentInParent<SignalCore>();
            if (core != null && GameManager.Instance != null)
                GameManager.Instance.DeliverCoreServer(core);
        }
    }
}
