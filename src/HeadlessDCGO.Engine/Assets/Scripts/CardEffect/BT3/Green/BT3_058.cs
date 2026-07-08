// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_058.cs — a Digimon (two timing blocks).
// 1:1 mirror of the original BT3_058.
//   <Security Attack +1>. AS-IS: CardEffectFactory.PierceSelfEffect on OnDetermineDoSecurityCheck — verbatim
//   factory match.
//   [When Attacking] If you attack an opponent's Digimon that has 12000 DP or more, this Digimon gets +7000
//   DP and <Security Attack +2> for the turn.
//   AS-IS: ActivateClass on EffectTiming.OnAllyAttack, CanUseCondition = CanTriggerOnAttack(hashtable, card)
//   && DefendingPermanent != null && DefendingPermanent.TopCard.Owner == card.Owner.Enemy &&
//   DefendingPermanent.IsDigimon && DefendingPermanent.DP >= 12000, CanActivateCondition =
//   IsExistOnBattleArea(card). ORDER=-1, ISOPTIONAL=false. ActivateCoroutine: unconditional (self, no select)
//   ChangeDigimonDP(+7000, UntilEachTurnEnd) then ChangeDigimonSAttack(+2, UntilEachTurnEnd).
// Headless mirror: uniform ActivatedEffect whose body is ApplyToAllMatchingBody restricted to this card's own
// permanent (the same no-select self-mutation shape as the BT3_004 sibling), applying BOTH the DP and SAttack
// deltas per match. The attack-target-is-a-12000+-DP-opponent-Digimon condition reads
// AttackController.Current.TargetId (headless mirror of DefendingPermanent, BT1_082 precedent) and
// CardEffectCommons.CurrentDp for the DP threshold (ST1_15 precedent).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_058 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerOnAttack(ctx, card)
                    && card.Context.AttackController.Current.TargetId is HeadlessEntityId targetId
                    && CardEffectCommons.IsOpponentBattleAreaDigimon(card, targetId)
                    && CardEffectCommons.CurrentDp(card, targetId) >= 12000;

            bool IsSelf(HeadlessEntityId id) => id == card.PermanentOfThisCard().TopInstanceId;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: CanUse,
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new ApplyToAllMatchingBody(
                    match: IsSelf,
                    perTarget: (c, sink, id) =>
                    {
                        var perm = new Permanent(c.Context, id, c.Owner);
                        CardEffectCommons.ChangeDigimonDP(perm, changeValue: 7000, EffectDuration.UntilEachTurnEnd, c);
                        CardEffectCommons.ChangeDigimonSAttack(perm, changeValue: 2, EffectDuration.UntilEachTurnEnd, c);
                    }),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Attacking] If you attack an opponent's Digimon that has 12000 DP or more, this Digimon gets +7000 DP and <Security Attack +2> for the turn."));
        }

        return cardEffects;
    }
}
