// Source: DCGO/Assets/Scripts/Script/SkillInfo.cs
// (EFFECT-MODEL REBUILD / P6 missing-type) 1:1 mirror of the AS-IS `SkillInfo` — a pure-data holder
// (`ICardEffect` + `Hashtable` + `EffectTiming`) built by GetFromHashtable / the card-effect factory layer
// when re-hydrating a resolved skill from its Hashtable payload.
//
// This file previously held only a migration-scaffold skeleton (no namespace, no members, a literal
// "port later" placeholder) — replaced here with the real 1:1 port.
//
// Namespace: `...Script.CardEffectCommons`, matching `ICardEffect` (abstract class, ICardEffect.cs) and
// `EffectTiming` (enum, EffectTiming.cs) which this class references verbatim — both already live there.
// AS-IS this type has no namespace (global); the factory/GetFromHashtable call sites that reference `SkillInfo`
// expect the CardEffectCommons one (see task brief) — DO NOT confuse with the pre-existing, structurally
// different `HeadlessDCGO.Engine.Headless.Effects.SkillInfo` substrate record (Headless/Effects/SkillInfo.cs),
// which is left untouched.
//
// No Unity/Photon references in the AS-IS file — ported verbatim, no adaptation needed beyond the namespace.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;

// AS-IS SkillInfo.cs:3-15.
public class SkillInfo
{
    public SkillInfo(ICardEffect cardEffect, Hashtable hashtable, EffectTiming timing)
    {
        CardEffect = cardEffect;
        Hashtable = hashtable;
        Timing = timing;
    }

    public ICardEffect CardEffect { get; set; }
    public Hashtable Hashtable { get; set; }
    public EffectTiming Timing { get; set; }
}
