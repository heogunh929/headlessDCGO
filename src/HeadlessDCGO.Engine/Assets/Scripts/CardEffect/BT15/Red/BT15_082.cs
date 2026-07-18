// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT15_082 (Tamer / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT15/Red/BT15_082.cs (140 lines, no region markers)
//    * [Start of Your Turn] :13-16 (timing == OnStartTurn — SetMemoryTo3TamerEffect)
//    * [All Turns]          :18-131(timing == OnReturnCardsToHandFromTrash + CanTriggerWhenCardsReturnToHandFromTrash
//                                    게이트) — 자신을 손패로 반환하여 13000DP 이하 red Digimon 손패 플레이 무료.
//    * [Security]           :133-136(timing == SecuritySkill — PlaySelfTamerSecurityEffect)
//
// ② 프리미티브 매핑:
//    * P:SetMemoryTo3TamerEffect — 자기 턴 시작 시 메모리 3으로 설정 (AS-IS :15).
//    * P:BouncePeremanentAndProcessAccordingToResult — 자신을 손패로 반환 (AS-IS :89).
//    * P:CanPlayAsNewPermanent — 손패 카드 후보 필터 (AS-IS :70).
//    * P:PlayPermanentCards — 선택 카드 플레이 (AS-IS :127).
//    * P:PlaySelfTamerSecurityEffect — [Security] 등재 (AS-IS :135).
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [All Turns] → EffectTiming.OnReturnCardsToHandFromTrash + CanTriggerWhenCardsReturnToHandFromTrash
//      (hashtable, cardCondition, card) 게이트(AS-IS :44 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      lone `yield return null`→`Task.CompletedTask`(BT8_092 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`(BT8_092 관례).
//    * `card.Owner.Enemy.SecurityCards` → `new Player(card.Context, card.Owner).Enemy!.SecurityCards`.
//    * `cardSource.HasCardColor(CardColor.Red)`(AS-IS enum) → `cardSource.HasCardColor("Red")`(미러
//      string-색 idiom, BT2_044/LM_054 헤더).
//    * AS-IS `HasAvianBeastAnimalTraits`(CardSource.cs:1930-1948, 미러 미존재) — 1차 프리미티브
//      `ContainsTraits(fragment)` 조립으로 인라인(BT9_111 편의-프로퍼티 인라인 판례): Avian/Bird/Beast 포함,
//      Animal 포함 && !Sea Animal 포함, Sovereign 포함.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT15_082 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnReturnCardsToHandFromTrash)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return this Tamer to your hand to play a Digimon from your hand.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] When a red Digimon card returns from your trash to the hand, by returning this Tamer to the hand, you may play 1 13000 DP or less red Digimon card with [Avian], [Bird], [Beast], [Animal] or [Sovereign], other than [Sea Animal] in one of its traits from your hand without paying the cost. For each of your opponent's security cards, remove 2000 from this effect's playable card's DP maximum.";
            }

            bool CardReturnedCondition(CardSource cardSource)
            {
                if (cardSource.Owner == card.Owner)
                {
                    return cardSource.HasCardColor("Red");
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenCardsReturnToHandFromTrash(hashtable, CardReturnedCondition, card))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            // AS-IS CardSource.HasAvianBeastAnimalTraits (:1930-1948) — 미러 미존재, ContainsTraits 조립으로 인라인.
            bool HasAvianBeastAnimalTraits(CardSource cardSource)
            {
                if (cardSource.ContainsTraits("Avian")) return true;
                if (cardSource.ContainsTraits("Bird")) return true;
                if (cardSource.ContainsTraits("Beast")) return true;
                if (cardSource.ContainsTraits("Animal") && !cardSource.ContainsTraits("Sea Animal")) return true;
                if (cardSource.ContainsTraits("Sovereign")) return true;
                return false;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                int subAmount = new Player(card.Context, card.Owner).Enemy!.SecurityCards.Count * 2000;
                int cardDP = 13000 - subAmount;

                if (cardSource.CardDP <= cardDP && cardSource.HasCardColor("Red") && cardSource.Owner == card.Owner)
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false,
                            cardEffect: activateClass))
                    {
                        if (HasAvianBeastAnimalTraits(cardSource))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent? bounceTargetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                if (bounceTargetPermanent != null)
                {
                    await CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(
                        targetPermanents: new List<Permanent>() { bounceTargetPermanent },
                        activateClass: activateClass,
                        successProcess: SuccessProcess,
                        failureProcess: null);

                    async Task SuccessProcess()
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

                        await CardEffectCommons.PlayPermanentCards(cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Hand, activateETB: true);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }
        return cardEffects;
    }
}
