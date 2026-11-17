using System.Collections.Generic;
using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>Enemy cluster in a loose ring around a bubbled flag bearer. Idles, then engages and either
    /// holds or loosely advances. Killing the bearer leaves a bubble: tapping it wipes the squad (verified mechanic).</summary>
    public class EnemySquad : MonoBehaviour
    {
        public EnemySquadConfig config;
        public bool advance = true;
        public int soldierCountOverride = 0;
        public int seed = 1;
        [Tooltip("Counterattack wave: marches on 'assaultPoint' with no leash and engages anything on the way.")] public bool assault;
        public Vector3 assaultPoint;
        public float assaultSpeed = 2.2f;

        readonly List<Soldier> members = new List<Soldier>(16);
        Vector3[] offsets;
        Vector3[] positions = new Vector3[16];
        Soldier bearer;
        FlagBubble bubble;
        Vector3 home;
        Vector3 center;
        bool engaged;
        bool wiped;
        float scanTimer;
        Vector3 threat;

        public bool Engaged => engaged;
        public bool Wiped => wiped;
        public int AliveCount { get { int c = 0; for (int i = 0; i < members.Count; i++) if (members[i] != null && members[i].IsAlive) c++; return c; } }
        public FlagBubble Bubble => bubble;
        public Soldier Bearer => bearer;
        public Vector3 Center => center;

        void Start()
        {
            home = transform.position; home.y = 0f;
            center = home;
            Spawn();
        }

        void Spawn()
        {
            var prefabs = BattleContext.Prefabs;
            var cfg = config;
            if (prefabs == null || prefabs.soldier == null || cfg == null) { Debug.LogWarning("EnemySquad: missing prefabs or config", this); return; }
            var color = BattleContext.Config != null ? BattleContext.Config.enemyTeam : TeamColorId.Teal;
            int n = soldierCountOverride > 0 ? soldierCountOverride : cfg.soldierCount;
            var rng = new System.Random(seed);
            offsets = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.5f;
                float r = cfg.clusterRadius + ((float)rng.NextDouble() - 0.5f) * 2f * cfg.clusterJitter;
                if (i % 4 == 3) r *= 0.55f; // a few stand closer to the bearer
                offsets[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < n; i++)
            {
                var s = Instantiate(prefabs.soldier, center + offsets[i], Quaternion.Euler(0f, 180f + Random.Range(-40f, 40f), 0f), transform);
                s.name = "Enemy_" + i;
                s.Setup(Team.Enemy, color, cfg.soldier);
                s.Motor.Teleport(center + offsets[i]);
                s.Motor.RestFacing = Quaternion.Euler(0f, Random.Range(150f, 210f), 0f) * Vector3.forward;
                members.Add(s);
            }
            if (positions.Length < n + 1) positions = new Vector3[n + 1];

            // Flag bearer at the centre, inside a bubble.
            var bearerPrefab = prefabs.flagBearer != null ? prefabs.flagBearer : prefabs.soldier;
            bearer = Instantiate(bearerPrefab, center, Quaternion.Euler(0f, 180f, 0f), transform);
            bearer.name = "FlagBearer";
            bearer.Setup(Team.Enemy, color, cfg.flagBearer != null ? cfg.flagBearer : cfg.soldier, TargetKind.FlagBearer);
            bearer.Motor.Teleport(center);
            bearer.Motor.RestFacing = Vector3.back;
            bearer.Health.Died += OnBearerDied;
            if (prefabs.bubble != null)
            {
                bubble = Instantiate(prefabs.bubble, center, Quaternion.identity, transform);
                bubble.Bind(this, bearer, color);
            }
        }

        void OnBearerDied(Health h, TeamColorId by)
        {
            if (bubble != null) bubble.MakePoppable();
        }

        /// <summary>Tapping the leftover bubble: the whole squad pops at once.</summary>
        public void WipeOut()
        {
            if (wiped) return;
            wiped = true;
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null && !members[i].IsGone) members[i].Pop();
            if (bearer != null && !bearer.IsGone) bearer.Pop();
            AudioManager.Play(SoundId.BubblePop, center);
            BattleEvents.RaiseSquadWiped(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var gc = BattleContext.Config;
            Vector2 bmin = gc != null ? gc.mapMin : new Vector2(-100, -100);
            Vector2 bmax = gc != null ? gc.mapMax : new Vector2(100, 100);

            scanTimer -= dt;
            if (scanTimer <= 0f && config != null)
            {
                scanTimer = 0.25f;
                var near = UnitRegistry.FindNearest(Team.Player, center, engaged ? config.aggroRange * 1.6f : config.aggroRange);
                if (near != null) { engaged = true; threat = near.transform.position; }
                else if (engaged && UnitRegistry.FindNearest(Team.Player, center, config.aggroRange * 2f) == null) engaged = false;
            }

            if (assault && config != null)
            {
                // March on the objective; stop at weapon stand off when something is in range.
                float range = config.soldier != null && config.soldier.weapon != null ? config.soldier.weapon.range : 6f;
                var near = UnitRegistry.FindNearest(Team.Player, center, range * 0.8f);
                engaged = true;
                Vector3 goal = near != null ? center : assaultPoint;
                Vector3 to = goal - center; to.y = 0f;
                if (to.magnitude > 0.5f) center += to.normalized * Mathf.Min(to.magnitude, assaultSpeed * dt);
            }
            else if (engaged && advance && config != null && config.soldier != null && config.soldier.weapon != null)
            {
                Vector3 to = threat - center; to.y = 0f;
                float standOff = config.soldier.weapon.range * config.standOffFraction;
                if (to.magnitude > standOff)
                {
                    Vector3 next = center + to.normalized * config.advanceSpeed * dt;
                    if ((next - home).magnitude <= config.leashDistance) center = next;
                }
            }
            else if (!engaged)
            {
                center = Vector3.MoveTowards(center, home, config != null ? config.advanceSpeed * 0.5f * dt : 0f);
            }

            int count = 0;
            for (int i = 0; i < members.Count; i++) if (members[i] != null) positions[count++] = members[i].transform.position;
            if (bearer != null && count < positions.Length) positions[count++] = bearer.transform.position;

            int idx = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var s = members[i];
                if (s == null) continue;
                var ms = s.Config.ToMotor(bmin, bmax);
                if (!engaged) ms.maxSpeed *= 0.5f;
                s.Motor.Target = center + offsets[i];
                s.HoldFire = !engaged;
                s.Tick(dt, ms, positions, count, idx);
                idx++;
            }
            if (bearer != null)
            {
                var ms = bearer.Config.ToMotor(bmin, bmax);
                ms.maxSpeed *= 0.6f;
                bearer.Motor.Target = center;
                bearer.HoldFire = !engaged;
                bearer.Tick(dt, ms, positions, count, count - 1);
            }

            // Drop destroyed members.
            for (int i = members.Count - 1; i >= 0; i--)
            {
                if (members[i] == null || members[i].IsGone)
                {
                    members.RemoveAt(i);
                    var o = offsets;
                    for (int k = i; k < o.Length - 1; k++) o[k] = o[k + 1];
                }
            }
            if (bearer != null && bearer.IsGone) bearer = null;

            if (!wiped && members.Count == 0 && bearer == null)
            {
                wiped = true;
                if (bubble != null) bubble.Pop(false);
                BattleEvents.RaiseSquadWiped(this);
            }
        }
    }
}
