// 1:1 mirror of the original BT1_012 (BT1/Red) — Biyomon.
//   [Your Turn] When this Digimon is blocked, it gets +2000 DP.
//   AS-IS: ActivateClass on EffectTiming.OnBlockAnyone, SetIsInheritedEffect(true), CanUseCondition =
//   CanTriggerOnAttack(hashtable, card) && IsOwnerTurn(card), CanActivateCondition = IsExistOnBattleArea(card),
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, ActivateCoroutine = ChangeDigimonDP(targetPermanent:
//   card.PermanentOfThisCard(), changeValue: 2000, effectDuration: UntilEachTurnEnd) — a fixed SELF-target
//   buff, no player selection.
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with isInheritedEffect: true and a
//   local non-interactive body (SelfDpDeltaBody) applying CardEffectCommons.ChangeDigimonDP to this card's own
//   permanent (same shape as the BT1_001 sibling). The prior mirror-invented SelfDpBuffTriggerEffect factory
//   has no isInheritedEffect support (TriggeredSelfDpBuffEffect carries no such flag), silently dropping the
//   AS-IS SetIsInheritedEffect(true) — fixed here via the raw uniform ActivatedEffect, which does.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_012 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnBlockAnyone)
        {
            const string description = "[Your Turn] When this Digimon is blocked, it gets +2000 DP.";

            // AS-IS CanUseCondition: CanTriggerOnAttack(hashtable, card) && IsOwnerTurn(card).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerOnAttack(ctx, card) && CardEffectCommons.IsOwnerTurn(card);

            // AS-IS CanActivateCondition: IsExistOnBattleArea(card).
            bool CanActivate() => CardEffectCommons.IsExistOnBattleArea(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnBlockAnyone,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SelfDpDeltaBody(2000),
                maxCountPerTurn: null,
                isOptional: false,
                description: description,
                isInheritedEffect: true)); // AS-IS SetIsInheritedEffect(true)
        }

        return cardEffects;
    }

    // AS-IS ActivateCoroutine: ChangeDigimonDP(card.PermanentOfThisCard(), changeValue, UntilEachTurnEnd) — a
    // fixed self-target buff, no player selection. Non-interactive.
    private sealed class SelfDpDeltaBody : IEffectBody
    {
        private readonly int _changeValue;

        public SelfDpDeltaBody(int changeValue) => _changeValue = changeValue;

        public bool IsInteractive => false;

        public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

        public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected) =>
            CardEffectCommons.ChangeDigimonDP(
                ICardEffect.ResolvePermanentOfThisCard(card), _changeValue, EffectDuration.UntilEachTurnEnd, card);
    }
}
