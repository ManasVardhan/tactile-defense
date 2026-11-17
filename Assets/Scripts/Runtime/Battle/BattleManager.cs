using TactileWars.Audio;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Formation;
using TactileWars.Squad;
using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Battle flow on a linear map: capture point 1 then point 2 (win), lose when every player soldier is
    /// dead. Tracks kills and the reinforcement meter and raises BattleEvents for the UI.</summary>
    [DefaultExecutionOrder(-500)]
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        public GameConfig gameConfig;
        public BattleConfig battleConfig;
        public BattlePrefabs prefabs;
        public CombatConfig combatConfig;
        public CapturePoint[] capturePoints = new CapturePoint[0];
        public SquadController squad;
        public StrokeRecorder recorder;
        [Tooltip("Spawn hatches: the counterattack wave starts at the one nearest the captured point.")] public Vector3[] counterSpawns = new Vector3[0];
        public Units.EnemySquadConfig counterSquadConfig;
        public Transform enemiesRoot;

        /// <summary>Defense battle: the AI attacks the player's base (roles unchanged: Team.Player attacks).
        /// Attackers wiped or time out means the defense held; both points captured means the base fell.</summary>
        public bool DefenseMode { get; set; }
        /// <summary>Seconds before the battle ends as a failed attack (0 = no limit). Used by defense battles.</summary>
        public float TimeLimit { get; set; }
        public bool TimedOut { get; private set; }
        public int CounterattacksLaunched { get; private set; }
        public Units.EnemySquad LastCounterattack { get; private set; }

        public ReinforcementMeter Meter { get; private set; }
        public int Kills { get; private set; }
        public int StructuresDestroyed { get; private set; }
        public int SquadsWiped { get; private set; }
        public float Elapsed { get; private set; }
        public bool Over { get; private set; }
        public BattleResult Result { get; private set; }
        public int CurrentPointIndex { get; private set; }
        int reinforcementsAdded;

        void Awake()
        {
            Instance = this;
            UnitRegistry.Clear();
            BattleContext.Config = gameConfig;
            BattleContext.Battle = battleConfig;
            BattleContext.Prefabs = prefabs;
            BattleContext.Combat = combatConfig;
            GameConfig.Active = gameConfig;
            BattleColors.Palette = gameConfig != null ? gameConfig.palette : null;
            BattleColors.Visuals = gameConfig != null ? gameConfig.visuals : null;
            Meter = new ReinforcementMeter(battleConfig != null ? battleConfig.meterRequired : 30f);
            BattleEvents.UnitKilled += OnUnitKilled;
            BattleEvents.SquadWiped += OnSquadWiped;
            BattleEvents.PointCaptured += OnPointCaptured;
        }

        void OnDestroy()
        {
            BattleEvents.UnitKilled -= OnUnitKilled;
            BattleEvents.SquadWiped -= OnSquadWiped;
            BattleEvents.PointCaptured -= OnPointCaptured;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            for (int i = 0; i < capturePoints.Length; i++)
            {
                capturePoints[i].Init(battleConfig, i);
                capturePoints[i].SetActive(i == 0);
            }
            CurrentPointIndex = 0;
            BattleEvents.RaiseBattleStarted();
            BattleEvents.RaiseReinforcementChanged(0f, false);
        }

        void OnUnitKilled(Team team, TargetKind kind, Vector3 pos)
        {
            if (team != Team.Enemy || Over) return;
            var c = battleConfig;
            float add;
            if (kind == TargetKind.Structure) { StructuresDestroyed++; add = c != null ? c.perStructureKill : 4f; }
            else { Kills++; add = c != null ? c.perSoldierKill : 1f; }
            bool nowReady = Meter.Add(add);
            if (nowReady) AudioManager.Play(SoundId.ReinforcementReady, pos);
            BattleEvents.RaiseReinforcementChanged(Meter.Fill, Meter.Ready);
        }

        void OnSquadWiped(Units.EnemySquad s)
        {
            if (Over) return;
            SquadsWiped++;
            Meter.Add(battleConfig != null ? battleConfig.perSquadWipe : 3f);
            BattleEvents.RaiseReinforcementChanged(Meter.Fill, Meter.Ready);
        }

        void OnPointCaptured(int index)
        {
            if (Over) return;
            int next = index + 1;
            if (next < capturePoints.Length)
            {
                CurrentPointIndex = next;
                capturePoints[next].SetActive(true);
                if (index == 0) ScheduleCounterattack(capturePoints[index].transform.position);
            }
            else End(true);
        }

        // ---------------- counterattack wave (ESTIMATE, BattleConfig.counterattack*)

        float counterTimer = -1f;
        Vector3 counterTarget;

        void ScheduleCounterattack(Vector3 point)
        {
            var c = battleConfig;
            if (c == null || !c.counterattackEnabled || c.counterattackSize <= 0 || counterSpawns == null || counterSpawns.Length == 0) return;
            counterTarget = point;
            counterTimer = Mathf.Max(0f, c.counterattackDelay);
        }

        void TickCounterattack(float dt)
        {
            if (counterTimer < 0f) return;
            counterTimer -= dt;
            if (counterTimer >= 0f) return;
            LaunchCounterattack(counterTarget);
        }

        /// <summary>Spawns the wave at the hatch nearest 'point' and sends it at the point. Returns the squad.</summary>
        public Units.EnemySquad LaunchCounterattack(Vector3 point)
        {
            counterTimer = -1f;
            var c = battleConfig;
            if (counterSquadConfig == null || counterSpawns == null || counterSpawns.Length == 0) return null;
            Vector3 best = counterSpawns[0];
            float bd = float.MaxValue;
            foreach (var h in counterSpawns)
            {
                float d = (h - point).sqrMagnitude;
                if (d < bd) { bd = d; best = h; }
            }
            var g = new GameObject("Counterattack_" + (CounterattacksLaunched + 1));
            if (enemiesRoot != null) g.transform.SetParent(enemiesRoot, false);
            g.transform.position = best;
            var es = g.AddComponent<Units.EnemySquad>();
            es.config = counterSquadConfig;
            es.advance = true;
            es.assault = true;
            es.assaultPoint = point;
            es.assaultSpeed = c != null ? c.counterattackSpeed : 2.2f;
            es.soldierCountOverride = c != null ? c.counterattackSize : 6;
            es.seed = 900 + CounterattacksLaunched;
            CounterattacksLaunched++;
            LastCounterattack = es;
            BattleEvents.RaiseEnemySquadSpawned(es);
            BattleEvents.RaiseCounterattack(best);
            return es;
        }

        void Update()
        {
            if (Over) return;
            Elapsed += Time.deltaTime;
            TickCounterattack(Time.deltaTime);
            if (!DefenseMode && PointerInput.ReinforceRequested) CallReinforcements();
            if (TimeLimit > 0f && Elapsed >= TimeLimit) { TimedOut = true; End(false); return; }
            if (squad != null && squad.StartCount > 0 && squad.AliveCount == 0 && Elapsed > 0.5f) End(false);
        }

        /// <summary>Spawns backup soldiers when the meter is full (keyboard R for now, UI later).</summary>
        public bool CallReinforcements()
        {
            if (Over || squad == null || !Meter.TryConsume()) return false;
            int n = battleConfig != null ? battleConfig.reinforcementCount : 10;
            squad.Reinforce(n);
            reinforcementsAdded += n;
            AudioManager.Play(SoundId.ReinforcementCall, squad.Centroid);
            BattleEvents.RaiseReinforced(n);
            BattleEvents.RaiseReinforcementChanged(Meter.Fill, Meter.Ready);
            return true;
        }

        public CapturePoint CurrentPoint => CurrentPointIndex < capturePoints.Length ? capturePoints[CurrentPointIndex] : null;

        void End(bool victory)
        {
            Over = true;
            var c = battleConfig;
            int survived = squad != null ? squad.AliveCount : 0;
            int total = (squad != null ? squad.StartCount : 0) + reinforcementsAdded;
            var r = new BattleResult
            {
                victory = victory,
                kills = Kills,
                structuresDestroyed = StructuresDestroyed,
                squadsWiped = SquadsWiped,
                timeSeconds = Elapsed,
                soldiersSurvived = survived,
                soldiersLost = Mathf.Max(0, total - survived),
            };
            if (c != null)
            {
                float gold = c.goldBase + c.goldPerKill * Kills + c.goldPerStructure * StructuresDestroyed + (victory ? c.goldVictoryBonus : 0);
                float xp = c.xpBase + c.xpPerKill * Kills + (victory ? c.xpVictoryBonus : 0);
                if (!victory) { gold *= c.defeatRewardFactor; xp *= c.defeatRewardFactor; }
                r.gold = Mathf.RoundToInt(gold);
                r.xp = Mathf.RoundToInt(xp);
            }
            Result = r;
            if (recorder != null) recorder.InputEnabled = false;
            counterTimer = -1f;
            if (victory)
            {
                if (squad != null) squad.SetCheer(true);
                // In a defense battle the attackers winning is the player's loss.
                AudioManager.Play(DefenseMode ? SoundId.Defeat : SoundId.Victory, Vector3.zero);
                BattleEvents.RaiseVictory(r);
            }
            else
            {
                AudioManager.Play(DefenseMode ? SoundId.Victory : SoundId.Defeat, Vector3.zero);
                BattleEvents.RaiseDefeat(r);
            }
            Debug.Log("[Battle] " + r);
        }
    }
}
