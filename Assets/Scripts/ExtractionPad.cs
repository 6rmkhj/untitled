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

            PhysicsLoot item = other.GetComponentInParent<PhysicsLoot>();
            if (item != null && GameManager.Instance != null)
                GameManager.Instance.DeliverLootServer(item);
        }
    }
}
