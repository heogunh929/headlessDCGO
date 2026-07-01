namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (R2-1 / N-2) Bridges <see cref="ContinuousEffectEvaluator"/> deletion-prevention REPLACEMENTS into
/// the battle deletion decision — the sibling of <see cref="ContinuousRestrictionGate"/> (which wires
/// the restriction slice). Before a battle would delete a Digimon, the registry is queried for
/// continuous replacement effects targeting it; a <see cref="ReplacementEventKind.Delete"/> /
/// <see cref="ReplacementActionKind.Prevent"/> replacement (e.g. a "cannot be deleted" effect granted
/// by another card) protects it, in addition to the static <c>preventBattleDeletion</c> flag the
/// battle resolvers already read.
///
/// Like the restriction gate, this queries continuous-role registry bindings only, so it is a no-op
/// until continuous effects are registered (Phase 4 card pool). NOTE: a battle-specific keyword such as
/// Jamming ("cannot be deleted by battle vs a Security Digimon") still needs its Phase-4 registration
/// to (a) register as a continuous replacement carrying <c>preventDeletion</c>, or (b) set the static
/// <c>preventBattleDeletion</c> flag at the battle-deletion check; this gate is the consumption half.
/// </summary>
public static class BattleDeletionGate
{
    // (PRIM-W4 CanNotBeDestroyedByBattleStaticEffect) a continuous BATTLE-ONLY deletion immunity flag —
    // read here (battle path) but NOT by the effect-delete path, so effect deletion still applies.
    public const string PreventBattleDeletionKey = "preventBattleDeletion";

    public static bool PreventsBattleDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return false;
        }

        // (FR-P3) Scan the effects that APPLY to this card — card-targeted AND player-scope (owner + arbitrary
        // permanentCondition predicate, evaluated 1:1) — so "your <X> Digimon cannot be deleted" reaches the
        // matching set, not just the source itself. A Delete/Prevent replacement is registered as the
        // preventDeletion flag; battle-only immunity is preventBattleDeletion.
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, ContinuousRestrictionGate.Scope, cardId))
        {
            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if ((values.TryGetValue(ReplacementHelpers.PreventDeletionKey, out object? del) && del is bool d && d)
                || (values.TryGetValue(PreventBattleDeletionKey, out object? bat) && bat is bool b && b))
            {
                return true;
            }
        }

        return false;
    }
}
