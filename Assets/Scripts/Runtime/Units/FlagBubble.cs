using TactileWars.Audio;
using TactileWars.Combat;
using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>Glass bubble around an enemy flag bearer with a team colored health arc on the ground.
    /// After the bearer dies the bubble floats in place; tapping it wipes out the rest of the squad.</summary>
    public class FlagBubble : MonoBehaviour, ITappable
    {
        public Transform sphere;
        public Transform flag;
        public Renderer arcRenderer;
        public SphereCollider tapCollider;
        public TeamTint flagTint;
        public float floatHeight = 0.9f;
        [Tooltip("Flag scale while carried: the reference pennant rises well above the bubble.")] public float carriedFlagScale = 1.9f;

        EnemySquad squad;
        Soldier bearer;
        bool poppable;
        bool popped;
        float t;
        Vector3 anchor;
        MaterialPropertyBlock mpb;
        static readonly int FillId = Shader.PropertyToID("_Fill");
        Vector3 sphereBase;

        public bool CanTap => poppable && !popped;
        public bool Poppable => poppable;

        public void Bind(EnemySquad s, Soldier b, TeamColorId color)
        {
            squad = s; bearer = b;
            gameObject.layer = Layers.Pickup;
            if (tapCollider != null) { tapCollider.gameObject.layer = Layers.Pickup; tapCollider.enabled = false; }
            if (flagTint != null && BattleColors.Visuals != null) flagTint.Apply(BattleColors.Visuals.Get(color));
            // The arc fills in the attacker's paint as the bearer takes damage (reference ps_12, burst_B_030).
            var gc = TactileWars.Battle.BattleContext.Config;
            if (arcRenderer != null && BattleColors.Visuals != null && gc != null)
            {
                var m = BattleColors.Visuals.Get(gc.playerTeam);
                if (m != null && m.arc != null) arcRenderer.sharedMaterial = m.arc;
            }
            mpb = new MaterialPropertyBlock();
            sphereBase = sphere != null ? sphere.localScale : Vector3.one;
            if (flag != null && bearer != null)
            {
                flag.SetParent(bearer.transform, false);
                flag.localPosition = new Vector3(0.25f, 0f, -0.15f);
                flag.localRotation = Quaternion.identity;
                flag.localScale = Vector3.one * carriedFlagScale;
            }
        }

        void LateUpdate()
        {
            if (popped) return;
            t += Time.deltaTime;
            if (!poppable)
            {
                if (bearer != null) anchor = bearer.transform.position;
                transform.position = anchor;
                SetFill(bearer != null ? 1f - bearer.Health.Fraction : 1f);
            }
            else
            {
                float bob = Mathf.Sin(t * 2.4f) * 0.12f;
                transform.position = anchor + Vector3.up * (floatHeight + bob);
                float pulse = 1f + Mathf.Sin(t * 5f) * 0.05f;
                if (sphere != null) sphere.localScale = sphereBase * pulse;
            }
        }

        void SetFill(float f)
        {
            if (arcRenderer == null) return;
            arcRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(FillId, f);
            arcRenderer.SetPropertyBlock(mpb);
        }

        public void MakePoppable()
        {
            if (popped) return;
            poppable = true;
            if (bearer != null) anchor = bearer.transform.position;
            anchor.y = 0f;
            // The flag stays inside the bubble.
            if (flag != null)
            {
                flag.SetParent(transform, false);
                flag.localPosition = new Vector3(0f, 0.1f, 0f);
                flag.localRotation = Quaternion.identity;
                flag.localScale = Vector3.one * 0.9f;
            }
            if (arcRenderer != null) arcRenderer.enabled = false;
            if (tapCollider != null) tapCollider.enabled = true;
        }

        public void OnTapped()
        {
            if (!CanTap) return;
            if (squad != null) squad.WipeOut();
            Pop(true);
        }

        public void Pop(bool tapped)
        {
            if (popped) return;
            popped = true;
            if (VfxManager.Instance != null) VfxManager.Instance.Play(VfxType.BubblePop, transform.position + Vector3.up * 0.4f, Color.white, 1.2f);
            if (!tapped) AudioManager.Play(SoundId.BubblePop, transform.position);
            gameObject.SetActive(false);
        }
    }
}
