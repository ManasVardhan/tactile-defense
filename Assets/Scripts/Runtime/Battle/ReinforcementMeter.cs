using UnityEngine;

namespace TactileWars.Battle
{
    /// <summary>Fills from kills; when full the player can call backup soldiers.</summary>
    public class ReinforcementMeter
    {
        public float Value { get; private set; }
        public float Required { get; private set; }
        public float Fill => Required > 0f ? Mathf.Clamp01(Value / Required) : 0f;
        public bool Ready => Value >= Required;
        /// <summary>Fraction added to Required after every call (diminishing returns).</summary>
        public float Growth { get; set; }
        public int Calls { get; private set; }

        public ReinforcementMeter(float required) { Required = Mathf.Max(1f, required); }

        /// <summary>Returns true when this add made the meter ready.</summary>
        public bool Add(float amount)
        {
            bool was = Ready;
            Value = Mathf.Min(Required, Value + amount);
            return !was && Ready;
        }

        public bool TryConsume()
        {
            if (!Ready) return false;
            Value = 0f;
            Calls++;
            Required *= 1f + Mathf.Max(0f, Growth);
            return true;
        }
    }
}
