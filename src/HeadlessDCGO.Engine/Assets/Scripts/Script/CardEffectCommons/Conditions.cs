namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (EFFECT-MODEL REBUILD / path-fidelity) The AS-IS play-condition types (Assembly/Link/AppFusion/Jogress/
// DigiXros/BurstDigivolution + their Element types) were relocated to CardSource.cs (Script/CardSource.cs) to
// match AS-IS's file grouping — AS-IS declares them all in CardSource.cs. This file now holds only the
// mirror-invented DigivolveCost enum, which has no AS-IS analog (a headless cost-treatment projection).

/// <summary>(PRIM-P0-flow B.O.3) The cost treatment of a select-and-digivolve (AS-IS payCost / reduceCost /
/// fixedCost knobs). Mirror-invented — no AS-IS type; keeps the headless select-and-digivolve API expressive.</summary>
public enum DigivolveCost
{
    Free,     // payCost:false
    Normal,   // the resolved evolution cost (ContinuousModifierGate-folded)
    Reduced,  // Normal minus a fixed amount (floored at 0)
    Fixed,    // a fixed literal cost
}
