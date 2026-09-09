using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DroneHazard : NetworkBehaviour
    {
        [SerializeField] private float orbitRadius = 5f;
        [SerializeField, Min(.03f)] private float syncInterval = .06f;
        [SerializeField, Min(1f)] private float remoteLerpSpeed = 15f;

        private readonly NetworkVariable<Vector3> syncedPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Vector3 center;
        private float phase;
        private float hitCooldown;
        private float syncTimer;

        public void Configure(float radius)
        {
            orbitRadius = radius;
        }

        public override void OnNetworkSpawn()
        {
            center = transform.position;

            if (IsServer)
            {
                phase = Random.Range(0f, 6f);
                syncedPosition.Value = transform.position;
            }
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (!IsServer)
            {
                float lerp = 1f - Mathf.Exp(-remoteLerpSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, syncedPosition.Value, lerp);
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.Ended)
                return;

            hitCooldown -= Time.deltaTime;
            PlayerController nearest = FindNearestPlayer(out float distance);

            Vector3 target;
            if (nearest != null && distance < 8f)
            {
                target = nearest.transform.position + Vector3.up * 1.1f;
                transform.position = Vector3.MoveTowards(transform.position, target, 4.2f * Time.deltaTime);
            }
            else
            {
                float t = Time.time * .65f + phase;
                target = center + new Vector3(Mathf.Cos(t) * orbitRadius, Mathf.Sin(t * 1.7f) * .8f, Mathf.Sin(t) * orbitRadius);
                transform.position = Vector3.Lerp(transform.position, target, 2.2f * Time.deltaTime);
            }

            if (nearest != null && distance < 1.45f && hitCooldown <= 0f)
            {
                hitCooldown = 1.2f;
                nearest.ApplyDamageServer(14f);
            }

            syncTimer -= Time.deltaTime;
            if (syncTimer <= 0f)
            {
                syncTimer = syncInterval;
                syncedPosition.Value = transform.position;
            }
        }

        private PlayerController FindNearestPlayer(out float nearestDistance)
        {
            nearestDistance = float.PositiveInfinity;
            PlayerController nearest = null;

            if (NetworkManager == null || !NetworkManager.IsServer)
                return null;

            foreach (NetworkClient client in NetworkManager.ConnectedClients.Values)
            {
                if (client.PlayerObject == null)
                    continue;

                PlayerController player = client.PlayerObject.GetComponent<PlayerController>();
                if (player == null || player.Health <= 0f)
                    continue;

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = player;
            }

            return nearest;
        }
    }
}
