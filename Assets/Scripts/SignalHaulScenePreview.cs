using UnityEngine;

namespace SignalHaul
{
    [ExecuteAlways]
    public sealed class SignalHaulScenePreview : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            DrawBox(new Vector3(0, -1, 0), new Vector3(42, 2, 42), new Color(.08f, .1f, .13f, .35f));
            DrawBox(new Vector3(0, .2f, 0), new Vector3(13, .4f, 13), new Color(.3f, .34f, .36f, .65f));

            const int levels = 10;
            for (int i = 0; i < levels; i++)
            {
                float y = 2.2f + i * 3.1f;
                float x = i % 2 == 0 ? -2.8f : 2.8f;
                float z = (i / 2) % 2 == 0 ? 1.8f : -1.8f;

                DrawBox(new Vector3(x, y, z), new Vector3(6.2f, .35f, 5.3f), new Color(.35f, .38f, .4f, .75f));
                float bridgeX = i % 2 == 0 ? 1.1f : -1.1f;
                DrawBox(new Vector3(bridgeX, y + 1.45f, z * .45f), new Vector3(2.1f, .28f, 2.8f), new Color(1f, .55f, .05f, .85f));
                DrawBox(new Vector3(0, y + 1.5f, z), new Vector3(.45f, 3f, 4.6f), new Color(.2f, .24f, .28f, .8f));

                if (i % 2 == 0)
                {
                    DrawBox(new Vector3(x - 3f, y + .65f, z), new Vector3(.15f, 1.3f, 5.4f), new Color(.2f, .24f, .28f, .8f));
                    DrawBox(new Vector3(x + 3f, y + .65f, z), new Vector3(.15f, 1.3f, 5.4f), new Color(.2f, .24f, .28f, .8f));
                }
            }

            DrawSphere(new Vector3(-2.8f, 15.2f, 1.8f), .55f, Color.cyan);
            DrawSphere(new Vector3(2.8f, 24.5f, -1.8f), .55f, Color.cyan);
            DrawSphere(new Vector3(-2.8f, 33.8f, 1.8f), .55f, Color.cyan);

            DrawSphere(new Vector3(0, 11, 0), .65f, Color.red);
            DrawSphere(new Vector3(0, 21, 0), .65f, Color.red);
            DrawSphere(new Vector3(0, 31, 0), .65f, Color.red);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(-4.5f, 1.15f, -4.5f), new Vector3(.8f, 1.8f, .8f));

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(new Vector3(4.5f, .55f, -4.5f), 2.2f);
        }

        private static void DrawBox(Vector3 position, Vector3 scale, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawCube(position, scale);
            Gizmos.color = new Color(color.r, color.g, color.b, 1f);
            Gizmos.DrawWireCube(position, scale);
        }

        private static void DrawSphere(Vector3 position, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(position, radius);
        }
    }
}
