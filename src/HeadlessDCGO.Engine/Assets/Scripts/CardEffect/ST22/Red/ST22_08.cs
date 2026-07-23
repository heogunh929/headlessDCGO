// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — ST22_08 (Option / Red, "ST22 Offensive Plug-in V")
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/ST22/Red/ST22_08.cs (300 lines, switch-dispatch 5 메서드)
//    * None       :17-20 → IgnoreColorCondition + LinkCondition
//    * OnDeclaration :21-22 → LinkAction (CardEffectFactory.LinkEffect)
//    * OnEndTurn  :24-25 → EndOfTurnLinkedEffect ([End of Your Turn] 링크된 카드가 공격 가능)
//    * SecuritySkill :27-28 → SecurityEffect (상대 최소DP 디지몬 삭제 + 손패로)
//    * OptionSkill :30-31 → MainEffect (링크 + 상대 디지몬 삭제)
//
// ② 프리미티브 매핑 (ST22_08은 EXEMPLAR-GLINK 판례의 첫 소비자 — G-Link 표면 위):
//    * P:IgnoreColorConditionClass — 자기 색상요건 무시, 소유 테이머 존재 시 (AS-IS :39-56; symbol_map row 49 OK).
//    * K:Link — P:AddSelfLinkConditionStaticEffect(LinkCondition) + P:LinkEffect(LinkAction, OnDeclaration) +
//      P:AddLinkCard(MainEffect 몸통) — EX10_029(EXEMPLAR-GLINK) established idiom, RD-P6C2-7 해소된 표면
//      그대로 재사용. STOP 불필요(표면 실존 확인됨).
//    * E:SelectPermanentEffect Mode.Destroy/Custom + P:IsMinDP + P:AddThisCardToHand — [Security] (AS-IS :58-99;
//      symbol_map row 103/53 OK).
//    * P:CardSource.CanLinkToTargetPermanent + P:AddLinkCard — [Main] 링크 파트 (AS-IS :118-156).
//    * E:SelectPermanentEffect Mode.Custom/Destroy — [Main] 삭제 파트 (AS-IS :180-235).
//    * P:SelectAttackEffect — [End of Your Turn] 링크된 카드 공격 (AS-IS :279-296).
//
// ③ 배선 관례 근거: Link 선언은 AS-IS 그대로 OnDeclaration. [End of Your Turn]은 AS-IS 자체가 직접
//    EffectTiming.OnEndTurn(WhenLinked 방언 대상 아님 — SetIsLinkedEffect(true)로 링크-소속만 마킹).
//    [Security]/[Main]은 AS-IS 그대로 SecuritySkill/OptionSkill.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      lone `yield return null`→`Task.CompletedTask` (BT8_092 idiom).
//    * `card.Owner.Enemy` → `CardEffectCommons.OpponentOf(card)`(HeadlessPlayerId 필요한 IsMinDP 호출용) 또는
//      `new Player(card.Context, card.Owner).Enemy!`(Player 인스턴스 필요한 GetBattleAreaDigimons()용)
//      (symbol_map_guide §2.2; EX7_014:32/75 idiom).
//    * `card.Owner.GetBattleAreaDigimons()` → HeadlessPlayerId 확장 그대로 사용.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT17_026/EX10_029 idiom;
//      DP/CanAttack 등 가변 Permanent 필요). AS-IS :283/288의 `...TopCard.PermanentOfThisCard()`(CardSource
//      위 이중 호출)도 동형으로 `ICardEffect.ResolvePermanentOfThisCard(...)`.
//    * `CardEffectCommons.HasMatchConditionPermanent(cond)`/`MatchConditionPermanentCount(cond)`(구식, card
//      없음) → card 파라미터 추가; 둘 다 Permanent-술어 오버로드 존재(id-flip 3b — SelectPermanentEffect.SetUp의
//      canTargetCondition도 동일 Permanent-술어 직접 전달, 이전 id 어댑터 전삭).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST22.Red;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST22_08 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        switch (timing)
        {
            case EffectTiming.None:
                cardEffects.Add(IgnoreColorCondition(card));
                cardEffects.Add(LinkCondition(card));
                break;
            case EffectTiming.OnDeclaration:
                cardEffects.Add(LinkAction(card));
                break;
            case EffectTiming.OnEndTurn:
                cardEffects.Add(EndOfTurnLinkedEffect(card));
                break;
            case EffectTiming.SecuritySkill:
                cardEffects.Add(SecurityEffect(card));
                break;
            case EffectTiming.OptionSkill:
                cardEffects.Add(MainEffect(card));
                break;

        }

        return cardEffects;
    }

    IgnoreColorConditionClass IgnoreColorCondition(CardSource card)
    {
        IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
        ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
        ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.HasMatchConditionOwnersPermanent(card, (permanent) => permanent.IsTamer);
        }

        bool CardCondition(CardSource cardSource)
        {
            return cardSource == card;
        }

        return ignoreColorConditionClass;
    }

    ActivateClass SecurityEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect($"Delete opponent's lowest DP Digimon and add this card to hand", CanUseCondition, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
        activateClass.SetIsSecurityEffect(true);

        string EffectDiscription()
            => "[Security] Delete 1 of your opponent's Digimon with the lowest DP. Then, add this card to the hand.";

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
        }

        bool CanSelectPermanentCondition(Permanent permanent)
            => CardEffectCommons.IsMinDP(permanent, CardEffectCommons.OpponentOf(card));

        async Task ActivateCoroutine(Hashtable _hashtable)
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
            // AS-IS :96 `AddThisCardToHand(card, activateClass)`(ICardEffect) — 미러 시그니처는
            // `(CardSource, CardSource)`(CardEffectCommons.cs:1874) → `(card, card)` (BT1_093/BT1_098 established idiom).
            await CardEffectCommons.AddThisCardToHand(card, card);
        }

        return activateClass;
    }

    ActivateClass MainEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());

        string EffectDiscription()
            => "[Main] You may link this card to 1 of your Digimon without paying the cost. Then, delete 1 of your opponent's Digimon with as much or less DP as 1 of your Digimon.";

        bool CanUseCondition(Hashtable hashtable)
            => CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            #region Select Digimon To Link

            bool CanLinkToPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                        card.CanLinkToTargetPermanent(permanent, false);
            }

            if (CardEffectCommons.HasMatchConditionPermanent(card, CanLinkToPermanentCondition))
            {
                Permanent? selectedPermanent = null;
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanLinkToPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentToLinkCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to link.", "The opponent is selecting 1 Digimon to link.");

                await selectPermanentEffect.Activate();

                Task SelectPermanentToLinkCoroutine(Permanent permanent)
                {
                    selectedPermanent = permanent;
                    return Task.CompletedTask;
                }

                if (selectedPermanent != null)
                {
                    // AS-IS :154 `AddLinkCard(card, activateClass)`(ICardEffect) — 미러 시그니처는
                    // `HeadlessEntityId?` 인자(Permanent.cs:3976) → `activateClass.EffectSourceCard?.InstanceId`
                    // (AddDigivolutionCardsBottom과 동형 어댑테이션, BT17_026:316-317 idiom).
                    await selectedPermanent.AddLinkCard(card, activateClass.EffectSourceCard?.InstanceId);
                }
            }

            #endregion

            #region Delete Digimon Setup

            Permanent? selectedOwnerDigimon = null;

            List<Permanent> ownerDigimonList = card.Owner.GetBattleAreaDigimons();
            List<Permanent> opponentDigimonList = new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons();

            int highestDp = ownerDigimonList.Count > 0 ? ownerDigimonList.Max(x => x.DP) : -1;

            bool CanSelectOwnerDigimon(Permanent permanent)
                    => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);

            bool CanSelectOpponentDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                       permanent.DP <= selectedOwnerDigimon!.DP;
            }

            #endregion

            #region Select Digimon to Compare

            // Comparing to our highest DP is just used as a fast way to check if there is a least 1 valid selection.
            if (ownerDigimonList.Count > 0 && opponentDigimonList.Any(x => x.DP <= highestDp))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectOwnerDigimon,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 of your Digimon.", "The opponent is selecting 1 Digimon");

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedOwnerDigimon = permanent;
                    return Task.CompletedTask;
                }

                await selectPermanentEffect.Activate();
            }

            #endregion

            #region Select Digimon To Delete

            if (selectedOwnerDigimon != null && opponentDigimonList.Any(x => x.DP <= selectedOwnerDigimon.DP))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectOpponentDigimon,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete");

                await selectPermanentEffect.Activate();
            }

            #endregion
        }

        return activateClass;
    }

    ActivateClass LinkAction(CardSource card)
    {
        return CardEffectFactory.LinkEffect(card);
    }

    AddLinkConditionClass LinkCondition(CardSource card)
    {
        static bool PermanentCondition(Permanent targetPermanent)
        {
            return targetPermanent.TopCard.HasLevel && targetPermanent.Level >= 3;
        }

        return CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 2, card: card);
    }

    ActivateClass EndOfTurnLinkedEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("This digimon may attack", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
        activateClass.SetIsLinkedEffect(true);
        activateClass.SetHashString("EOT_ST22_08");

        string EffectDiscription() => "[End of Your Turn] this Digimon may attack.";

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                   ICardEffect.ResolvePermanentOfThisCard(card).CanAttack(activateClass);
        }

        async Task ActivateCoroutine(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleArea(card))
            {
                if (ICardEffect.ResolvePermanentOfThisCard(ICardEffect.ResolvePermanentOfThisCard(card).TopCard).CanAttack(activateClass))
                {
                    SelectAttackEffect selectAttackEffect = GManager.instance.GetComponent<SelectAttackEffect>();

                    selectAttackEffect.SetUp(
                        attacker: ICardEffect.ResolvePermanentOfThisCard(ICardEffect.ResolvePermanentOfThisCard(card).TopCard),
                        canAttackPlayerCondition: () => true,
                        defenderCondition: (permanent) => true,
                        cardEffect: activateClass);

                    await selectAttackEffect.Activate();
                }
            }
        }

        return activateClass;
    }
}
