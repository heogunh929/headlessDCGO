// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Black/BT25_075.cs (Vulcanusmon, Digimon / Black, 5 regions)
//    * Alt Digivolution   :16-26  (None — AddSelfDigivolutionRequirementStaticEffect, [TS] level5 cost3 ignore)
//    * Reduce Play Cost   :28-38  (None — MandatorySelfPlayCostReduction 5, 자기 배틀 Digimon 수 < 상대 시)
//    * Shared OP / WD     :40-225 (ActivateClassesForSharedEffects — hand/trash 최대 2장 free link → 링크 카드 수만큼
//        상대 전체 <De-Digivolve 1>)
//    * All Turns          :227-242(None — RushStaticEffect + ChangeLinkMaxStaticEffect(+1), 자기 [TS] Digimon 대상,
//        card 배틀에어리어 상주 시)  ← 증인 표적(inert-grant 검증: LinkedMax +1 실착지)
//    * Your Turn          :244-295(WhenLinked — 링크된 자기 Digimon 1체 즉시 공격 offer)
//
// ② 프리미티브 매핑: P:AddSelfDigivolutionRequirementStaticEffect, P:MandatorySelfPlayCostReduction,
//    P:ActivateClassesForSharedEffects, P:ILinkCard, P:IMassDegeneration, P:RushStaticEffect,
//    **P:ChangeLinkMaxStaticEffect (ChangeLinkMaxClass; read-side = Permanent.LinkedMax / ModifierHelpers link fold)**,
//    T:WhenLinked (SelectAttackEffect).
//
// ③ 배선 관례: Alt-digivolve/Reduce-cost/All-turns → None. Shared → ActivateClassesForSharedEffects(onPlay:true,
//    whenDigivolving:true) 내부 게이팅. [Your Turn] WhenLinked → WhenLinked 키 + CanTriggerWhenLinked.
//
// 치환(substrate translations only):
//    * `card.Owner.HandCards/TrashCards/GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner).*`.
//    * `card.Owner.Enemy.GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons()` (BT18_042 확립).
//    * `HasMatchConditionPermanent(cond)` → `HasMatchConditionPermanent(card, cond)`; SelectPermanentEffect.SetUp
//      canTargetCondition = id-형 어댑터 (BT9_109 확립).
//    * `new IMassDegeneration(perms, 1, activateClass)` → `new IMassDegeneration(perms, 1,
//      activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass)` (IDegeneration 확립 idiom).
//    * `new ILinkCard(false, cardSource, permanent, activateClass)` — 미러 ctor 1:1.
//    * IEnumerator→async Task; `yield return (Continuous/Start)Coroutine(X)`→`await X`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Black;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_075 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alt Digivolution
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return permanent.TopCard.EqualsTraits("TS");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(PermanentCondition, 3, true, card, null, level: 5));
        }
        #endregion

        #region Reduce Play Cost
        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return new Player(card.Context, card.Owner).GetBattleAreaDigimons().Count < new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Count;
            }

            cardEffects.Add(CardEffectFactory.MandatorySelfPlayCostReduction(5, card, Condition));
        }
        #endregion

        #region Shared OP / WD

        string SharedEffectName = "May link up to 2 cards from hand/trash to your digimon for free. Then <De-Digivolve 1> all enemy Digimon per your link card";

        string SharedEffectDescription(string tag)
            => $"[{tag}] You may link up to 2 cards from your hand or trash to any of your Digimon without paying the cost. Then, for each of your link cards, <De-Digivolve 1> all of your opponent's Digimon.";

        bool CanLinkCardCondition(CardSource cardSource) => cardSource.CanLink(false);

        CardEffectFactory.ActivateClassesForSharedEffects
            (ref cardEffects, timing, card,
                SharedEffectName,
                SharedActivateCoroutine,
                SharedEffectDescription,
                optional: false,
                whenDigivolving: true,
                onPlay: true);

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            #region Link 2 cards
            int toLink = Math.Min(2, new Player(card.Context, card.Owner).HandCards.Count(CanLinkCardCondition) + new Player(card.Context, card.Owner).TrashCards.Count(CanLinkCardCondition));
            while (toLink > 0)
            {
                int validHandCardCount = new Player(card.Context, card.Owner).HandCards.Count(CanLinkCardCondition);
                int validTrashCardCount = new Player(card.Context, card.Owner).TrashCards.Count(CanLinkCardCondition);

                if (validHandCardCount > 0 && validTrashCardCount > 0)
                {
                    List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>()
                    {
                        new(message: "from Hand", value: 1, spriteIndex: 0),
                        new(message: "from Trash", value: 2, spriteIndex: 0),
                        new(message: "Do not link", value: 3, spriteIndex: 1)
                    };

                    GManager.instance.userSelectionManager.SetIntSelection(
                        selectionElements: selectionElements,
                        selectPlayer: card.Owner,
                        selectPlayerMessage: "From which area will you link a card?",
                        notSelectPlayerMessage: "The opponent is choosing from which area to link card.");
                }
                else
                {
                    GManager.instance.userSelectionManager.SetInt(validHandCardCount > 0 ? 1 : 2);
                }

                await GManager.instance.userSelectionManager.WaitForEndSelect();

                if (GManager.instance.userSelectionManager.SelectedIntValue == 3)
                {
                    break;
                }
                if (GManager.instance.userSelectionManager.SelectedIntValue == 1)
                {
                    int maxCount = Math.Min(toLink, validHandCardCount);
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanLinkCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: true,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    string messagePluralize = maxCount > 1 ? "Select one or more cards to link to 1 Digimon. You will be able to select a second link card and second Digimon target if you only select 1 card now." : "Select a card to link to 1 Digimon.";

                    selectHandEffect.SetUpCustomMessage(
                        messagePluralize,
                        $"The opponent is selecting cards to link.");

                    await selectHandEffect.Activate();
                }
                else
                {
                    int maxCount = Math.Min(toLink, validTrashCardCount);
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanLinkCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        message: "Select link card(s)",
                        maxCount: maxCount,
                        canEndNotMax: true,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    string messagePluralize = maxCount > 1 ? "Select one or more cards to link to 1 Digimon. You will be able to select a second link card and second Digimon target if you only select 1 card now." : "Select a card to link to 1 Digimon.";

                    selectCardEffect.SetUpCustomMessage(
                        messagePluralize,
                        $"The opponent is selecting cards to link.");

                    await selectCardEffect.Activate();
                }

                async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                {
                    if (cardSources.Count == 0)
                    {
                        toLink = 0;
                    }
                    else
                    {
                        bool CanLinkPermanentCondition(Permanent permanent)
                        {
                            return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                                && cardSources.All(cardSource => cardSource.CanLinkToTargetPermanent(permanent, false));
                        }

                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanLinkPermanentCondition))
                        {
                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanLinkPermanentCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: 1,
                                canNoSelect: true,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            string choicePluralize = cardSources.Count > 1 ? "cards" : "card";

                            selectPermanentEffect.SetUpCustomMessage($"Select 1 Digimon to link the chosen {choicePluralize}.", "The opponent is selecting 1 Digimon to link.");
                            await selectPermanentEffect.Activate();

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                foreach (CardSource cardSource in cardSources)
                                    await new ILinkCard(false, cardSource, permanent, activateClass).LinkCard();

                                toLink -= cardSources.Count;
                            }
                        }
                        else
                        {
                            List<SelectionElement<int>> selectionElements1 = new List<SelectionElement<int>>()
                            {
                                new(message: "Ok", value: 1, spriteIndex: 1)
                            };

                            GManager.instance.userSelectionManager.SetIntSelection(
                                selectionElements: selectionElements1,
                                selectPlayer: card.Owner,
                                selectPlayerMessage: "The cards you chose do not have a valid digimon which could link both. Try choosing 1 at a time.",
                                notSelectPlayerMessage: "The opponent is selecting cards to link.");

                            await GManager.instance.userSelectionManager.WaitForEndSelect();
                        }
                    }
                }
            }
            #endregion

            #region De-Digivolve
            int degenerationCount = new Player(card.Context, card.Owner).GetBattleAreaDigimons().Map(permanent => permanent.LinkedCards).Flat().Count();
            for (int i = 0; i < degenerationCount; i++)
            {
                await new IMassDegeneration(new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons(), 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
            }
            #endregion
        }

        #endregion

        #region All Turns
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.TopCard.HasTSTraits;
            }

            bool Condition() => CardEffectCommons.IsExistOnBattleArea(card);

            cardEffects.Add(CardEffectFactory.RushStaticEffect(PermanentCondition, false, card, Condition));

            cardEffects.Add(CardEffectFactory.ChangeLinkMaxStaticEffect(PermanentCondition, 1, false, card, Condition));
        }
        #endregion

        #region Your Turn
        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Linked digimon may Attack", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSkippable(true);
            activateClass.SetEffectTargets(TargetablePermanents);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Your Turn] When your Digimon get linked, one of them may attack.";
            }

            bool PermanentCondition(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) && permanent.CanAttack(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerWhenLinked(hashtable, PermanentCondition, null);
            }

            Permanent GetAttacker(Hashtable hashtable) => CardEffectCommons.GetPermanentFromHashtable(hashtable);

            List<Permanent> TargetablePermanents(Hashtable hashtable) => new List<Permanent>() { GetAttacker(hashtable) };

            bool CanActivateCondition(Hashtable hashtable)
            {
                activateClass.SetEffectName($"{GetAttacker(hashtable).TopCard.BaseENGCardNameFromEntity} may attack");
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent attacker = GetAttacker(hashtable);
                if (attacker != null && attacker.TopCard != null)
                {
                    SelectAttackEffect selectAttackEffect = GManager.instance.GetComponent<SelectAttackEffect>();

                    selectAttackEffect.SetUp(
                        attacker: attacker,
                        canAttackPlayerCondition: () => true,
                        defenderCondition: (permanent) => true,
                        cardEffect: activateClass);

                    await selectAttackEffect.Activate();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
