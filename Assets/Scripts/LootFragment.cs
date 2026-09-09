using UnityEngine;

namespace SignalHaul
{
    public sealed class LootFragment : MonoBehaviour
    {
        private Vector3 velocity;
        private Vector3 angularVelocity;
        private float life;
        private float initialLife;

        public void Configure(Vector3 initialVelocity, Vector3 initialAngularVelocity, float lifetime)
        {
            velocity = initialVelocity;
            angularVelocity = initialAngularVelocity;
            life = Mathf.Max(.25f, lifetime);
            initialLife = life;
        }

        private void Update()
        {
            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(angularVelocity * Time.deltaTime, Space.Self);

            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (life < .45f && initialLife > 0f)
            {
                float scale = Mathf.Clamp01(life / .45f);
                transform.localScale *= Mathf.Lerp(.92f, 1f, scale);
            }
        }
    }
}
