// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 1:1 포팅 카드 — EX8_072 (Option / Purple)  [Barbamon (X Antibody) 연계 옵션]
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX8/Purple/EX8_072.cs (184 lines, 3 regions)
//    * Trash Your Turn :12-85  (OnEnterFieldAnyone + CanTriggerWhenPermanentDigivolving — 자기 디지몬이
//      [Barbamon (X Antibody)]로 진화 시, 이 카드를 덱밑 반환→자기 [Main] 재발화)
//    * Main             :87-169 (OptionSkill — 상대 손패 5장↑이면 1장 트래시; 상대 Lv≤(7-상대손패/3) 디지몬 1체 삭제)
//    * Security Effect  :171-179(SecuritySkill — AddActivateMainOptionSecurityEffect)
//
// ② 배선 관례 근거 (trigger-wiring-porting-rules, EX7_072 sibling exemplar):
//    * AS-IS `timing == OnEnterFieldAnyone` + `CanTriggerWhenPermanentDigivolving` 게이트 →
//      미러 전용 키 EffectTiming.WhenDigivolving(EX7_072 :59-60 관례와 동형; 게이트 자체는 그대로 유지).
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect(hashtable, card). [Security] → SecuritySkill +
//      AddActivateMainOptionSecurityEffect(SecurityResolver가 해소).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)`/`yield return StartCoroutine(X)`
//      →`await X`; 말미 lone `yield return null` 소거.
//    * AS-IS :71-72 `CardObjectController.AddLibraryBottomCards(cardSources)` — 명명 헬퍼 미이관 →
//      `card.Context.ZoneMover.MoveToDeckBottomAsync(card.Owner, card.InstanceId)` 1:1 대체(EX7_072 idiom).
//    * AS-IS :73 `GManager.instance.GetComponent<Effects>().ShowCardEffect2(...)` = UI 연출 — 스트립(EX7_072/EX9_062 관례).
//    * `card.Owner.Enemy.HandCards`(AS-IS Player) → `new Player(card.Context, card.Owner).Enemy!.HandCards`(EX7 idiom).
//    * `card.Owner.Enemy`(SelectHandEffect selectPlayer) → `new Player(card.Context, card.Owner).Enemy!.PlayerId`.
//    * SelectPermanentEffect / HasMatchConditionOpponentsPermanent canTargetCondition = 정본 Func<Permanent,bool>
//      → Permanent 술어 직결(id 어댑터 없음).
//    * OptionMainEffect 은 미러에서 ActivateClass? 반환(OptionMainEffect.cs:15).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Purple;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_072 : CEntity_Effect
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
                return "[Trash] [Your Turn] When your Digimon digivolves into [Barbamon (X Antibody)], by returning this card to the bottom of the deck, activate this card's [Main] effect.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
                {
                    if (permanent.TopCard.CardNames.Contains("Barbamon (X Antibody)"))
                    {
                        return true;
                    }

                    if (permanent.TopCard.CardNames.Contains("Barbamon(XAntibody)"))
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
                    // AS-IS :69-71 `List<CardSource>{card}` → `CardObjectController.AddLibraryBottomCards(cardSources)`
                    // — 명명 헬퍼 미이관; 1:1 substrate 는 per-card IZoneMover.MoveToDeckBottomAsync (EX7_072 idiom).
                    await card.Context.ZoneMover.MoveToDeckBottomAsync(card.Owner, card.InstanceId);

                    // AS-IS :73 `ShowCardEffect2(...)` — UI 연출, 스트립.

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
            activateClass.SetUpICardEffect("Opponent trashes 1 card, then delete 1 Digimon.", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] If your opponent has 5 or more cards in their hand, they trash 1 card in their hand. Then, delete 1 of your opponent's level 7 or lower Digimon. For every 3 cards in your opponent's hand, remove 1 from this effect's level maximum.";
            }

            bool SelectOpponentsDigimon(Permanent permanent)
            {
                var levelMax = 7 - (new Player(card.Context, card.Owner).Enemy!.HandCards.Count / 3);
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasLevel && permanent.Level <= levelMax)
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

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).Enemy!.HandCards.Count >= 5)
                {
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: new Player(card.Context, card.Owner).Enemy!.PlayerId,
                        canTargetCondition: (cardSource) => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Discard,
                        cardEffect: activateClass);

                    await selectHandEffect.Activate();
                }

                if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, SelectOpponentsDigimon))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: SelectOpponentsDigimon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete.");

                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects,
                effectName: "Opponent trashes 1 card, then delete 1 Digimon.");
        }

        #endregion

        return cardEffects;
    }
}
