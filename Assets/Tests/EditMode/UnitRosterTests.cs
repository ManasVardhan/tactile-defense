using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Meta;
using UnityEditor;
using UnityEngine;

namespace TactileWars.Tests
{
    /// <summary>The generated unit roster: all six types, each with its glyph, accent, description and the ability
    /// values its role needs.</summary>
    public class UnitRosterTests
    {
        static UnitRoster Roster() => AssetDatabase.LoadAssetAtPath<UnitRoster>("Assets/Data/UnitRoster.asset");

        [Test]
        public void UnitRosterHasAllSixTypesWithDescriptions()
        {
            var roster = Roster();
            Assert.IsNotNull(roster, "Assets/Data/UnitRoster.asset");
            Assert.AreEqual(6, roster.types.Length, "six unit types");
            var glyphs = new System.Collections.Generic.HashSet<string>();
            foreach (UnitTypeId id in System.Enum.GetValues(typeof(UnitTypeId)))
            {
                var t = roster.Get(id);
                Assert.IsNotNull(t, id + " is in the roster");
                Assert.AreEqual(id.ToString(), t.displayName);
                Assert.IsTrue(glyphs.Add(t.glyph), id + " has its own glyph");
                Assert.AreEqual(3, t.soldierCount);
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.description), id + " has a description");
                Assert.IsTrue(t.description.TrimEnd().EndsWith("."), id + " description is a sentence");
                Assert.IsFalse(t.description.Contains("\u2014"), "no em dashes");
                Assert.Greater(t.captureWeight, 0);
            }
            // Accents: distinct from each other.
            for (int i = 0; i < roster.types.Length; i++)
                for (int k = i + 1; k < roster.types.Length; k++)
                {
                    Color a = roster.types[i].accent, b = roster.types[k].accent;
                    float d = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    Assert.Greater(d, 0.25f, roster.types[i].id + " and " + roster.types[k].id + " accents differ");
                }

            var healer = roster.Get(UnitTypeId.Healer);
            Assert.AreEqual("H", healer.glyph);
            Assert.IsFalse(healer.hasWeapon);
            Assert.AreEqual(0.8f, healer.healthMult, 1e-4f);
            Assert.AreEqual(45f * 0.12f, healer.healPerSecond, 1e-3f, "12 percent of an Attacker soldier's health per second");
            Assert.AreEqual(8f, healer.reviveInterval, 1e-4f);

            var sniper = roster.Get(UnitTypeId.Sniper);
            Assert.AreEqual("S", sniper.glyph);
            Assert.AreEqual(0.6f, sniper.healthMult, 1e-4f);
            Assert.AreEqual(2f, sniper.rangeMult, 1e-4f);
            Assert.AreEqual(3f, sniper.damageMult, 1e-4f);
            Assert.AreEqual(3f, sniper.fireIntervalMult, 1e-4f);
            Assert.AreEqual(0.9f, sniper.modelScale, 1e-4f);

            var vanguard = roster.Get(UnitTypeId.Vanguard);
            Assert.AreEqual("V", vanguard.glyph);
            Assert.AreEqual(2f, vanguard.speedMult, 1e-4f);
            Assert.AreEqual(0.6f, vanguard.damageMult, 1e-4f);
            Assert.AreEqual(2, vanguard.captureWeight);

            var demo = roster.Get(UnitTypeId.Demolisher);
            Assert.AreEqual("D", demo.glyph);
            Assert.AreEqual(1.3f, demo.rangeMult, 1e-4f);
            Assert.AreEqual(0.8f, demo.speedMult, 1e-4f);
            Assert.AreEqual(2.5f, demo.fireIntervalMult, 1e-4f);
            Assert.IsTrue(demo.lobbed && demo.prefersStructures);
            Assert.AreEqual(1.8f, demo.splashRadius, 1e-4f);
            Assert.AreEqual(8f, demo.structureDamageMult, 1e-4f);
            Assert.AreEqual(0.4f, demo.soldierDamageMult, 1e-4f);
            Assert.AreEqual(1.15f, demo.modelScale, 1e-4f);

            // The Plan 1 types keep their roles and gain no abilities.
            foreach (var id in new[] { UnitTypeId.Attacker, UnitTypeId.Tank })
            {
                var t = roster.Get(id);
                Assert.IsTrue(t.hasWeapon);
                Assert.AreEqual(0f, t.healPerSecond);
                Assert.IsFalse(t.lobbed || t.prefersStructures);
                Assert.AreEqual(1, t.captureWeight);
            }
        }

        [Test]
        public void UnitStatBarsComeFromConfig()
        {
            var roster = Roster();
            var atk = roster.Get(UnitTypeId.Attacker);
            float A(UnitStat s) => TactileWars.Meta.UnitStatBars.Fill(atk, s, atk);
            float F(UnitTypeId id, UnitStat s) => TactileWars.Meta.UnitStatBars.Fill(roster.Get(id), s, atk);
            foreach (var s in TactileWars.Meta.UnitStatBars.Shown)
                Assert.AreEqual(TactileWars.Meta.UnitStatBars.ReferenceFill, A(s), 1e-4f, "the Attacker is the reference: " + s);
            Assert.Greater(F(UnitTypeId.Tank, UnitStat.Health), A(UnitStat.Health), "the Tank's Health bar is longer");
            Assert.Greater(F(UnitTypeId.Sniper, UnitStat.Range), A(UnitStat.Range), "the Sniper's Range bar is longer");
            Assert.Greater(F(UnitTypeId.Vanguard, UnitStat.Speed), A(UnitStat.Speed), "the Vanguard's Speed bar is longer");
            Assert.AreEqual(0f, F(UnitTypeId.Healer, UnitStat.Damage), "the Healer's Damage bar is empty");
            Assert.IsTrue(TactileWars.Meta.UnitStatBars.Heals(roster.Get(UnitTypeId.Healer)));
            Assert.Less(F(UnitTypeId.Tank, UnitStat.Speed), A(UnitStat.Speed), "the Tank is slower");
            foreach (var t in roster.types)
                foreach (var s in TactileWars.Meta.UnitStatBars.Shown)
                {
                    float f = TactileWars.Meta.UnitStatBars.Fill(t, s, atk);
                    Assert.That(f >= 0f && f < 1f, t.id + " " + s + " fill " + f + " stays on its track");
                }

            // Read from the config, not written by hand: changing a multiplier moves the bar.
            var copy = Object.Instantiate(roster.Get(UnitTypeId.Tank));
            try
            {
                float before = TactileWars.Meta.UnitStatBars.Fill(copy, UnitStat.Health, atk);
                copy.healthMult *= 0.25f;
                Assert.Less(TactileWars.Meta.UnitStatBars.Fill(copy, UnitStat.Health, atk), before);
                copy.hasWeapon = false;
                Assert.AreEqual(0f, TactileWars.Meta.UnitStatBars.Fill(copy, UnitStat.Damage, atk));
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
