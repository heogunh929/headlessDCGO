// 1:1 mirror of the original BT1_001 (BT1/Red).
//   [When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn.
//   AS-IS: ActivateClass on EffectTiming.OnAllyAttack, SetIsInheritedEffect(true), CanUseCondition =
//   CanTriggerOnAttack(hashtable, card) && GManager.instance.attackProcess.DefendingPermanent != null (the
//   attack targets an opponent's Digimon, not the player directly), CanActivateCondition =
//   IsExistOnBattleArea(card), ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false, ActivateCoroutine =
//   ChangeDigimonDP(targetPermanent: card.PermanentOfThisCard(), changeValue: 1000,
//   effectDuration: UntilEachTurnEnd) — a fixed SELF-target buff, no player selection.
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with isInheritedEffect: true and a
//   local non-interactive body (SelfDpDeltaBody) applying CardEffectCommons.ChangeDigimonDP to this card's own
//   permanent. "DefendingPermanent != null" is the headless AttackController.Current.IsDirectAttack flag,
//   negated (BT1_082's sibling uses the same flag for the opposite "== null" phrasing).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_001 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            const string description =
                "[When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn.";

            // AS-IS CanUseCondition: CanTriggerOnAttack(hashtable, card) && attackProcess.DefendingPermanent != null.
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerOnAttack(ctx, card)
                && !card.Context.AttackController.Current.IsDirectAttack;

            // AS-IS CanActivateCondition: IsExistOnBattleArea(card).
            bool CanActivate() => CardEffectCommons.IsExistOnBattleArea(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SelfDpDeltaBody(1000),
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
