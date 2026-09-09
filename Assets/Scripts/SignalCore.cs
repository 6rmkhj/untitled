using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(PhysicsLoot))]
    public sealed class SignalCore : MonoBehaviour
    {
        private PhysicsLoot loot;

        public PhysicsLoot Loot
        {
            get
            {
                if (loot == null)
                    loot = GetComponent<PhysicsLoot>();
                return loot;
            }
        }

        public bool Delivered => Loot != null && Loot.Delivered;
        public bool Broken => Loot != null && Loot.Broken;

        private void Reset()
        {
            PhysicsLoot physicsLoot = GetComponent<PhysicsLoot>();
            if (physicsLoot != null)
                physicsLoot.Configure("SIGNAL CORE", 1000, 5f, 120f, 7.5f, 1.1f, 1, 1, true);
        }

        private void Awake()
        {
            loot = GetComponent<PhysicsLoot>();
        }

        public void SetMapSpawnServer(Vector3 position)
        {
            Loot?.SetMapSpawnServer(position);
        }
    }
}
