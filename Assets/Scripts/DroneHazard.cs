using UnityEngine;

namespace SignalHaul
{
    public sealed class DroneHazard : MonoBehaviour
    {
        [SerializeField] private float orbitRadius = 5f;

        private Vector3 center;
        private float phase;
        private float hitCooldown;

        public void Configure(float radius)
        {
            orbitRadius = radius;
        }

        private void Start()
        {
            center = transform.position;
            phase = Random.Range(0f, 6f);
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.Player == null)
                return;

            hitCooldown -= Time.deltaTime;
            var player = GameManager.Instance.Player.transform;
            float dist = Vector3.Distance(transform.position, player.position);
            Vector3 target;

            if (dist < 8f)
            {
                target = player.position + Vector3.up * 1.1f;
                transform.position = Vector3.MoveTowards(transform.position, target, 4.2f * Time.deltaTime);
            }
            else
            {
                float t = Time.time * .65f + phase;
                target = center + new Vector3(Mathf.Cos(t) * orbitRadius, Mathf.Sin(t * 1.7f) * .8f, Mathf.Sin(t) * orbitRadius);
                transform.position = Vector3.Lerp(transform.position, target, 2.2f * Time.deltaTime);
            }

            if (dist < 1.45f && hitCooldown <= 0f)
            {
                hitCooldown = 1.2f;
                GameManager.Instance.Player.TakeDamage(14f);
            }
        }
    }
}
