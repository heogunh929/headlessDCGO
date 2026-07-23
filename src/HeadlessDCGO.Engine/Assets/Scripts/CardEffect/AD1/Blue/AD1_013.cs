// Source: DCGO/Assets/Scripts/CardEffect/AD1/Blue/AD1_013.cs
// TRUE AS-IS-verbatim port (ZeigGreymon, AD1/Blue). WITNESS card for CardEffectCommons.IsMinDigivolutionCards
// (the min-digivolution-cards predicate) — AD1_013 is its SOLE AS-IS caller (relocated in the skeleton campaign).
// All six timing blocks:
//   [None]   AddSelfDigivolutionRequirementStaticEffect (alt digivolve: Lv.5 [Blue Flare]/[Xros Heart], cost 3);
//            RebootSelfStaticEffect; BlockerSelfStaticEffect (the ported CardEffectFactory kind-classes, verbatim).
//   [On Play]/[When Digivolving] (OnEnterFieldAnyone) — delete 1 opponent Digimon with the FEWEST digivolution
//            cards (IsMinDigivolutionCards gate + SelectPermanentEffect Mode.Destroy). The witnessed half.
//   [All Turns] (WhenRemoveField, not for DigiXros) — play 1 Lv.5- [Blue Flare]/[Xros Heart] Digimon from this
//            Digimon's digivolution cards for free.
//   [None]   ESS ChangeSelfDPStaticEffect +1000 per distinct digivolution-card colour.
//
// Substrate translation only (established idioms): IEnumerator→async Task, `ContinuousController.instance.
// StartCoroutine(X)`→`await X`; the AS-IS Func<Permanent,bool> IsMinDigivolutionCards target predicate is kept
// verbatim on the canonical Func<Permanent,bool> shape, feeding both HasMatchConditionOpponentsPermanent and
// SelectPermanentEffect.SetUp's canTargetCondition (id-flip 3b) — `card.Owner.Enemy` (Player) →
// `CardEffectCommons.OpponentOf(card)` (HeadlessPlayerId, the enemy that owns the target permanent);
// `Some`→`Any`; `CanTrigger*/IsLeavingForDigiXros(hashtable, …)` → the Hashtable overloads;
// `card.PermanentOfThisCard()` (returns the lighter mirror PermanentView) → `ICardEffect.ResolvePermanentOfThisCard(
// card)` (the full Permanent bridge, BT9_109 idiom); its `DigivolutionCards` (List) → the IReadOnlyList view
// (`.ToList()` where a List is required).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Blue;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class AD1_013 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternative Digivolve Condition

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Blue Flare")
                        || targetPermanent.TopCard.EqualsTraits("Xros Heart");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(level: 5, permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Reboot
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Blocker
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Shared OP / WD

        string SharedEffectName = "Delete an Opponent's Digimon with the fewest sources";

        string SharedEffectDescription(string tag) => $"[{tag}] Delete 1 of your opponent's Digimon with the fewest digivolution cards.";

        // AS-IS :50 `IsMinDigivolutionCards(permanent) => CardEffectCommons.IsMinDigivolutionCards(permanent,
        // card.Owner.Enemy)` — the single Permanent-shape predicate feeds both HasMatchConditionOpponentsPermanent
        // and SelectPermanentEffect.SetUp's canonical canTargetCondition (id-flip 3b).
        bool IsMinDigivolutionCards(Permanent permanent) =>
            CardEffectCommons.IsMinDigivolutionCards(permanent, CardEffectCommons.OpponentOf(card));

        bool SharedCanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, IsMinDigivolutionCards))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: IsMinDigivolutionCards,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }
        }
        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hashtable => SharedActivateCoroutine(hashtable, activateClass), -1, false, SharedEffectDescription("On Play"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }
        }
        #endregion

        #region When Digivolving
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hashtable => SharedActivateCoroutine(hashtable, activateClass), -1, false, SharedEffectDescription("When Digivolving"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 level 5 or lower [Blue Flare] or [Xros Heart] Digimon from this Digimon's digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[All Turns] When this Digimon would leave the battle area other than by DigiXros, you may play 1 level 5 or lower [Blue Flare] or [Xros Heart] trait Digimon card from its digivolution cards without paying the cost.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card)
                    && !CardEffectCommons.IsLeavingForDigiXros(hashtable);
            }

            bool CanSelectSourceCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && cardSource.HasLevel && cardSource.Level <= 5
                    && (cardSource.EqualsTraits("Blue Flare") || cardSource.EqualsTraits("Xros Heart"))
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Any(CanSelectSourceCardCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: CanSelectSourceCardCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 digivolution card to play.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.DigivolutionCards,
                    customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage(
                    "Select 1 digivolution card to play.",
                    "The opponent is selecting 1 digivolution card to play.");
                selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectCardEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                if (selectedCards.Count > 0)
                {
                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards,
                        activateClass: activateClass,
                        payCost: false,
                        isTapped: false,
                        root: SelectCardEffect.Root.DigivolutionCards,
                        activateETB: true);
                }
            }
        }
        #endregion

        #region All Turns - ESS
        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && (ICardEffect.ResolvePermanentOfThisCard(card).TopCard.EqualsTraits("Blue Flare")
                        || ICardEffect.ResolvePermanentOfThisCard(card).TopCard.EqualsTraits("Xros Heart"));
            }

            int changeDP()
            {
                return 1000 * ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCardsColors.Count;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: (Func<int>)changeDP,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }
        #endregion

        return cardEffects;
    }
}
