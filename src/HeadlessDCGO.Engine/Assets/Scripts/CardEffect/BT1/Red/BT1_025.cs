// 1:1 mirror of the original BT1_025 (BT1/Red).
//   [When Digivolving] This Digimon gains <Security Attack +1> (This Digimon checks 1 additional security
//                       card) for the turn.
//   AS-IS: ActivateClass on EffectTiming.WhenDigivolving, CanUseCondition = CanTriggerWhenDigivolving(hashtable,
//   card), CanActivateCondition = IsExistOnBattleArea(card), ORDER=-1, ISOPTIONAL=false, ActivateCoroutine =
//   ChangeDigimonSAttack(targetPermanent: card.PermanentOfThisCard(), changeValue: 1, UntilEachTurnEnd) — a
//   fixed SELF-target buff, no player selection (the prior mirror wrongly modelled this as a select-1-owner-
//   Digimon buff via SelectAndBuffSAttackEffect; fixed here to self-only).
//
//   [All Turns] "Ignore Security Effect": a SECOND, independent effect — timing None, DisableEffectClass.
//   SetUpDisableEffectClass(InvalidateCondition), InvalidateCondition(cardEffect) = IsExistOnBattleArea(card)
//   && IsOwnerTurn(card) && cardEffect?.EffectSourceCard?.IsOption == true && cardEffect.IsSecurityEffect &&
//   attackProcess.AttackingPermanent == card.PermanentOfThisCard() — while this Digimon is the attacker on its
//   owner's turn, negate any Option-card-sourced [Security] effect. (The prior mirror fabricated an unrelated
//   <Jamming> keyword grant here that has no AS-IS analog for this card — removed.)
//   Headless mirror: [When Digivolving] = the uniform ActivatedEffect (= AS-IS ActivateClass) with explicit
//   CanUse/CanActivate gates and a local non-interactive self-buff body. "Ignore Security Effect" = the ported
//   DisableEffectClass kind-class, ported verbatim (see docs/audit/rebuild_p5_cards_missing.md for the
//   dispatch-wiring caveat: DisableEffectClass/CheckEffectDisabledClass gates the LEGACY ICardEffect.CanUse
//   path; whether it is consulted by the newer ActivatedEffect-based [Security] resolution used for most ported
//   Option cards is unconfirmed).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_025 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            const string description = "[When Digivolving] This Digimon gains <Security Attack +1> (This Digimon checks 1 additional security card) for the turn.";

            // AS-IS CanUseCondition: CanTriggerWhenDigivolving(hashtable, card).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerWhenDigivolving(ctx, card);

            // AS-IS CanActivateCondition: IsExistOnBattleArea(card).
            bool CanActivate() => CardEffectCommons.IsExistOnBattleArea(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenDigivolving,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SelfSAttackDeltaBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        if (timing == EffectTiming.None)
        {
            // AS-IS: DisableEffectClass invalidationClass; SetUpICardEffect("Ignore Security Effect",
            // CanUseCondition, card); SetUpDisableEffectClass(InvalidateCondition).
            var invalidationClass = new DisableEffectClass();
            invalidationClass.SetUpICardEffect("Ignore Security Effect", CanUseCondition, card);
            invalidationClass.SetUpDisableEffectClass(InvalidateCondition);
            cardEffects.Add(invalidationClass);

            bool CanUseCondition(Hashtable hashtable) => true;

            bool InvalidateCondition(ICardEffect cardEffect)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (cardEffect != null)
                        {
                            if (cardEffect.EffectSourceCard != null)
                            {
                                if (cardEffect.EffectSourceCard.IsOption)
                                {
                                    if (cardEffect.IsSecurityEffect)
                                    {
                                        if (card.Context.AttackController.Current.AttackerId
                                            == ICardEffect.ResolvePermanentOfThisCard(card).TopInstanceId)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }
        }

        return cardEffects;
    }

    // AS-IS ActivateCoroutine: ChangeDigimonSAttack(card.PermanentOfThisCard(), changeValue, UntilEachTurnEnd)
    // — a fixed self-target buff, no player selection. Non-interactive.
    private sealed class SelfSAttackDeltaBody : IEffectBody
    {
        private readonly int _changeValue;

        public SelfSAttackDeltaBody(int changeValue) => _changeValue = changeValue;

        public bool IsInteractive => false;

        public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

        public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected) =>
            CardEffectCommons.ChangeDigimonSAttack(
                ICardEffect.ResolvePermanentOfThisCard(card), _changeValue, EffectDuration.UntilEachTurnEnd, card);
    }
}
