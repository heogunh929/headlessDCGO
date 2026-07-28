// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 카드 — Torajiro Asuka (BT22_087, Tamer / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT22/Yellow/BT22_087.cs (231 lines, 3 regions)
//    * [Start of Main Phase] OnStartMainPhase :15-22 (Gain1MemoryTamerOpponentDigimonEffect)
//    * [Your Turn] WhenLinked   :24-217 (자기 Tamer 서스펜드→상대 1체 -2000 DP(턴 끝)→아군 1체를 손패로 app-fuse)
//    * [Security] SecuritySkill  :219-226 (PlaySelfTamerSecurityEffect)
//
// ② 1:1 근거: WhenLinked app-fuse 몸통은 BT25_089(같은 Tamer app-fuse 정본, RD-EXT3-02 해소)의 [End of Turn]
//    팔과 동형 — CanAppFusionFromTargetPermanent/AppFusionConditionOf/PlayCardClass+SetAppFusion/
//    PermanentFrame.FrameID·LinkedCards 표면 재사용, 신규 발명 0.
//
// ③ 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)`/`StartCoroutine(X)`
//      →`await X`; lone `yield return null` 코루틴→Task.CompletedTask.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (ST1_09/BT2_081 판례).
//    * `new SuspendPermanentsClass(list, hashtable).Tap()`(AS-IS 해시테이블 ctor) → `new SuspendPermanentsClass(
//      list, activateClass, isBlock:false).Tap()` (BT8_092/EX4_062 idiom).
//    * `card.Owner.HandCards`(AS-IS live Player) → `new Player(card.Context, card.Owner).HandCards` (BT25_089 판례).
//    * `hand.appFusionCondition` / `selectedCard.appFusionCondition` → `.AppFusionConditionOf()` (BT25_089 판례).
//    * `SelectPermanentEffect` canTargetCondition는 AS-IS Permanent-술어를 직접 받음
//      (BT25_089/BT3_056 판례). Has/Count 스캔(Owners*/Opponents*)도 Permanent-술어를 직접 받음(Vortex 판례).
//    * `selectedPermanent.PermanentFrame.FrameID` → `selectedPermanent.PermanentFrame!.FrameID` (BT25_089 판례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Yellow;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT22_087 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Start of Main Phase

        if (timing == EffectTiming.OnStartMainPhase)
        {
            cardEffects.Add(CardEffectFactory.Gain1MemoryTamerOpponentDigimonEffect(card));
        }

        #endregion

        #region Your Turn

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("-2K DP, then you may app fuse", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When any of your Digimon get linked, by suspending this Tamer, 1 of your opponent's Digimon gets -2000 DP for the turn. Then, 1 of your Digimon may app fuse into a Digimon card in the hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenLinked(hashtable, LinkPermanentCondition, null);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanActivateSuspendCostEffect(card);
            }

            bool LinkPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
            }

            bool IsOpponentDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanSelectPermanent(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    foreach (CardSource hand in new Player(card.Context, card.Owner).HandCards)
                    {
                        if (hand.AppFusionConditionOf() != null)
                        {
                            if (hand.CanAppFusionFromTargetPermanent(permanent, true))
                                return true;
                        }
                    }
                }
                return false;
            }

            bool CanSelectCard(CardSource card, Permanent permanent)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (card.AppFusionConditionOf() != null)
                    {
                        if (card.CanAppFusionFromTargetPermanent(permanent, true))
                            return true;
                    }
                }
                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await new SuspendPermanentsClass(new List<Permanent> { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass, isBlock: false).Tap();

                if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, permanent => IsOpponentDigimon(permanent)))
                {
                    Permanent selectedPermament = null;

                    #region Select Permanent

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOpponentsPermanentCount(card, IsOpponentDigimon));
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOpponentDigimon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermament = permanent;
                        return Task.CompletedTask;
                    }

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to -2K DP", "The opponent is selecting 1 Digimon to -2K DP");
                    await selectPermanentEffect.Activate();

                    #endregion

                    if (selectedPermament != null) await CardEffectCommons.ChangeDigimonDP(
                        targetPermanent: selectedPermament,
                        changeValue: -2000,
                        effectDuration: EffectDuration.UntilEachTurnEnd,
                        activateClass: activateClass
                        );
                }

                if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, CanSelectPermanent))
                {
                    Permanent selectedPermanent = null;

                    #region Select App Fuse Permanent Target

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersPermanentCount(card, CanSelectPermanent));
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanent,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;
                        return Task.CompletedTask;
                    }

                    selectPermanentEffect.SetUpCustomMessage("Select 1 digimon to app fuse", "The opponent is selecting 1 digimon to app fuse");

                    await selectPermanentEffect.Activate();

                    #endregion

                    if (selectedPermanent != null)
                    {
                        CardSource selectedCard = null;

                        #region Select App Fusion Hand Target

                        int maxCount1 = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, handCard => CanSelectCard(handCard, selectedPermanent)));
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();
                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: handCard => CanSelectCard(handCard, selectedPermanent),
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount1,
                            canNoSelect: true,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Custom,
                            cardEffect: activateClass);

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCard = cardSource;
                            return Task.CompletedTask;
                        }

                        selectHandEffect.SetUpCustomMessage("Select digimon to app fuse into.", "The opponent is selecting digimon to app fuse into.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Selected digimon");
                        await selectHandEffect.Activate();

                        #endregion

                        if (selectedCard != null && selectedCard.CanAppFusionFromTargetPermanent(selectedPermanent, true))
                        {
                            CardSource linkCard = selectedPermanent.LinkedCards.Where(x => selectedCard.AppFusionConditionOf()!.linkedCondition(selectedPermanent, x)).First();

                            PlayCardClass playCardClass = new PlayCardClass(new List<CardSource> { selectedCard }, hashtable, true, selectedPermanent, false, SelectCardEffect.Root.Hand, true);
                            playCardClass.SetAppFusion(new int[] { selectedPermanent.PermanentFrame!.FrameID, selectedPermanent.LinkedCards.IndexOf(linkCard) });

                            await playCardClass.PlayCard();
                        }
                    }
                }
            }
        }

        #endregion

        #region Security

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        #endregion

        return cardEffects;
    }
}
