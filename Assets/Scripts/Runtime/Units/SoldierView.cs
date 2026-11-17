using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>Presentation for a soldier: animator parameters (Speed blend, Shoot, Hit, Die, Cheer)
    /// or a procedural bob when the model has no animator (temporary stand-in).</summary>
    public class SoldierView
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int ShootId = Animator.StringToHash("Shoot");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DieId = Animator.StringToHash("Die");
        static readonly int CheerId = Animator.StringToHash("Cheer");

        readonly Animator animator;
        readonly Transform model;
        readonly Vector3 modelBase;
        float phase;
        float shootKick;

        public SoldierView(Animator a, Transform modelRoot)
        {
            animator = a != null && a.runtimeAnimatorController != null ? a : null;
            model = modelRoot;
            modelBase = modelRoot != null ? modelRoot.localPosition : Vector3.zero;
            phase = Random.value * 10f;
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.Update(Random.value * 0.5f); // desync idle cycles
            }
        }

        public void Tick(float dt, float speedNorm)
        {
            if (animator != null)
            {
                animator.SetFloat(SpeedId, speedNorm, 0.08f, dt);
                return;
            }
            if (model == null) return;
            phase += dt * Mathf.Lerp(2f, 14f, Mathf.Clamp01(speedNorm));
            float bob = Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(0.01f, 0.08f, Mathf.Clamp01(speedNorm));
            shootKick = Mathf.MoveTowards(shootKick, 0f, dt * 0.6f);
            model.localPosition = modelBase + new Vector3(0f, bob, -shootKick);
        }

        public void Shoot() { if (animator != null) animator.SetTrigger(ShootId); else shootKick = 0.06f; }
        public void Hit() { if (animator != null) animator.SetTrigger(HitId); }
        public void Die() { if (animator != null) animator.SetTrigger(DieId); }
        public void Cheer(bool on) { if (animator != null) animator.SetBool(CheerId, on); }
    }
}
