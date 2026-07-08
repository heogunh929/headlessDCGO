// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_030.cs — a Digimon, mixed timings.
//   Inherited continuous (timing == EffectTiming.None): while it is your turn, your level 4 or lower Digimon
//     gain <Jamming>. AS-IS: CardEffectFactory.JammingStaticEffect(permanentCondition = own battle-area
//     Digimon && Level<=4 && TopCard.HasLevel, isInheritedEffect:false, condition = IsExistOnBattleArea(card)
//     && IsOwnerTurn(card)). 1:1 mirror below (JammingStaticEffect already exists with this exact shape).
//
//   [When Digivolving] You may play 1 level 4 or lower digivolution card of 1 of your Digimon cards as
//   another Digimon without paying its memory cost. AS-IS: ActivateClass on OnEnterFieldAnyone,
//   CanUseCondition = CanTriggerWhenDigivolving(hashtable, card), CanActivateCondition = IsExistOnBattleArea
//   && HasMatchConditionPermanent(own battle-area permanent with >=1 digivolution card matching
//   CanSelectCardCondition), ORDER=-1, ISOPTIONAL=true ("you may"). ActivateCoroutine: (1) SelectPermanentEffect
//   (Mode.Custom, maxCount=Min(1,count), canNoSelect:true) picks ONE of the owner's battle-area permanents
//   that has a qualifying digivolution card; (2) SelectCardEffect(Mode.Custom, root=Custom,
//   customRootCardList: THAT permanent's DigivolutionCards, canNoSelect:true) picks ONE digivolution card
//   from the FIRST selection's own under-stack (Digimon, owner, Level<=4, CanPlayAsNewPermanent(payCost:false),
//   HasLevel); (3) CardEffectCommons.PlayPermanentCards(payCost:false) plays it as a brand-new permanent.
//
// STOP: no headless activated-effect primitive composes "select a permanent, THEN select one of THAT
// permanent's OWN digivolution cards (a candidate pool that depends on the first selection), THEN play it as
// a new permanent for free". Grepped (2x) every Select* factory in CardPortingFramework.cs —
// SelectAndPlayFromZoneEffect/SelectAndDigivolveEffect/SelectAndDeDigivolveEffect/
// SelectAndTrashDigivolutionEffect/SelectAndBuffDpEffect/SelectAndRestrictEffect/SelectAndBounce*/
// SelectAndReturnToDeckEffect/SelectAndPutSecurityEffect/SelectAndAddToHandFromZoneEffect/
// SelectAndTrashFromZoneEffect — every one resolves against ONE flat, already-known candidate pool (a fixed
// zone or the battle area), never a SECOND select whose pool is derived from the first selection's own
// state. HasMatchConditionPermanentDigivolutionCards (CardPortingFramework.cs:9286) only queries THIS card's
// OWN permanent's digivolution sources, not an externally-selected permanent's — so even the CanActivate
// guard's shape (any OTHER permanent with a qualifying under-card) has no id-based query counterpart, let
// alone the nested select+play itself. Per rule 4 this is a genuine primitive gap (dependent-pool nested
// select), engine-file work out of scope for a single-card porting pass. No cardEffects registered for
// WhenDigivolving. — 강모델
// if (timing == EffectTiming.WhenDigivolving) { ... }
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_030 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool CanUseCondition() =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);

            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.Level <= 4
                && permanent.TopCard.HasLevel;

            cardEffects.Add(CardEffectFactory.JammingStaticEffect(
                permanentCondition: PermanentCondition,
                isInheritedEffect: false,
                card: card,
                condition: CanUseCondition));
        }

        return cardEffects;
    }
}
