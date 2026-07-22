// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/StartOfMainAttack.cs
// Decision: PORT (deferred — design item RD-3A-01)
// Category: CardEffect
//
// The AS-IS StartOfMainAttack grant is NOT ported here. On main, campaign ④ retired the invented
// EffectRegistry cluster (EffectBinding / StartOfMainAttackEffect payload / registry emits), so the
// pre-relocation registry body is invalid. ③-A judged the AS-IS body a "needs a new firing window"
// grant (mandatory OnStartMainPhase SelectAttackEffect offer via an inline ActivateClass, 0 live
// callers) — see the RD-3A-01 STOP that owns the surviving surface in the monolith:
//   CardEffectCommons.cs  ->  public static void StartOfMainAttack(Permanent?, CardSource)
//   (throws NotSupportedException "design item RD-3A-01").
// Port the AS-IS body 1:1 into this home file when a live caller AND an OnStartMainPhase auto-fire
// window exist; until then this file intentionally declares no member (the monolith STOP is the
// single surviving copy).
