using TactileWars.Core;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Live 3D home base behind the menu: HQ, props and idle soldiers tinted in the player's color.
    /// Soldiers occasionally cheer; the camera drifts very slightly.</summary>
    public class MenuBackdrop : MonoBehaviour
    {
        public TeamVisuals visuals;
        public TeamTint[] tinted = new TeamTint[0];
        public Animator[] soldierAnimators = new Animator[0];
        public Transform cameraPivot;
        public Renderer ringRenderer;
        public Renderer blotRenderer;
        float[] cheerTimer;
        static readonly int CheerId = Animator.StringToHash("Cheer");
        static readonly int SpeedId = Animator.StringToHash("Speed");
        Vector3 camBase;

        void Start()
        {
            cheerTimer = new float[soldierAnimators.Length];
            for (int i = 0; i < soldierAnimators.Length; i++)
            {
                var a = soldierAnimators[i];
                if (a == null) continue;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.SetFloat(SpeedId, 0f);
                a.Update(Random.value * 2f);
                cheerTimer[i] = 2f + Random.value * 8f;
            }
            if (cameraPivot != null) camBase = cameraPivot.position;
        }

        public void ApplyTeam(TeamColorId team)
        {
            if (visuals == null) return;
            var m = visuals.Get(team);
            foreach (var t in tinted) if (t != null) t.Apply(m);
            if (m != null && ringRenderer != null) ringRenderer.sharedMaterial = m.ring;
            if (m != null && blotRenderer != null) blotRenderer.sharedMaterial = m.blot;
        }

        public void CheerAll()
        {
            for (int i = 0; i < soldierAnimators.Length; i++)
                if (soldierAnimators[i] != null) { soldierAnimators[i].SetBool(CheerId, true); cheerTimer[i] = -(1.5f + Random.value); }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < soldierAnimators.Length; i++)
            {
                var a = soldierAnimators[i];
                if (a == null) continue;
                if (cheerTimer[i] > 0f)
                {
                    cheerTimer[i] -= dt;
                    if (cheerTimer[i] <= 0f) { a.SetBool(CheerId, true); cheerTimer[i] = -(1.2f + Random.value * 1.5f); }
                }
                else
                {
                    cheerTimer[i] += dt;
                    if (cheerTimer[i] >= 0f) { a.SetBool(CheerId, false); cheerTimer[i] = 4f + Random.value * 10f; }
                }
            }
            if (cameraPivot != null)
            {
                float t = Time.time * 0.15f;
                cameraPivot.position = camBase + new Vector3(Mathf.Sin(t) * 0.35f, 0f, Mathf.Sin(t * 0.7f) * 0.2f);
            }
        }
    }
}
