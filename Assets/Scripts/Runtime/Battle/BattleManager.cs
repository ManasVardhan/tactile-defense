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
            }
            else End(true);
        }

        void Update()
        {
            if (Over) return;
            Elapsed += Time.deltaTime;
            if (PointerInput.ReinforceRequested) CallReinforcements();
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
            if (victory)
            {
                if (squad != null) squad.SetCheer(true);
                AudioManager.Play(SoundId.Victory, Vector3.zero);
                BattleEvents.RaiseVictory(r);
            }
            else
            {
                AudioManager.Play(SoundId.Defeat, Vector3.zero);
                BattleEvents.RaiseDefeat(r);
            }
            Debug.Log("[Battle] " + r);
        }
    }
}
