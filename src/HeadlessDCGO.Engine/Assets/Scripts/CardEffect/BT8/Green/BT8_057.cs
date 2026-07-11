// (E-3 witness) BT8_057 (BT8/Green Digimon). AS-IS BT8/Green/BT8_057.cs.
//   [None] "[All Turns] While all of your Digimon are suspended, during your opponent's turn, they can't use
//          Option cards."  -> CanNotPlayClass (continuous ICanNotPlayCardEffect), ported 1:1 as a FIELD-static
//          ContinuousCanNotPlayOptionEffect scanned by CanNotPlayOptionScan (this goal's witness).
//   [Your Turn] OnUnTappedAnyone "when this Digimon becomes unsuspended during your unsuspend phase, trash the
//          top card of your opponent's security stack."  -> ActivatedEffect(OnUnTappedAnyone) mirroring ST4_11's
//          OnEndBattle shape: CanUse = host IsExistOnBattleArea && IsOwnerTurn && IsUnsuspendPhase (AS-IS
//          TurnPhase == phase.Active) && CanTriggerWhenPermanentUnsuspends(self permanent); CanActivate = host on
//          field && opponent has >= 1 security; body = TrashSecurityBody(opponent, 1, fromTop: true); no cap
//          (AS-IS ORDER=-1), non-optional. (E3-P1-2 remediation: STOP E3-01 was unjust — all three primitives
//          exist. The phase flow's natural unsuspend now also emits OnUntapped, so the [Your Turn] branch fires.)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT8_057 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            // AS-IS BT8_057.cs:17-56 — CanNotPlayClass. CanUseCondition = host IsExistOnBattleArea && ALL of the
            // owner's battle-area Digimon are suspended (count == suspended-count) && it is the opponent's turn;
            // CardCondition(source) = source.Owner == card.Owner.Enemy && source.IsOption. Ported as a FIELD
            // static (auto-registered at enter-play) whose CanUse rides the ConditionKey gate and whose
            // CardCondition is the option-play joint.
            cardEffects.Add(CardEffectFactory.CanNotPlayOptionStaticEffect(
                cardCondition: CardCondition,
                isInheritedEffect: false,
                card: card,
                condition: CanUseCondition));

            bool CanUseCondition()
            {
                if (!CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return false;
                }

                // AS-IS: card.Owner.GetBattleAreaDigimons().Count == card.Owner.GetBattleAreaDigimons()
                // .Count(p => p.IsSuspended) — every one of the owner's battle-area Digimon is suspended.
                int digimon = CardEffectCommons.MatchConditionOwnersPermanentCount(card, p => p.IsDigimon);
                int suspended = CardEffectCommons.MatchConditionOwnersPermanentCount(card, p => p.IsDigimon && p.IsSuspended);
                if (digimon != suspended)
                {
                    return false;
                }

                return CardEffectCommons.IsOpponentTurn(card);
            }

            bool CardCondition(CardSource cardSource) =>
                cardSource is not null
                && cardSource.Owner == CardEffectCommons.OpponentOf(card)
                && cardSource.IsOption;
        }

        if (timing == EffectTiming.OnUnTappedAnyone)
        {
            // AS-IS BT8_057.cs:58-109. CanUseCondition = host IsExistOnBattleArea && IsOwnerTurn && TurnPhase ==
            // phase.Active (the unsuspend step) && CanTriggerWhenPermanentUnsuspends(permanent == this card's
            // permanent). The self-permanent condition mirrors ST4_11's WinnerCondition — the unsuspended subject
            // is the top of the stack this card belongs to.
            bool SelfPermanentCondition(Permanent permanent) =>
                card.PermanentOfThisCard() is { TopInstanceId: var top } && !top.IsEmpty && permanent.InstanceId == top;

            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.IsUnsuspendPhase(card)
                && CardEffectCommons.CanTriggerWhenPermanentUnsuspends(ctx, card, SelfPermanentCondition);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnUnTappedAnyone,
                canUse: CanUse,
                // AS-IS CanActivateCondition: host on battle area && opponent security count >= 1.
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.OpponentSecurityCount(card) >= 1,
                body: new TrashSecurityBody(CardEffectCommons.OpponentOf(card), 1, fromTop: true),
                maxCountPerTurn: null, // AS-IS ORDER = -1 (uncapped).
                isOptional: false,
                description: "[Your Turn] When this Digimon becomes unsuspended during your unsuspend phase, trash the top card of your opponent's security stack."));
        }

        return cardEffects;
    }
}
