// TEST FIXTURE (not a real card). A uniform activated effect whose two gate halves are INDEPENDENTLY toggleable
// via metadata flags on its own card instance: canUse (the AS-IS CanUseCondition / CanTrigger half) reads
// "tfxGateUse", canActivate (the AS-IS CanActivateCondition / CanActivate half) reads "tfxGateAct" (both default
// true when absent). Pins the RDx-A3 predicate SPLIT: the window's COLLECT gate (CanCollectAt) evaluates ONLY the
// canUse half + cap (AS-IS CanTrigger, evaluated once at GetSkillInfos), while the PER-PASS gate (CanActivateAt)
// evaluates ONLY the canActivate half + cap (AS-IS MultipleSkills re-checks CanActivate alone on stacked skills —
// a stacked skill whose CanUseCondition lapsed mid-window STAYS resolvable, ST4_14). Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSplitGateDraw : CEntity_Effect
{
    public const string GateUseKey = "tfxGateUse";
    public const string GateActKey = "tfxGateAct";

    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: _ => ReadFlag(card, GateUseKey),
                canActivate: () => ReadFlag(card, GateActKey),
                body: new DrawBody(1),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Once Per Turn] Draw 1 (split-gate fixture)."));
        }

        return effects;
    }

    private static bool ReadFlag(CardSource card, string key) =>
        !card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? inst)
        || inst is null
        || !inst.Metadata.TryGetValue(key, out object? raw)
        || raw is not false;
}
