namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (C-15 Progress, S2) While a Progress Digimon attacks, it is not affected by the opponent's effects
/// (UntilEndAttack). Mirrors AS-IS <c>ProgressStaticEffect</c>: a <c>CanNotAffectedClass</c> with
/// CardCondition = the attacker, SkillCondition = <c>IsOpponentEffect</c>. It is a PASSIVE static effect
/// (not optional / no agent choice), so it is applied automatically when the Progress Digimon's attack is
/// declared. The immunity is a continuous opponent-only <see cref="ContinuousImmunityGate"/> binding on the
/// attacker with <see cref="EffectDuration.UntilEndAttack"/> (auto-removed at attack end), consumed by the
/// mutation sink's immunity check.
/// </summary>
public static class ProgressImmunity
{
    public const string HasProgressKey = "hasProgress";

    /// <summary>Registers the opponent-only immunity on the current attacker if it has Progress (and not
    /// already registered this attack). No-op otherwise. Auto-applied — Progress is a passive static effect.</summary>
    public static void TryRegister(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HeadlessAttackState attack = context.AttackController.Current;
        if (!attack.IsPending ||
            attack.AttackerId is not HeadlessEntityId attackerId ||
            attack.AttackingPlayerId is not HeadlessPlayerId owner ||
            !HasProgress(context, attackerId))
        {
            return;
        }

        var effectId = new HeadlessEntityId($"{attackerId.Value}:progress");
        if (context.EffectRegistry.HasEffect(effectId))
        {
            return;   // already active this attack
        }

        // (structure-1:1) Progress immunity = "this attacker is unaffected by the opponent's effects during the
        // attack". Emit the canonical joint predicate ContinuousImmunityGate reads on the context path, so it works
        // in real play (not only the context-less unit path): protected == this attacker AND the causing effect is
        // opponent-sourced.
        HeadlessEntityId protectedId = attackerId;
        var effectContext = new EffectContext(
            owner,
            owner,
            attackerId,
            triggerEntityId: null,
            targetEntityIds: new[] { attackerId },
            values: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [ContinuousImmunityGate.JointPredicateKey] =
                    (Func<Assets.Scripts.Script.CardEffectCommons.CardSource, Assets.Scripts.Script.CardEffectCommons.CardSource, bool>)(
                        (protectedCard, causeSource) => protectedCard.InstanceId == protectedId && causeSource.Owner != protectedCard.Owner),
            });

        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(effectId, owner, "Continuous", effectContext),
            keywords: new[] { "Progress" },
            EffectQueryRole.Continuous,
            new[] { ContinuousImmunityGate.Scope },
            effect: null,
            duration: EffectDuration.UntilEndAttack));
    }

    private static bool HasProgress(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) || attacker is null)
        {
            return false;
        }

        if (ReadFlag(attacker.Metadata, HasProgressKey)
            || ContinuousKeywordGate.HasKeyword(context, attackerId, ContinuousKeywordGate.Progress)) // GR-005 C-group seal
        {
            return true;
        }

        return context.CardRepository.TryGetCard(attacker.DefinitionId, out CardRecord? card) &&
            card is not null &&
            ReadFlag(card.Metadata, HasProgressKey);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
}
