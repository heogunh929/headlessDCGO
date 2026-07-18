// Source: DCGO/Assets/Scripts/CardEffect/EX4/Blue/EX4_062.cs (1:1 mirror) — "KirihaAonuma&NeneAmano" Tamer (Blue Flare/Twilight DigiXros support).
//
// S6 롱테일 트랜치 — 감사 시절 ⚠STOP-예상(DigiXros 계열)이었으나 표면 실존 확인(S5 3/3 관통 사례 연장):
//   * [None]x2 — ChangeCardNamesClass (Kiriha Aonuma / Nene Amano로도 취급) — 미러 1:1 존재.
//   * [Start of Your Main Phase] — ActivateClass "Memory +1"(card.Owner.CanAddMemory/AddMemory 확장 메서드,
//     PlayerId에 직접 매달림 — symbol_map_guide §2.2 예외).
//   * [BeforePayCost] — "Can select DigiXros cards from Tamer's digivolution cards from trash": 자기 서스펜드 후
//     AddMaxUnderTamerCountDigiXrosClass/AddMaxTrashCountDigiXrosClass를 UntilCalculateFixedCostEffect에 등재
//     (둘 다 미러 1:1 kind-class, CardEffectFactory 없이 카드가 직접 new).
//   * [Security] — CardEffectFactory.PlaySelfTamerSecurityEffect(card) 팩토리 그대로.
//
// 치환(substrate translations only): IEnumerator→async Task, `ContinuousController.instance.StartCoroutine(X)`→
// `await X`; `isExistOnField(card)` = CEntity_Effect 정적 헬퍼(1:1, CardEffectCommons 네임스페이스 상속선상);
// `new SuspendPermanentsClass(list, hashtable).Tap()`(AS-IS 해시테이블 ctor) → `new SuspendPermanentsClass(list,
// activateClass, isBlock:false).Tap()`(BT8_092/EX11_074 idiom); UI만인 `ContinuousController.instance.PlaySE(...)`
// 스트립.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX4.Blue;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class EX4_062 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Also treated as [Kiriha Aonuma]
        if (timing == EffectTiming.None)
        {
            ChangeCardNamesClass changeCardNamesClass = new ChangeCardNamesClass();
            changeCardNamesClass.SetUpICardEffect("Also treated as [Kiriha Aonuma]", CanUseCondition, card);
            changeCardNamesClass.SetUpChangeCardNamesClass(changeCardNames: ChangeCardNames);

            cardEffects.Add(changeCardNamesClass);

            bool CanUseCondition(Hashtable hashtable) => true;

            List<string> ChangeCardNames(CardSource cardSource, List<string> cardNames)
            {
                if (cardSource == card)
                {
                    cardNames.Add("Kiriha Aonuma");
                    cardNames.Add("KirihaAonuma");
                }

                return cardNames;
            }
        }
        #endregion

        #region Also treated as [Nene Amano]
        if (timing == EffectTiming.None)
        {
            ChangeCardNamesClass changeCardNamesClass = new ChangeCardNamesClass();
            changeCardNamesClass.SetUpICardEffect("Also treated as [Nene Amano]", CanUseCondition, card);
            changeCardNamesClass.SetUpChangeCardNamesClass(changeCardNames: ChangeCardNames);

            cardEffects.Add(changeCardNamesClass);

            bool CanUseCondition(Hashtable hashtable) => true;

            List<string> ChangeCardNames(CardSource cardSource, List<string> cardNames)
            {
                if (cardSource == card)
                {
                    cardNames.Add("Nene Amano");
                    cardNames.Add("NeneAmano");
                }

                return cardNames;
            }
        }
        #endregion

        #region Start of Your Main Phase — Memory +1
        if (timing == EffectTiming.OnStartMainPhase)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() => "[Start of Your Main Phase] If there are 2 or more Digimon in play, gain 1 memory.";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && card.Owner.GetBattleAreaDigimons().Count + new Player(card.Context, card.Owner).Enemy!.PlayerId.GetBattleAreaDigimons().Count >= 2
                && card.Owner.CanAddMemory(activateClass);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }
        #endregion

        #region [All Turns] Can select DigiXros cards from Tamer's digivolution cards / trash
        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Can select DigiXros cards from Tamer's digivolution cards from trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetHashString("CanSelectDigiXrosFromTamer_EX4_062");
            cardEffects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When you would play 1 Digimon card with the [Blue Flare] or [Twilight] trait with DigiXros requirements, by suspending this Tamer, you may place 1 card from under your Tamers and 1 card from your trash as digivolution cards for a DigiXros.";

            bool CardCondition(CardSource cardSource) =>
                cardSource.Owner == card.Owner
                && cardSource.IsDigimon
                && cardSource.HasDigiXros
                && (cardSource.CardTraits.Contains("Blue Flare") || cardSource.CardTraits.Contains("BlueFlare") || cardSource.CardTraits.Contains("Twilight"));

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition)
                && CardEffectCommons.IsOnly1CardPlayed(hashtable);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.CanActivateSuspendCostEffect(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);
                    if (!thisPermanent.IsSuspended && thisPermanent.CanSuspend)
                    {
                        await new SuspendPermanentsClass(new List<Permanent>() { thisPermanent }, activateClass, isBlock: false).Tap();

                        // AS-IS `ContinuousController.instance.PlaySE(...BuffSE)` = UI (stripped).

                        AddMaxUnderTamerCountDigiXrosClass addMaxTamerCountDigiXrosClass = new AddMaxUnderTamerCountDigiXrosClass();
                        addMaxTamerCountDigiXrosClass.SetUpICardEffect("Can select DigiXros cards from Tamer's digivolution cards", CanUseCondition1, card);
                        addMaxTamerCountDigiXrosClass.SetUpAddMaxUnderTamerCountDigiXrosClass(getMaxUnderTamerCount: GetCount);
                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => addMaxTamerCountDigiXrosClass);

                        bool CanUseCondition1(Hashtable ht) => true;

                        int GetCount(CardSource cardSource) => CardSourceCondition(cardSource) ? 1 : 0;

                        bool CardSourceCondition(CardSource cardSource) =>
                            cardSource != null && cardSource.Owner == card.Owner && cardSource.HasDigiXros;

                        AddMaxTrashCountDigiXrosClass addMaxTrashCountDigiXrosClass = new AddMaxTrashCountDigiXrosClass();
                        addMaxTrashCountDigiXrosClass.SetUpICardEffect("Can select DigiXros cards from trash", CanUseCondition1, card);
                        addMaxTrashCountDigiXrosClass.SetUpAddMaxTrashCountDigiXrosClass(getMaxTrashCount: GetCount);
                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => addMaxTrashCountDigiXrosClass);
                    }
                }
            }
        }
        #endregion

        #region [Security] Play this Tamer for free
        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }
        #endregion

        return cardEffects;
    }
}
