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

    /// <summary>(AD1-G) the stored AS-IS 4-arg battle predicate
    /// <c>Func&lt;Permanent(self), Permanent(attacker), Permanent(defender), CardSource(defendingCard), bool&gt;</c>
    /// — <c>CanNotBeDestroyedByBattleClass.CanNotBeDestroyedByBattle(this, AttackingPermanent,
    /// DefendingPermanent, DefendingCard)</c> (Permanent.cs:3252). Evaluated with the CURRENT attack state;
    /// absent = unconditional (the flag alone protects).</summary>
    public const string BattleConditionKey = "preventBattleDeletion.condition";

    public static bool PreventsBattleDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return false;
        }

        // (FR-P3) Evaluate over the effects that APPLY to this card — card-targeted AND player-scope (owner +
        // arbitrary permanentCondition predicate, evaluated 1:1) — so "your <X> Digimon cannot be deleted"
        // reaches the matching set, not just the source itself. The full result parses EVERY Delete/Prevent
        // replacement source (not just one flag), matching the pre-FR reading.
        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, ContinuousRestrictionGate.Scope, cardId);
        foreach (ReplacementEffect replacement in result.Replacements)
        {
            if (replacement.EventKind == ReplacementEventKind.Delete && replacement.ActionKind == ReplacementActionKind.Prevent)
            {
                return true;
            }
        }

        // Battle-only immunity (does not prevent effect deletion) — a value flag, not a replacement.
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, ContinuousRestrictionGate.Scope, cardId))
        {
            if (!effect.Context.Values.TryGetValue(PreventBattleDeletionKey, out object? bat) || bat is not bool b || !b)
            {
                continue;
            }

            // (AD1-G) honour the stored 4-arg battle predicate (self, attacker, defender, defendingCard)
            // against the CURRENT attack — absent predicate = unconditional flag.
            if (effect.Context.Values.TryGetValue(BattleConditionKey, out object? rawCond) &&
                rawCond is Func<Permanent, Permanent, Permanent, CardSource, bool> battleCondition &&
                !EvaluateBattleCondition(context, cardId, battleCondition))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool EvaluateBattleCondition(
        EngineContext context, HeadlessEntityId cardId, Func<Permanent, Permanent, Permanent, CardSource, bool> battleCondition)
    {
        Permanent self = View(context, cardId);
        HeadlessAttackState attack = context.AttackController.Current;
        Permanent attacker = View(context, attack.AttackerId ?? default);
        Permanent defender = View(context, attack.TargetId ?? default);
        CardSource defendingCard = defender?.TopCard!;
        try
        {
            return battleCondition(self!, attacker!, defender!, defendingCard);
        }
        catch (NullReferenceException)
        {
            // AS-IS card predicates dereference possibly-null participants without guards; a null
            // dereference there reads as "condition not met" (the AS-IS scan simply skips that effect).
            return false;
        }
    }

    private static Permanent? View(EngineContext context, HeadlessEntityId id) =>
        id.IsEmpty ? null : new Permanent(context, id, OwnerOf(context, id));

    private static HeadlessPlayerId OwnerOf(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : default;
}
