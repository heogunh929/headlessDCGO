// (EFFECT-MODEL REBUILD / P6 stage A — LEGACY-BRIDGE, retires with the old-model corpus)
//
// The pre-rebuild mirror had `interface ICardEffect { EffectBinding ToBinding(string); }` and
// `interface IActivatedCardEffect : ICardEffect` (old CardEffectCommons/CardEffectInterfaces.cs). The rebuild
// replaced `ICardEffect` with the AS-IS ABSTRACT CLASS (Script/ICardEffect.cs) and removed the old trio —
// leaving the old-model primitive corpus (ActivatedEffects.cs / ActivatedEffect.cs / CardEffectFactory.cs
// legacy methods) referencing a dead `IActivatedCardEffect` marker: the RED-baseline 59 CS0246.
//
// Stage A could NOT retire that corpus: ~80 REAL card files (the C/D/E/F goal-witness corpus — BT9_043,
// BT9_062, BT22_003/035, BT24_018, AD1_025, EX1_072, EX8_074, ST4_13, … see rebuild_p6_stageA_notes.md §2)
// still construct old-model effect types directly and are only scheduled for AS-IS re-porting in a later
// batch. So the marker is resurrected here as a BASE CLASS deriving from the new abstract `ICardEffect`:
//
//   * the 59 declaration errors lift (unmasking the project's real method-body error surface);
//   * old-model effects become type-compatible with the new card surface (`List<ICardEffect>`), so the
//     un-re-ported card corpus keeps compiling AND keeps its verified behavior (the resolver's legacy switch
//     still dispatches on the concrete old types);
//   * new-model code never reads old effects through the base (the activated test for the NEW model is
//     `is ActivateICardEffect`, which no legacy type implements), and legacy paths read old effects through
//     their concrete types — so the base-member hiding (old classes re-declare MaxCountPerTurn/IsOptional/
//     IsInheritedEffect/…) is inert: no path reads a hidden member through the base.
//
// DELETE this file together with ActivatedEffects.cs / ActivatedEffect.cs / the CardEffectFactory legacy
// methods once the remaining old-model consumers (real-card re-port backlog + Tfx fixtures) are re-ported.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.Concurrent;
using System.Reflection;
using HeadlessDCGO.Engine.Headless.Effects;

/// <summary>LEGACY-BRIDGE base for the OLD-model activated primitives (pre-rebuild
/// <c>IActivatedCardEffect</c> marker interface, now a class so old primitives inherit the new abstract
/// <see cref="ICardEffect"/> and stay addable to the new card surface). NOT part of the AS-IS model — the
/// AS-IS activated contract is <see cref="ActivateICardEffect"/>, which no legacy type implements.</summary>
public abstract class IActivatedCardEffect : ICardEffect
{
}

/// <summary>LEGACY-BRIDGE lowering of an OLD-model effect to an <see cref="EffectBinding"/>. The old
/// <c>ICardEffect</c> interface carried <c>ToBinding(string)</c> as a contract member; the new abstract
/// <see cref="ICardEffect"/> (AS-IS 1:1) has no such member, but every old-model class still declares the
/// method. This single reflective dispatch preserves the old registration path byte-for-byte for the
/// un-re-ported corpus without touching ~90 legacy class declarations; a NEW-model effect (no ToBinding
/// method) returns false — the flip's live-enumeration path serves it instead.</summary>
public static class LegacyBindingBridge
{
    private static readonly ConcurrentDictionary<Type, MethodInfo?> ToBindingByType = new();

    public static bool TryToBinding(ICardEffect cardEffect, string effectId, out EffectBinding? binding)
    {
        ArgumentNullException.ThrowIfNull(cardEffect);
        binding = null;
        MethodInfo? method = ToBindingByType.GetOrAdd(
            cardEffect.GetType(),
            static type => type.GetMethod("ToBinding", BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) }));
        if (method is null || method.ReturnType != typeof(EffectBinding))
        {
            return false;
        }

        binding = (EffectBinding)method.Invoke(cardEffect, new object[] { effectId })!;
        return binding is not null;
    }
}
