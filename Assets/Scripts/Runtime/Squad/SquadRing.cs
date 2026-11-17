using UnityEngine;

namespace TactileWars.Squad
{
    /// <summary>Large flat team colored ring with a spiral paint blot that marks the squad anchor.</summary>
    public class SquadRing : MonoBehaviour
    {
        public Transform ring;
        public Transform blot;
        public float spinSpeed = 25f;
        float pulse;
        float radius = 4f;

        public void SetRadius(float r)
        {
            radius = r;
            if (ring != null) ring.localScale = new Vector3(r * 2f, r * 2f, 1f);
        }

        public void Pulse() { pulse = 1f; }

        public void Follow(Vector3 anchor, float sharpness, float dt, bool snap)
        {
            anchor.y = 0f;
            Vector3 p = transform.position;
            p = snap ? anchor : Vector3.Lerp(p, anchor, 1f - Mathf.Exp(-sharpness * dt));
            p.y = 0.02f;
            transform.position = p;
            if (blot != null) blot.Rotate(0f, 0f, spinSpeed * dt, Space.Self);
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 3f);
            float k = 1f + pulse * 0.08f;
            if (ring != null) ring.localScale = new Vector3(radius * 2f * k, radius * 2f * k, 1f);
        }
    }
}
