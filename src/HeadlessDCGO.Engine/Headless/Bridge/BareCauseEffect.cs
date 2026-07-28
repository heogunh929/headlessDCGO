// (C3 SUBSTRATE→HEADLESS) Relocated verbatim from Assets/Scripts/Script/CardEffectCommons/
// ContinuousAndRestrictionEffects.cs to the Headless/Bridge substrate. BareCauseEffect is an invented minimal
// cause-carrier (no AS-IS analogue as a standalone type — AS-IS threads the real causing ICardEffect or null); it
// belongs beside its documented peer ActivatedHashtableBridge.CauseStub in the substrate bridge layer, not in the
// mirror game-rule namespace. Body is byte-identical to the pre-move source.

namespace HeadlessDCGO.Engine.Headless.Bridge;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>(R3-W3c-4) A minimal cause carrier — an <see cref="ICardEffect"/> whose only meaningful data is its
/// <c>EffectSourceCard</c>. Used to route id/source-only substrate consumers (trash-protection filters,
/// stack-trash immunity, …) through the AS-IS live joint-scan getters, every one of which takes the causing
/// <c>ICardEffect</c> and reduces it to its non-null-ness / <c>EffectSourceCard</c> (the same reduction
/// <c>ActivatedHashtableBridge.CauseStub</c> uses for driving-event payloads). The old-model
/// <c>ContinuousTrashProtectionEffect</c> (which lowered this concept into a dead registry binding) is retired:
/// the sole producer today is BT9_109's inline <c>CanNotTrashFromDigivolutionCardsClass</c>, served by the live
/// <see cref="CardSource.CanNotTrashFromDigivolutionCards"/> scan.
///
/// (design item RD-BCE-01) <see cref="For(EngineContext, HeadlessEntityId)"/> collapses to a source-less cause
/// (a fresh BareCauseEffect with no EffectSourceCard) when the id is empty/unresolvable, and both factories always
/// return a NON-null ICardEffect. Some AS-IS restriction/immunity predicates distinguish a null causing effect
/// (a RULE-sourced action, e.g. battle/end-of-turn, which many `CanNotAffect`/`CanNotBeTrashed` conditions treat
/// as "not an opponent effect" ⇒ NOT immune) from a real-but-unknown source. A source-LESS BareCauseEffect is not
/// byte-identical to AS-IS `null`: the getter's own `_cardEffect == null` early-out (CanNotBeAffected :743) is NOT
/// taken, and an IsOpponentEffect check reads `EffectSourceCard?.Owner` = null-owner rather than short-circuiting.
/// In practice the sink/Commons consumers here always carry a real card source (SourceEntityId / sourceCard), so
/// this divergence is latent; revisit if a genuinely rule-sourced (null-cause) mutation is ever routed through a
/// BareCauseEffect gate.</summary>
public sealed class BareCauseEffect : ICardEffect
{
    /// <summary>A bare cause whose <c>EffectSourceCard</c> is <paramref name="sourceCard"/> (the AS-IS collapse of
    /// the causing effect to its source card). Null <paramref name="sourceCard"/> yields a source-less cause.</summary>
    public static BareCauseEffect For(CardSource? sourceCard)
    {
        var stub = new BareCauseEffect();
        if (sourceCard is not null)
        {
            stub.SetEffectSourceCard(sourceCard);
        }

        return stub;
    }

    /// <summary>A bare cause whose <c>EffectSourceCard</c> resolves <paramref name="sourceId"/> to a
    /// <see cref="CardSource"/> (owner read from the repository). Empty id — OR an id that resolves to no live
    /// instance (hence no owner) — yields a source-less cause: a <see cref="CardSource"/> requires a non-empty
    /// controller, and an unresolvable cause matches no narrowed restriction predicate (AS-IS "unknown causing
    /// source does not block a conditional restriction").</summary>
    public static BareCauseEffect For(EngineContext context, HeadlessEntityId sourceId)
    {
        if (sourceId.IsEmpty
            || !(context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? instance) && instance is not null)
            || instance.OwnerId.IsEmpty)
        {
            return new BareCauseEffect();
        }

        return For(new CardSource(context, sourceId, instance.OwnerId, instance.OwnerId));
    }

    /// <summary>Like <see cref="For(EngineContext, HeadlessEntityId)"/> but returns <c>null</c> for an empty /
    /// unresolvable cause instead of a source-less stub. AS-IS threads a NULL causing effect for a rule-sourced
    /// (battle / DP-zero / end-of-turn) or unknown cause, and the immunity getters short-circuit that null to
    /// "not immune" (<c>CardSource.CanNotBeAffected</c>: <c>if (_cardEffect == null) return false</c>;
    /// <c>Permanent.ImmuneFromStackTrashing</c> is only reached with a non-null cause). Use this at
    /// immunity/restriction call sites so the AS-IS null path fires exactly, rather than evaluating the
    /// predicate against a null-owner source-less stub (RD-BCE-01).</summary>
    public static ICardEffect? ForOrNull(EngineContext context, HeadlessEntityId sourceId)
    {
        if (sourceId.IsEmpty
            || !(context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? instance) && instance is not null)
            || instance.OwnerId.IsEmpty)
        {
            return null;
        }

        return For(new CardSource(context, sourceId, instance.OwnerId, instance.OwnerId));
    }
}
