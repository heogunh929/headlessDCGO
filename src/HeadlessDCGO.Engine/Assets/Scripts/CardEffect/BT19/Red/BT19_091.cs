// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T1 정본 카드 — Trinity Burst! (BT19_091, Option / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT19/Red/BT19_091.cs (233 lines, 3 regions)
//    * Color Requirements :15-45  (timing == None — 조건부 IgnoreColorConditionClass: Lv.5 디지몬 +
//                                  WarGrowlmon/Taomon/Rapidmon 보유 시 색 요구 무시)
//    * [Main]             :49-147 (OptionSkill — 토큰 3종 플레이 → Lv.5 1장에 <Alliance> 2회 부여 → 공격)
//    * [Security]         :151-228(SecuritySkill — 손패의 Lv.5 3종 중 1장 무료 플레이)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #4, 5축):
//    * K:Alliance            — [Main] 몸통 CardEffectCommons.GainAlliance ×2 (AS-IS :124-125)
//    * P:PlayRapidmonToken   — [Main] 몸통 (AS-IS :94; TokenSpecs["Rapidmon"] 경유)
//    * P:PlayTaomonToken     — [Main] 몸통 (AS-IS :91)
//    * P:PlayWarGrowlmonToken— [Main] 몸통 (AS-IS :88)
//    * S:SelectAttackEffect  — [Main] 몸통 강제 공격(SetCanNotSelectNotAttack) (AS-IS :132-142)
//    * (+P:IgnoreColorConditionClass 조건부, X:IgnoreRequirement, E:SelectPermanentEffect/SelectHandEffect,
//       P:PlayPermanentCards, T:OptionSkill/SecuritySkill)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [Main] 옵션 주효과 → OptionSkill + CanTriggerOptionMainEffect 게이트(AS-IS :61-65 그대로).
//    * [Security] → SecuritySkill + CanTriggerSecurityEffect 게이트 + SetIsSecurityEffect(true)
//      (AS-IS :151-166 그대로; SecurityResolver가 해소).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame() && frame.IsBattleAreaFrame()) >= 1`
//      (AS-IS :64) — 미러는 frame/slot 모델 없음: 빈-프레임 용량 절반은 확립 어댑테이션으로 소거
//      (RD-P6C1-2; Player.CanMove/CanPlayFromHandDuringMainPhase 헤더와 동일 근거). CanTriggerOptionMainEffect
//      절반은 1:1 유지.
//    * 토큰 커먼즈는 AS-IS-시그니처 브릿지(PlayCardsBridge.cs:342/351/360, ICardEffect-형) 그대로 호출.
//    * `selectedPermanent.CanAttack(activateClass)` — 미러 Permanent.CanAttack(ICardEffect, ...) 1:1(:3043).
//    * `card.Owner.GetBattleAreaPermanents()` → `new Player(card.Context, card.Owner).*`;
//      CardColor enum→string idiom; Level == 5 등 술어 원문 유지.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT19_091 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Color Requirements
        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);

            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, (permanent) => permanent.IsDigimon && permanent.TopCard.HasLevel && permanent.Level == 5))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, (permanent) => permanent.TopCard.EqualsCardName("WarGrowlmon") || permanent.TopCard.EqualsCardName("Taomon") || permanent.TopCard.EqualsCardName("Rapidmon")))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CardCondition(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    return true;
                }

                return false;
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play tokens, give alliance and attack", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Play 1 [WarGrowlmon] Token (Digimon/Red/6000 DP), [Taomon] Token (Digimon/Yellow/6000 DP), and 1 [Rapidmon] Token (Digimon/Green/6000 DP). This effect can't play tokens with the same names as your Digimon. Then, 1 of your level 5 Digimon gains <Alliance> twice for the turn and attacks.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                // AS-IS :63-64 `&& card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame() &&
                // frame.IsBattleAreaFrame()) >= 1` — 빈-프레임 용량 절반은 미러 frame-모델 부재로 소거
                // (확립 어댑테이션 RD-P6C1-2, Player.CanMove 헤더 동일 근거).
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if(CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if(permanent.IsDigimon && permanent.TopCard.HasLevel && permanent.Level == 5)
                    {
                        return true;
                    }
                }

                return false;
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanPlayTokens(string cardName)
            {
                return !new Player(card.Context, card.Owner).GetBattleAreaPermanents().Some(permanent => permanent.TopCard.EqualsCardName(cardName));
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CanPlayTokens("WarGrowlmon"))
                    await CardEffectCommons.PlayWarGrowlmonToken(activateClass);

                if (CanPlayTokens("Taomon"))
                    await CardEffectCommons.PlayTaomonToken(activateClass);

                if (CanPlayTokens("Rapidmon"))
                    await CardEffectCommons.PlayRapidmonToken(activateClass);

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));
                    Permanent? selectedPermanent = null;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get Alliance twice.", "The opponent is selecting 1 Digimon.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        await CardEffectCommons.GainAlliance(targetPermanent: permanent, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                        await CardEffectCommons.GainAlliance(targetPermanent: permanent, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                    }

                    if (selectedPermanent != null)
                    {
                        if (selectedPermanent.CanAttack(activateClass))
                        {
                            SelectAttackEffect selectAttackEffect = GManager.instance.GetComponent<SelectAttackEffect>();

                            selectAttackEffect.SetUp(
                                attacker: selectedPermanent,
                                canAttackPlayerCondition: () => true,
                                defenderCondition: (permanent) => true,
                                cardEffect: activateClass);

                            selectAttackEffect.SetCanNotSelectNotAttack();

                            await selectAttackEffect.Activate();
                        }
                    }
                }
            }
        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Play 1 [WarGrowlmon]/[Taomon]/[Rapidmon] Digimon card from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Security] You may play 1 level 5 [WarGrowlmon]/[Taomon]/[Rapidmon] from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if(cardSource.IsDigimon && cardSource.HasLevel && cardSource.Level == 5 && cardSource.EqualsCardName("WarGrowlmon") && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    return true;
                }

                if (cardSource.IsDigimon && cardSource.HasLevel && cardSource.Level == 5 && cardSource.EqualsCardName("Taomon") && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    return true;
                }

                if(cardSource.IsDigimon && cardSource.HasLevel && cardSource.Level == 5 && cardSource.EqualsCardName("Rapidmon") && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardCondition))
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 level 5 [WarGrowlmon]/[Taomon]/[Rapidmon card to play.", "The opponent is selecting 1 card to play.");
                    selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                    // AS-IS :214 `yield return StartCoroutine(selectHandEffect.Activate())` — 동일 await.
                    await selectHandEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false,
                        root: SelectCardEffect.Root.Hand, activateETB: true);
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
