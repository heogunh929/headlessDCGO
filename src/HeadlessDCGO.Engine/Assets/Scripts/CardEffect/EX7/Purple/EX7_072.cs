// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3A 정본 카드 (수확 트랜치) — Seventh Fascination (EX7_072, Option / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX7/Purple/EX7_072.cs (296 lines, 3 regions)
//    * [Trash][Your Turn] OnEnterFieldAnyone :14-85 (Lilithmon(X Antibody) 진화 시 이 카드 덱밑 반환→자기 [Main] 발화)
//    * [Main] OptionSkill              :87-233 (상대 전 디지몬에 "[End of Your Turn] 자기 디지몬 1체 삭제" 부여)
//    * [Security] SecuritySkill         :235-292 (상대 미서스펜드 디지몬 1체 삭제)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #15, 3축):
//    * P:AddSkillClass    — [Main] 몸통 nested-grant (AS-IS :128; 상대 디지몬에 스킬 부여)
//    * P:AddDetailClass   — [Main] 몸통 detail 부여 (AS-IS :157; CardEffectFactory.AddDetailClass)
//    * P:OptionMainEffect — [Trash] 몸통 자기 [Main] 재발화 (AS-IS :76; CardEffectCommons.OptionMainEffect)
//    * (+CanTriggerWhenPermanentDigivolving, SelectPermanentEffect Destroy)
//
// ③ 배선 관례 근거: [Trash]의 permanent-digivolve 워쳐 → EffectTiming.WhenDigivolving 전용 키(미러 방언;
//    AS-IS는 OnEnterFieldAnyone에 CanTriggerWhenPermanentDigivolving 게이트를 매달았으며 게이트는 그대로 유지 —
//    BT17_026 [When Digivolving] 관례와 동형). [Main]→OptionSkill; [Security]→SecuritySkill+SetIsSecurityEffect.
//
// 수확(예측 BUSTED — coverage_exemplar_audit §6 "AddSkillClass(중첩 부여) → nested-grant STOP"):
//    AddSkillClass는 완전 미러(내부 STOP 없음)이며 IAddSkillEffect는 **플레이어-레벨로 LIVE 스캔**
//    (CEntity_EffectController.cs:156-167; CardSource.EffectList→GetCardEffects→ActivatedEffectResolver가
//    OnEndTurn에 호출). Player.EffectList가 UntilOpponentTurnEndEffects를 집계(Player.cs:386). 작동 선례=BT1_104.
//    → 부여된 "[End of Your Turn] Delete 1 of your Digimon"는 발화. AddSkillClass STOP 예측 BUSTED — 3팔 전부 포팅.
//    (좁은 substrate 치환: [Trash]의 `CardObjectController.AddLibraryBottomCards`는 명명 헬퍼 미이관 →
//     IZoneMover.MoveToDeckBottomAsync 1:1 대체.)
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask.
//    * `card.Owner.Enemy`(AS-IS Player) → `new Player(card.Context, card.Owner).Enemy` (BT2_023 idiom).
//    * `cardSource.PermanentOfThisCard()`(PermanentView) → `ICardEffect.ResolvePermanentOfThisCard(cardSource)`
//      (PermanentView→Permanent 브릿지; BT19_091 idiom).
//    * SelectPermanentEffect canTargetCondition = 정본 Func<Permanent,bool> → Permanent 술어 직결(id 어댑터 없음).
//    * `CreateDebuffEffect`/`ShowCardEffect2` = UI 연출 — 스트립.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX7.Purple;

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

public sealed class EX7_072 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Trash Your Turn
        // AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenPermanentDigivolving → 미러 EffectTiming.WhenDigivolving.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return this to bottom of deck, Activate Main", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Trash] [Your Turn] When your Digimon digivolves into [Lilithmon (X Antibody)], by returning this card to the bottom of the deck, activate this card's [Main] effect.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Lilithmon (X Antibody)"))
                    {
                        return true;
                    }

                    if (permanent.TopCard.CardNames.Contains("Lilithmon(XAntibody)"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerWhenPermanentDigivolving(hashtable, PermanentCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnTrash(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    // AS-IS :70-72 `List<CardSource>{card}` → `CardObjectController.AddLibraryBottomCards(cardSources)`
                    // — the named CardObjectController deck-move helper is unported; the 1:1 substrate is
                    // IZoneMover.MoveToDeckBottomAsync per card (BT2_044 DeckBottom idiom).
                    await card.Context.ZoneMover.MoveToDeckBottomAsync(card.Owner, card.InstanceId);

                    // AS-IS :74 `ShowCardEffect2(...)` — UI 연출, 스트립.

                    ActivateClass? mainActivateClass = CardEffectCommons.OptionMainEffect(card);

                    if (mainActivateClass != null)
                    {
                        await mainActivateClass.Activate(CardEffectCommons.OptionMainCheckHashtable(card));
                    }
                }
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("All Opponents Digimon gain \"Delete 1 of your Digimon\"", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] All your opponent's Digimon gain \" [End of Your Turn] Delete 1 of your Digimon.\" until the end of their turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool PermanentCondition(Permanent permanent)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                foreach (Permanent permanent in new Player(card.Context, card.Owner).Enemy!.GetBattleAreaPermanents())
                {
                    if (PermanentCondition(permanent))
                    {
                        // AS-IS :124 `CreateDebuffEffect(permanent)` — UI 연출, 스트립.
                    }
                }

                AddSkillClass addSkillClass = new AddSkillClass();
                addSkillClass.SetUpICardEffect("Delete 1 of your Digimon", CanUseCondition1, card);
                addSkillClass.SetUpAddSkillClass(cardSourceCondition: CardSourceCondition, getEffects: GetEffects);

                new Player(card.Context, card.Owner).UntilOpponentTurnEndEffects.Add((_timing) => addSkillClass);
                new Player(card.Context, card.Owner).UntilOpponentTurnEndEffects.Add(GetDetailEffect);

                bool CanUseCondition1(Hashtable hashtable)
                {
                    return true;
                }

                bool CardSourceCondition(CardSource cardSource)
                {
                    Permanent? permanentOfThisCard = ICardEffect.ResolvePermanentOfThisCard(cardSource);
                    if (PermanentCondition(permanentOfThisCard!))
                    {
                        if (permanentOfThisCard is not null && cardSource == permanentOfThisCard.TopCard)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                ICardEffect? GetDetailEffect(EffectTiming timing)
                {
                    if (timing == EffectTiming.None)
                    {
                        return CardEffectFactory.AddDetailClass(CanUseCondition1, PermanentCondition, "[End of your turn] Delete 1 of your Digimon", true, card);
                    }
                    return null;
                }

                List<ICardEffect> GetEffects(CardSource cardSource, List<ICardEffect> cardEffects, EffectTiming _timing)
                {
                    if (_timing == EffectTiming.OnEndTurn)
                    {
                        ActivateClass activateClass1 = new ActivateClass();
                        activateClass1.SetUpICardEffect("Delete 1 of your Digimon", CanUseCondition2, cardSource);
                        activateClass1.SetUpActivateClass(CanActivateCondition2, ActivateCoroutine1, -1, false, EffectDiscription1());
                        activateClass1.SetEffectSourceCard(cardSource);
                        cardEffects.Add(activateClass1);

                        Permanent? sourcePermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
                        if (sourcePermanent != null && CardEffectCommons.IsExistOnBattleArea(cardSource))
                        {
                            activateClass1.SetEffectSourcePermanent(sourcePermanent);
                        }

                        string EffectDiscription1()
                        {
                            return "[End of Your Turn] Delete 1 of your Digimon.";
                        }

                        bool CanSelectPermanentCondition(Permanent permanent)
                        {
                            return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, cardSource);
                        }

                        bool CanUseCondition2(Hashtable hashtable)
                        {
                            if (CardEffectCommons.IsOwnerTurn(cardSource))
                            {
                                if (CardEffectCommons.IsExistOnBattleAreaDigimon(cardSource))
                                    return CardSourceCondition(cardSource);
                            }

                            return false;
                        }

                        bool CanActivateCondition2(Hashtable hashtable)
                        {
                            return CardEffectCommons.IsExistOnBattleAreaDigimon(cardSource);
                        }

                        async Task ActivateCoroutine1(Hashtable _hashtable)
                        {
                            if (CardEffectCommons.HasMatchConditionPermanent(cardSource, CanSelectPermanentCondition))
                            {
                                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(cardSource, CanSelectPermanentCondition));

                                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                                selectPermanentEffect.SetUp(
                                    selectPlayer: cardSource.Owner,
                                    canTargetCondition: CanSelectPermanentCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    maxCount: maxCount,
                                    canNoSelect: false,
                                    canEndNotMax: false,
                                    selectPermanentCoroutine: null,
                                    afterSelectPermanentCoroutine: null,
                                    mode: SelectPermanentEffect.Mode.Destroy,
                                    cardEffect: activateClass);

                                await selectPermanentEffect.Activate();
                            }
                        }
                    }

                    return cardEffects;
                }
            }
        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Opponents unsuspended Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Delete 1 of your opponent's unsuspended Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (!permanent.IsSuspended)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
