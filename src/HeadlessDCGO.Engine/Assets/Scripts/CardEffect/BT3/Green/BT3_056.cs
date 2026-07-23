// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3B 정본 카드 — Ceresmon (BT3_056, Digimon / Green) — 수확 STOP 상환(RD-EXT3-03)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT3/Green/BT3_056.cs (486 lines, 3 regions)
//    * <Digisorption -3>          :16-277 (BeforePayCost — 진화-흡수 시 아군 1체 서스펜드→진화 코스트 -3)
//    * [Your Turn][OPT] 상대 대체  :279-407(WhenDigisorption — Digisorption 서스펜드 대상을 상대로 대체)
//    * ESS 상대 대체(상시)         :409-483(None — CanSuspendByDigisorptionClass 등록)
//
// ② 상환 근거 (RD-EXT3-03 — 진입 판정 Player.CanTapWhenAbsorbEvolution(_CheckAvailability) 2종을 미러 Player에
//    1:1 포팅함으로써 STOP 좌석 해소): <Digisorption -N> BeforePayCost 팔의 후보/가용성 술어가 이 두 Player
//    메서드를 부른다(AS-IS :31/:47). 미러 Player.cs에 두 메서드가 착지한 뒤 세 팔을 1:1 포팅 — 옵션 서스펜드
//    →조건부 ChangeCost(-3) 파이프는 기존 SelectPermanentEffect(Mode.Tap)+ChangeCostClass 확립 표면 재사용
//    (BT21_030 판례; 신규 choice 타입 발명 없음).
//
// ③ 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`;
//      lone `yield return null`→제거.
//    * `card.Owner`(AS-IS Player) → HeadlessPlayerId; Player 조작(CanTapWhenAbsorbEvolution(_CheckAvailability)/
//      CanReduceCost/GetBattleAreaPermanents/UntilCalculateFixedCostEffect)은 `new Player(card.Context, card.Owner).*`.
//    * `GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer` → `new GameContext(card.Context).
//      Players_ForTurnPlayer`(미러 확립 idiom; List<Player>).
//    * `SelectPermanentEffect` canTargetCondition는 정본 Permanent-형(Func<Permanent,bool>) — AS-IS Permanent-술어를
//      그대로 전달(id 어댑터 없음). Has/Count 스캔도 동일 Permanent-술어 직접 사용.
//    * `card.PermanentOfThisCard()`(PermanentView) → `ICardEffect.ResolvePermanentOfThisCard(card)`(Permanent;
//      ST1_09/BT2_081 판례).
//    * `card.Owner.Enemy`(AS-IS live Player.Enemy) → `new Player(card.Context, card.Owner).Enemy?.PlayerId`
//      (BT8_057/BT5_086 판례; Owner=HeadlessPlayerId 비교).
//    * `ContinuousController.instance.PlaySE(...BuffSE)`(AS-IS :191/:372) = UI/SE 연출 — 스트립(ST17_13 판례).
//    * `card.Owner.CanReduceCost(new List<Permanent>() { new Permanent(new List<CardSource>()) }, card)`(AS-IS :189)
//      — AS-IS는 SE 게이트로만 쓰는 부수효과 없는 판정. 미러 Permanent는 CardSource-리스트 ctor가 없어 실인수를
//      만들 수 없으므로(부수효과 없음) 이 SE-게이트 판정은 스트립(연출; ST17_13 판례) — 코스트 -3 인과에는 무영향
//      (실제 감액 게이트는 ChangeCostClass.GetCost 내부의 CanReduceCost가 담당, ChangeCostClass.cs:45).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_056 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Digisorption -3 (BeforePayCost)

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Digisorption -3", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetHashString("Digisorption-3_BT2_047");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "<Digisorption -3> (When one of your Digimon digivolves into this card from your hand, you may suspend 1 of your Digimon to reduce the memory cost of the digivolution by 3.)";
            }

            bool CanSelectCondition_CheckAvailability(Permanent permanent)
            {
                if (new Player(card.Context, card.Owner).CanTapWhenAbsorbEvolution_CheckAvailability(permanent, activateClass))
                {
                    if (permanent.CanSelectBySkill(activateClass))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (new Player(card.Context, card.Owner).CanTapWhenAbsorbEvolution(permanent, activateClass))
                {
                    if (permanent.CanSelectBySkill(activateClass))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (hashtable != null)
                {
                    if (hashtable.ContainsKey("Card"))
                    {
                        if (hashtable["Card"] is CardSource)
                        {
                            CardSource Card = (CardSource)hashtable["Card"];

                            if (Card == card)
                            {
                                if (hashtable.ContainsKey("isEvolution"))
                                {
                                    if (hashtable["isEvolution"] is bool)
                                    {
                                        bool isEvolution = (bool)hashtable["isEvolution"];

                                        if (isEvolution)
                                        {
                                            if (hashtable.ContainsKey("Permanents"))
                                            {
                                                if (hashtable["Permanents"] is List<Permanent>)
                                                {
                                                    List<Permanent> Permanents = (List<Permanent>)hashtable["Permanents"];

                                                    if (Permanents != null)
                                                    {
                                                        if (Permanents.Count((permanent) => permanent.TopCard.Owner == card.Owner && new Player(card.Context, permanent.TopCard.Owner).GetBattleAreaPermanents().Contains(permanent)) >= 1)
                                                        {
                                                            if (new GameContext(card.Context).Players_ForTurnPlayer.Count((player) => player.GetBattleAreaDigimons().Count(CanSelectCondition_CheckAvailability) >= 1) >= 1)
                                                            {
                                                                return true;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (new GameContext(card.Context).Players_ForTurnPlayer.Count((player) => player.GetBattleAreaDigimons().Count(CanSelectCondition_CheckAvailability) >= 1) >= 1)
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                #region 진화-흡수 컷인(WhenDigisorption) 개방
                Hashtable hashtable = new Hashtable();
                hashtable.Add("CardEffect", activateClass);

                foreach (Player player in new GameContext(card.Context).Players_ForTurnPlayer)
                {
                    #region 장 파마넌트의 WhenDigisorption
                    foreach (Permanent permanent1 in player.GetFieldPermanents())
                    {
                        foreach (ICardEffect cardEffect in permanent1.EffectList(EffectTiming.WhenDigisorption))
                        {
                            if (cardEffect is ActivateICardEffect)
                            {
                                if (cardEffect.CanTrigger(hashtable))
                                {
                                    GManager.instance.autoProcessing_CutIn.PutStackedSkill(new SkillInfo(cardEffect, hashtable, EffectTiming.WhenDigisorption));
                                }
                            }
                        }
                    }
                    #endregion

                    #region 플레이어의 WhenDigisorption
                    foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.WhenDigisorption))
                    {
                        if (cardEffect is ActivateICardEffect)
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                GManager.instance.autoProcessing_CutIn.PutStackedSkill(new SkillInfo(cardEffect, hashtable, EffectTiming.WhenDigisorption));
                            }
                        }
                    }
                    #endregion
                }

                await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(false, AutoProcessing.HasExecutedSameEffect);
                #endregion

                if (new GameContext(card.Context).Players_ForTurnPlayer.Count((player) => player.GetBattleAreaDigimons().Count(CanSelectPermanentCondition) >= 1) >= 1)
                {
                    int maxCount = 1;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();

                    async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                    {
                        if (permanents.Count >= 1)
                        {
                            // AS-IS :189-192 CanReduceCost 판정 + PlaySE(BuffSE) = SE 연출 게이트 — 스트립(헤더 ③).

                            ChangeCostClass changeCostClass = new ChangeCostClass();
                            changeCostClass.SetUpICardEffect("Digivolution Cost -3", CanUseCondition1, card);
                            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                            new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

                            await CardEffectCommons.ShowReducedCost(_hashtable);

                            bool CanUseCondition1(Hashtable hashtable)
                            {
                                return true;
                            }

                            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
                            {
                                if (CardSourceCondition(cardSource))
                                {
                                    if (RootCondition(root))
                                    {
                                        if (PermanentsCondition(targetPermanents))
                                        {
                                            Cost -= 3;
                                        }
                                    }
                                }

                                return Cost;
                            }

                            bool PermanentsCondition(List<Permanent> targetPermanents)
                            {
                                if (targetPermanents != null)
                                {
                                    if (targetPermanents.Count(PermanentCondition) >= 1)
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            bool PermanentCondition(Permanent targetPermanent)
                            {
                                if (targetPermanent.TopCard != null)
                                {
                                    if (targetPermanent.TopCard.Owner == card.Owner)
                                    {
                                        if (new Player(card.Context, targetPermanent.TopCard.Owner).GetBattleAreaPermanents().Contains(targetPermanent))
                                        {
                                            return true;
                                        }
                                    }
                                }

                                return false;
                            }

                            bool CardSourceCondition(CardSource cardSource)
                            {
                                if (cardSource != null)
                                {
                                    if (cardSource == card)
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            bool RootCondition(SelectCardEffect.Root root)
                            {
                                return true;
                            }

                            bool isUpDown()
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region [Your Turn][Once Per Turn] 상대 디지몬 대체 서스펜드 (WhenDigisorption)

        if (timing == EffectTiming.WhenDigisorption)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend opponent's Digimon for Digisorption instead", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("SuspendEnemyDigimonWhenDigisorption_BT3_056");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When suspending Digimon for a <Digisorption> skill, you may suspend your opponent's Digimon instead.";
            }

            bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent != null)
                {
                    if (targetPermanent.TopCard != null)
                    {
                        if (targetPermanent.TopCard.Owner == new Player(card.Context, card.Owner).Enemy?.PlayerId)
                        {
                            if (!targetPermanent.IsSuspended)
                            {
                                if (new Player(card.Context, targetPermanent.TopCard.Owner).GetBattleAreaDigimons().Contains(targetPermanent))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (isExistOnField(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        if (CardEffectCommons.IsOwnerTurn(card))
                        {
                            if (hashtable != null)
                            {
                                if (hashtable.ContainsKey("CardEffect"))
                                {
                                    if (hashtable["CardEffect"] is ICardEffect)
                                    {
                                        ICardEffect CardEffect = (ICardEffect)hashtable["CardEffect"];

                                        if (CardEffect != null)
                                        {
                                            if (CardEffect.EffectName.Contains("Digisorption"))
                                            {
                                                if (CardEffect.EffectSourceCard != null)
                                                {
                                                    if (CardEffect.EffectSourceCard.Owner == card.Owner)
                                                    {
                                                        return true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).Enemy?.GetBattleAreaDigimons().Count(PermanentCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        // AS-IS :372 PlaySE(BuffSE) = SE 연출 — 스트립(ST17_13 판례).

                        CanSuspendByDigisorptionClass canTapDigisorptionClass = new CanSuspendByDigisorptionClass();
                        canTapDigisorptionClass.SetUpICardEffect("Suspend your opponent's Digimon instead", CanUseCondition1, card);
                        canTapDigisorptionClass.SetUpCanSuspendByDigisorptionClass(PermanentCondition: PermanentCondition, CardEffectCondition: CardEffectCondition, () => false);
                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => canTapDigisorptionClass);

                        bool CanUseCondition1(Hashtable hashtable)
                        {
                            return true;
                        }

                        bool CardEffectCondition(ICardEffect cardEffect)
                        {
                            if (cardEffect != null)
                            {
                                if (cardEffect.EffectName.Contains("Digisorption"))
                                {
                                    if (cardEffect.EffectSourceCard != null)
                                    {
                                        if (cardEffect.EffectSourceCard.Owner == card.Owner)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }

                            return false;
                        }

                        await Task.CompletedTask;
                    }
                }
            }
        }

        #endregion

        #region ESS 상대 대체(상시) (None)

        if (timing == EffectTiming.None)
        {
            CanSuspendByDigisorptionClass canTapDigisorptionClass = new CanSuspendByDigisorptionClass();
            canTapDigisorptionClass.SetUpICardEffect("Suspend your opponent's Digimon instead", CanUseCondition, card);
            canTapDigisorptionClass.SetUpCanSuspendByDigisorptionClass(PermanentCondition: PermanentCondition, CardEffectCondition: CardEffectCondition, () => true);
            canTapDigisorptionClass.SetNotShowUI(true);
            cardEffects.Add(canTapDigisorptionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (isExistOnField(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        if (CardEffectCommons.IsOwnerTurn(card))
                        {
                            if (card.EffectList(EffectTiming.WhenDigisorption).Count >= 1)
                            {
                                ICardEffect cardEffect = card.EffectList(EffectTiming.WhenDigisorption)[0];

                                if (!card.cEntity_EffectController.isOverMaxCountPerTurn(cardEffect, cardEffect.MaxCountPerTurn))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool PermanentCondition(Permanent targetPermanent)
            {
                if (targetPermanent != null)
                {
                    if (targetPermanent.TopCard != null)
                    {
                        if (targetPermanent.TopCard.Owner == new Player(card.Context, card.Owner).Enemy?.PlayerId)
                        {
                            if (!targetPermanent.IsSuspended)
                            {
                                if (new Player(card.Context, targetPermanent.TopCard.Owner).GetBattleAreaDigimons().Contains(targetPermanent))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CardEffectCondition(ICardEffect cardEffect)
            {
                if (cardEffect != null)
                {
                    if (cardEffect.EffectName.Contains("Digisorption"))
                    {
                        if (cardEffect.EffectSourceCard != null)
                        {
                            if (cardEffect.EffectSourceCard.Owner == card.Owner)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
        }

        #endregion

        return cardEffects;
    }
}
