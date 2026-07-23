// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — EX8_068 (Option / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX8/Blue/EX8_068.cs (161 lines, 4 regions)
//    * Ignore Color Requirement :16-33 (timing == None — IgnoreColorConditionClass, 시큐리티 전부 비공개일 때만)
//    * All Turns - Security     :38-76 (timing == None — CanNotBeDestroyedByBattleStaticEffect, 시큐리티에
//      존재+메모리≥1인 동안 자신의 [DS] 디지몬 보호)
//    * Main Effect               :81-84 (timing == OptionSkill — ReplaceBottomSecurityWithFaceUpOptionMainEffect)
//    * Security Effect           :89-155 (timing == SecuritySkill — [DS] 디지몬 무료 플레이)
//
// ② 프리미티브 매핑:
//    * P:IgnoreColorConditionClass — 시큐리티 전부 비공개일 때 색 요구 무시(자기 한정) (AS-IS :18-31)
//    * P:CanNotBeDestroyedByBattleStaticEffect — [All Turns] 자기가 시큐리티에 존재+메모리≥1 동안 자신의 [DS]
//      전투 파괴 면역 (AS-IS :67-74)
//    * P:ReplaceBottomSecurityWithFaceUpOptionMainEffect — [Main] (AS-IS :83)
//    * E:SelectHandEffect Mode.Custom + P:PlayPermanentCards(payCost:false) — [Security] [DS] Lv.5 이하 무료
//      플레이 (AS-IS :116-153)
//
// ③ 배선 관례 근거:
//    * [Main] → OptionSkill(팩토리 자체 배선; 인자로 card만 받아 자체 CanUse/Activate 완비).
//    * [Security] → SecuritySkill + CanTriggerSecurityEffect(hashtable, card) 게이트(AS-IS :106 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`→`await X` (BT17_026 idiom).
//    * `card.Owner.SecurityCards` → `new Player(card.Context, card.Owner).SecurityCards` (미러 Player 접근 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Blue;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_068 : CEntity_Effect
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
                // (SEC-FaceUpSecuritySource) AS-IS `!cardSource.IsFlipped` face gate — the mirror stamps security
                // face state via SecurityFaceState (never the raw field-ACE flag; Permanent.cs FoldLinkedMax
                // precedent, commit 40d1eaee). Gate TRUE when zero face-UP security cards.
                return new Player(card.Context, card.Owner).SecurityCards.Count(cardSource => Headless.Runtime.SecurityFaceState.IsFaceUpInSecurity(card.Context, cardSource.InstanceId)) == 0;
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
                return CardEffectCommons.IsExistInSecurity(card, false) &&
                       new Player(card.Context, card.Owner).MemoryForPlayer >= 1;
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                       permanent.TopCard.EqualsTraits("DS");
            }

            bool CanNotBeDestroyedByBattleCondition(Permanent permanent, Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard)
            {
                if (permanent == AttackingPermanent)
                {
                    return true;
                }

                if (permanent == DefendingPermanent)
                {
                    return true;
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
                canNotBeDestroyedByBattleCondition: CanNotBeDestroyedByBattleCondition,
                permanentCondition: PermanentCondition,
                isInheritedEffect: false,
                card: card,
                condition: CanUseCondition,
                effectName: "Can not be deleted by Battle")
             );
        }

        #endregion

        #region Main Effect

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.ReplaceBottomSecurityWithFaceUpOptionMainEffect(card));
        }

        #endregion

        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Play 1 [DS] Digimon card from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[Security] You may play 1 level 5 or lower [DS] trait Digimon card from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.HasLevel && cardSource.Level <= 5 &&
                       cardSource.EqualsTraits("DS") &&
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

                    selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
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
