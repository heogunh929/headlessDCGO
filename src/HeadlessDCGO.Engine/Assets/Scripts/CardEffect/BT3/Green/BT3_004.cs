// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_004.cs — a Digimon.
// 1:1 mirror of the original BT3_004.
//   [When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn.
//   AS-IS: ActivateClass on EffectTiming.OnAllyAttack, CanUseCondition = CanTriggerOnAttack(hashtable, card)
//   && GManager.instance.attackProcess.DefendingPermanent != null (the attack targets a Digimon, not the
//   player directly), CanActivateCondition = IsExistOnBattleArea(card), ORDER=-1 (maxCountPerTurn:null),
//   ISOPTIONAL=false. ActivateCoroutine: unconditional CardEffectCommons.ChangeDigimonDP(targetPermanent:
//   card.PermanentOfThisCard(), changeValue: 1000, EffectDuration.UntilEachTurnEnd) — no select, self only.
//   AS-IS also sets SetIsInheritedEffect(true); the uniform ActivatedEffect primitive does not model
//   inherited-effect (buried-under-digivolution) firing — same accepted gap as the BT1_049 sibling port.
// Headless mirror: uniform ActivatedEffect whose body is ApplyToAllMatchingBody restricted to a match set of
// exactly this card's own permanent (the AS-IS "no select, direct self mutation" shape — same "no-select
// apply" body class used by BT1_101's foreach-all pattern, here degenerating to a singleton match). "attacks
// an opponent's Digimon" (DefendingPermanent != null) is the headless AttackController.Current.IsDirectAttack
// flag negated — the BT1_082 sibling's precedent for the opposite ("attacks a player" / IsDirectAttack) phrasing.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_004 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerOnAttack(ctx, card)
                    && !card.Context.AttackController.Current.IsDirectAttack;

            bool IsSelf(HeadlessEntityId id) => id == card.PermanentOfThisCard().TopInstanceId;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: CanUse,
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new ApplyToAllMatchingBody(
                    match: IsSelf,
                    perTarget: (c, sink, id) => CardEffectCommons.ChangeDigimonDP(
                        new Permanent(c.Context, id, c.Owner), changeValue: 1000, EffectDuration.UntilEachTurnEnd, c)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn."));
        }

        return cardEffects;
    }
}
