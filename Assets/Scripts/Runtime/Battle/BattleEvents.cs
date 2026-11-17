using System;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Formation;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.Battle
{
    [Serializable]
    public struct BattleResult
    {
        public bool victory;
        public int kills;
        public int structuresDestroyed;
        public int squadsWiped;
        public float timeSeconds;
        public int soldiersLost;
        public int soldiersSurvived;
        public int gold;
        public int xp;

        public override string ToString() =>
            $"{(victory ? "Victory" : "Defeat")} kills={kills} structures={structuresDestroyed} squadsWiped={squadsWiped} time={timeSeconds:0.0}s lost={soldiersLost} survived={soldiersSurvived} gold={gold} xp={xp}";
    }

    /// <summary>Static battle events. UI subscribes; gameplay never references UI.</summary>
    public static class BattleEvents
    {
        public static event Action BattleStarted;
        /// <summary>(capture point index, progress 0..1, contested)</summary>
        public static event Action<int, float, bool> CaptureProgress;
        public static event Action<int> PointCaptured;
        /// <summary>(alive, max)</summary>
        public static event Action<int, int> SoldierCountChanged;
        /// <summary>(fill 0..1, ready)</summary>
        public static event Action<float, bool> ReinforcementChanged;
        public static event Action<int> Reinforced;
        public static event Action<BattleResult> Victory;
        public static event Action<BattleResult> Defeat;
        public static event Action<ShapeType> ShapeRecognized;
        public static event Action<Team, TargetKind, Vector3> UnitKilled;
        public static event Action<EnemySquad> SquadWiped;

        public static void RaiseBattleStarted() => BattleStarted?.Invoke();
        public static void RaiseCaptureProgress(int i, float p, bool contested) => CaptureProgress?.Invoke(i, p, contested);
        public static void RaisePointCaptured(int i) => PointCaptured?.Invoke(i);
        public static void RaiseSoldierCountChanged(int alive, int max) => SoldierCountChanged?.Invoke(alive, max);
        public static void RaiseReinforcementChanged(float fill, bool ready) => ReinforcementChanged?.Invoke(fill, ready);
        public static void RaiseReinforced(int n) => Reinforced?.Invoke(n);
        public static void RaiseVictory(BattleResult r) => Victory?.Invoke(r);
        public static void RaiseDefeat(BattleResult r) => Defeat?.Invoke(r);
        public static void RaiseShapeRecognized(ShapeType s) => ShapeRecognized?.Invoke(s);
        public static void RaiseUnitKilled(Team t, TargetKind k, Vector3 p) => UnitKilled?.Invoke(t, k, p);
        public static void RaiseSquadWiped(EnemySquad s) => SquadWiped?.Invoke(s);

        /// <summary>Drops every subscriber (scene reloads, tests).</summary>
        public static void ClearAll()
        {
            BattleStarted = null; CaptureProgress = null; PointCaptured = null; SoldierCountChanged = null;
            ReinforcementChanged = null; Reinforced = null; Victory = null; Defeat = null; ShapeRecognized = null;
            UnitKilled = null; SquadWiped = null;
        }
    }
}
