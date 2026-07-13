// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_053.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass):
//   [Your Turn] When you play a level 3 yellow Digimon, if this Digimon is suspended, trigger <Draw 1>
//   (Draw 1 card from your deck).
// AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone.
//   CanUseCondition   = IsExistOnBattleArea(card) && IsOwnerTurn(card)
//                        && CanTriggerOnPermanentPlay(hashtable, PermanentCondition)
//   PermanentCondition(permanent) = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
//                        && permanent.TopCard.CardColors.Contains(Yellow) && permanent.TopCard.HasLevel
//                        && permanent.TopCard.Level == 3
//   CanActivateCondition = IsExistOnBattleArea(card) && card.PermanentOfThisCard().IsSuspended
//                        && card.Owner.LibraryCards.Count >= 1
//   ORDER = -1 (no once-per-turn cap) -> maxCountPerTurn: null. ISOPTIONAL = false.
//   ActivateCoroutine = DrawClass(card.Owner, 1, activateClass).Draw() -> DrawBody(1).
// Headless mirror: CanTriggerOnPermanentPlay(ctx, card, PermanentCondition) evaluates the entered permanent
// (the headless Permanent view backed by the engine, PRIM-W5-0) exactly like AS-IS; the suspended check reads
// the top card of this card's own permanent (PermanentOfThisCard().TopInstanceId) via the verbatim IsSuspended
// mirror; the library count check reads the owner's Library zone directly.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_053 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.TopCard.HasCardColor("Yellow")
                && permanent.TopCard.HasLevel
                && permanent.TopCard.Level == 3;

            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsSuspended(card, card.PermanentOfThisCard().TopInstanceId)
                && ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: ctx =>
                    CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerOnPermanentPlay(ctx, card, PermanentCondition),
                canActivate: CanActivate,
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Your Turn] When you play a level 3 yellow Digimon, if this Digimon is suspended, trigger <Draw 1> (Draw 1 card from your deck)."));
        }

        return cardEffects;
    }
}
