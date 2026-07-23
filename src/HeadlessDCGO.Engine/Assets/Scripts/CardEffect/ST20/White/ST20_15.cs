// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — ST20_15 (Option / White, "ST21 Gennai's House")
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/ST20/White/ST20_15.cs (145 lines, 4 regions)
//    * Ignore Color Requirement :16-32 (timing == None — IgnoreColorConditionClass, [Island of Adventure]
//      가드 — 앞면이 아닐 때만 발동)
//    * All Turns - Security     :37-62 (timing == None — ChangeDPStaticEffect, 시큐리티 존재 조건부)
//    * Main Effect               :66-69 (timing == OptionSkill — ReplaceTopSecurityWithFaceUpOptionMainEffect)
//    * Security Effect          :75-138 (timing == SecuritySkill — 손패 테이머 1장 무상 플레이)
//
// ② 프리미티브 매핑:
//    * P:IgnoreColorConditionClass — 자기 자신 색상요건 무시 (AS-IS :18-31; symbol_map row 49 OK).
//    * P:ChangeDPStaticEffect      — [Security][All Turns] 자기 레벨3+ 디지몬 DP+2000 (AS-IS :55-61;
//      symbol_map row 108 OK, effectName은 Func<string>).
//    * P:ReplaceTopSecurityWithFaceUpOptionMainEffect — [Main] (AS-IS :68; symbol_map row 418 OK, card 단일 인자).
//    * E:SelectHandEffect Mode.Custom + P:PlayPermanentCards — [Security] 손패 테이머 무상 플레이
//      (AS-IS :99-137; BT19_091 established idiom).
//
// ③ 배선 관례 근거: [Security]→SecuritySkill+CanTriggerSecurityEffect(hashtable, card) 그대로(AS-IS :90).
//    [Main]은 AS-IS 자체가 OptionSkill.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`/`yield return
//      ContinuousController.instance.StartCoroutine(X)`→`await X` (BT8_092/BT19_091 idiom).
//    * `card.Owner.SecurityCards` → `new Player(card.Context, card.Owner).SecurityCards`
//      (symbol_map_guide §2.2; BT1_006 idiom).
//    * `CardEffectCommons.CanPlayAsNewPermanent(cardSource:, payCost:, cardEffect:)` — AS-IS 그대로(named args
//      동일 시그니처, symbol_map row 18 OK).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST20.White;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST20_15 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Ignore Color Requirement
        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);
            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                // (SEC-FaceUpSecuritySource) AS-IS `!securityCard.IsFlipped` face gate — the mirror stamps security
                // face state via SecurityFaceState (never the raw field-ACE flag; Permanent.cs FoldLinkedMax
                // precedent, commit 40d1eaee). Gate TRUE when no face-UP [Island of Adventure] security card.
                return !new Player(card.Context, card.Owner).SecurityCards.Any(securityCard => securityCard.EqualsCardName("Island of Adventure") && Headless.Runtime.SecurityFaceState.IsFaceUpInSecurity(card.Context, securityCard.InstanceId));
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }
        #endregion

        #region All Turns - Security

        if (timing == EffectTiming.None)
        {
            bool CanUseCondition()
            {
                return CardEffectCommons.IsExistInSecurity(card, false);
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                       permanent.TopCard.HasLevel && permanent.Level >= 3;
            }

            string EffectDescription()
            {
                return "[Security][All Turns] All of your level 3 or higher Digimon get +2000 DP.";
            }

            cardEffects.Add(CardEffectFactory.ChangeDPStaticEffect(
            permanentCondition: PermanentCondition,
            changeValue: 2000,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: EffectDescription));
        }
        #endregion

        #region Main Effect
        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.ReplaceTopSecurityWithFaceUpOptionMainEffect(card));
        }
        #endregion


        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 tamer from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, true, EffectDescription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[Security] You may play 1 Tamer card from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsTamer &&
                       CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
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

                    selectHandEffect.SetUpCustomMessage("Select 1 tamer to play.", "The opponent is selecting 1 tamer to play.");
                    selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

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
