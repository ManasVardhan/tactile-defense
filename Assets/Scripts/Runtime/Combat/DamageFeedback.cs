using UnityEngine;

namespace TactileWars.Combat
{
    /// <summary>Hit flash (temporary swap to a shared white material, keeps SRP batching) and squash and stretch.</summary>
    public class DamageFeedback
    {
        readonly Renderer[] renderers;
        readonly Material[][] normal;
        readonly Material[][] flash;
        readonly Transform squashTarget;
        readonly Vector3 baseScale;
        float flashTimer;
        float squash;
        bool flashing;

        public DamageFeedback(Renderer[] rs, Material flashMat, Transform squashRoot)
        {
            renderers = rs ?? new Renderer[0];
            normal = new Material[renderers.Length][];
            flash = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                normal[i] = renderers[i].sharedMaterials;
                flash[i] = new Material[normal[i].Length];
                for (int k = 0; k < flash[i].Length; k++) flash[i][k] = flashMat != null ? flashMat : normal[i][k];
            }
            squashTarget = squashRoot;
            baseScale = squashRoot != null ? squashRoot.localScale : Vector3.one;
        }

        /// <summary>Call after the team tint changed the materials.</summary>
        public void RefreshNormal()
        {
            for (int i = 0; i < renderers.Length; i++) normal[i] = renderers[i].sharedMaterials;
        }

        /// <summary>Squash (positive) or stretch (negative) without a flash: hops, landings.</summary>
        public void Squash(float amount)
        {
            if (Mathf.Abs(amount) > Mathf.Abs(squash)) squash = amount;
        }

        public void Trigger(float duration, float squashAmount)
        {
            flashTimer = duration;
            squash = Mathf.Max(squash, squashAmount);
            if (!flashing)
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = flash[i];
                flashing = true;
            }
        }

        public void Tick(float dt, float recover)
        {
            if (flashing)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f)
                {
                    for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sharedMaterials = normal[i];
                    flashing = false;
                }
            }
            if (Mathf.Abs(squash) > 0.001f && squashTarget != null)
            {
                squash = Mathf.MoveTowards(squash, 0f, recover * dt * Mathf.Max(0.1f, Mathf.Abs(squash)));
                float s = squash;
                squashTarget.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z * (1f + s));
                if (Mathf.Abs(squash) <= 0.001f) squashTarget.localScale = baseScale;
            }
        }
    }
}
