using UnityEngine;

namespace SignalHaul
{
    public sealed class ExtractionPad : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            var core = other.GetComponent<SignalCore>();
            if (core != null && GameManager.Instance != null)
                GameManager.Instance.DeliverCore(core);
        }
    }
}
