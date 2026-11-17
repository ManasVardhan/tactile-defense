using TactileWars.CameraRig;
using TactileWars.Combat;
using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>Presentation for a soldier: animator parameters (Speed blend, Shoot, Hit, Die, Cheer), or a procedural
    /// bob when the model has no animator, plus the visual-only model transform:
    /// - the model leans back toward the camera so the face and body read like the original's upright sprites,
    /// - the visual yaw is clamped to the camera-facing half so soldiers never fully turn their backs
    ///   (gameplay facing, targeting and fire checks still use the root transform),
    /// - formation hop with stretch and landing squash, and a recoil kick per shot.</summary>
    public class SoldierView
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int ShootId = Animator.StringToHash("Shoot");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DieId = Animator.StringToHash("Die");
        static readonly int CheerId = Animator.StringToHash("Cheer");

        readonly Animator animator;
        readonly Transform model;
        readonly Transform root;
        readonly Vector3 modelBase;
        readonly Quaternion modelBaseRot;
        float phase;
        float bob;
        float shootKick;
        float visYaw;          // degrees from the camera-facing direction
        bool visInit;
        float hopT = -1f, hopDelay;
        bool dead;

        /// <summary>Squash request (positive squash, negative stretch); wired to DamageFeedback by the soldier.</summary>
        public System.Action<float> Squash;

        // Camera basis shared by every soldier, refreshed once per frame.
        static int basisFrame = -1;
        static Vector3 towardCam = Vector3.back;
        static Quaternion leanRot = Quaternion.identity;
        static float leanUsed = float.NaN;

        public SoldierView(Animator a, Transform modelRoot)
        {
            animator = a != null && a.runtimeAnimatorController != null ? a : null;
            model = modelRoot;
            root = modelRoot != null ? modelRoot.parent : null;
            modelBase = modelRoot != null ? modelRoot.localPosition : Vector3.zero;
            modelBaseRot = modelRoot != null ? modelRoot.localRotation : Quaternion.identity;
            phase = Random.value * 10f;
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.Update(Random.value * 0.5f); // desync idle cycles
            }
        }

        static CombatConfig Cfg => TactileWars.Battle.BattleContext.Combat;

        static void RefreshBasis(float lean)
        {
            if (basisFrame == Time.frameCount && Mathf.Approximately(leanUsed, lean)) return;
            basisFrame = Time.frameCount;
            leanUsed = lean;
            Transform cam = BattleCamera.Instance != null && BattleCamera.Instance.Cam != null ? BattleCamera.Instance.Cam.transform : null;
            if (cam == null) { var c = Camera.main; cam = c != null ? c.transform : null; }
            Vector3 f = cam != null ? cam.forward : Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            towardCam = -f;
            Vector3 right = Vector3.Cross(Vector3.up, f);
            leanRot = Quaternion.AngleAxis(lean, right); // positive tips the top toward the camera (verified with the lineup capture)
        }

        public void Tick(float dt, float speedNorm)
        {
            if (animator != null) animator.SetFloat(SpeedId, speedNorm, 0.08f, dt);
            else if (model != null)
            {
                phase += dt * Mathf.Lerp(2f, 14f, Mathf.Clamp01(speedNorm));
                bob = Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(0.01f, 0.08f, Mathf.Clamp01(speedNorm));
            }
            ApplyModel(dt);
        }

        /// <summary>Visual model transform. Also called while dying so the lean holds during the death pop.</summary>
        public void ApplyModel(float dt)
        {
            if (model == null || root == null) return;
            var cfg = Cfg;
            float lean = cfg != null ? cfg.visualLean : 30f;
            float maxYaw = cfg != null ? cfg.visualMaxYaw : 80f;
            float turn = cfg != null ? cfg.visualTurnSpeed : 900f;
            RefreshBasis(lean);

            Vector3 fwd = root.forward; fwd.y = 0f;
            float target = fwd.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(towardCam, fwd, Vector3.up) : visYaw;
            if (Mathf.Abs(target) > maxYaw)
            {
                // Facing away: show a 3/4 back-side view on the side the soldier already shows (hysteresis near 180).
                float side = Mathf.Abs(target) > 165f && visInit ? Mathf.Sign(visYaw == 0f ? target : visYaw) : Mathf.Sign(target);
                target = side * maxYaw;
            }
            if (!visInit || dead) { visYaw = dead ? visYaw : target; visInit = true; }
            else visYaw = Mathf.MoveTowardsAngle(visYaw, target, turn * dt);

            // Hop (formation set): parabola, stretch on the way up, squash on landing.
            float hop = 0f;
            if (hopT >= 0f)
            {
                if (hopDelay > 0f) hopDelay -= dt;
                else
                {
                    float dur = cfg != null ? Mathf.Max(0.05f, cfg.hopTime) : 0.28f;
                    if (hopT == 0f) Squash?.Invoke(-0.16f);
                    hopT += dt / dur;
                    if (hopT >= 1f)
                    {
                        hopT = -1f;
                        Squash?.Invoke(cfg != null ? cfg.landSquash : 0.22f);
                    }
                    else hop = (cfg != null ? cfg.hopHeight : 0.32f) * 4f * hopT * (1f - hopT);
                }
            }

            shootKick = Mathf.MoveTowards(shootKick, 0f, dt * 0.9f);
            Quaternion yawRot = Quaternion.LookRotation(Quaternion.AngleAxis(visYaw, Vector3.up) * towardCam, Vector3.up);
            Vector3 visFwd = yawRot * Vector3.forward;
            model.rotation = leanRot * yawRot * modelBaseRot;
            model.position = root.TransformPoint(modelBase) + Vector3.up * (hop + bob) - visFwd * shootKick;
        }

        public void Shoot()
        {
            if (animator != null) animator.SetTrigger(ShootId);
            var cfg = Cfg;
            shootKick = cfg != null ? cfg.recoilKick : 0.08f;
        }

        public void Hop(float delay)
        {
            if (dead) return;
            hopT = 0f;
            hopDelay = Mathf.Max(0f, delay);
        }

        public void Hit() { if (animator != null) animator.SetTrigger(HitId); }
        public void Die() { dead = true; hopT = -1f; if (animator != null) animator.SetTrigger(DieId); }
        public void Cheer(bool on) { if (animator != null) animator.SetBool(CheerId, on); }

        /// <summary>Current visual yaw in degrees from the camera-facing direction (tests).</summary>
        public float VisualYaw => visYaw;
    }
}
