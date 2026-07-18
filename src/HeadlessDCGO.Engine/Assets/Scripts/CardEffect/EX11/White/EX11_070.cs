// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T1 정본 카드 — Unchained (EX11_070, Tamer / White / LIBERATOR)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX11/White/EX11_070.cs (162 lines, 5 regions)
//    * [Security]                  :15-19  (SecuritySkill → CardEffectFactory.PlaySelfTamerSecurityEffect)
//    * [Start of Your Turn]        :22-26  (OnStartTurn → CardEffectFactory.SetMemoryTo3TamerEffect)
//    * [End of Your Turn]          :29-87  (OnEndTurn — DNA digivolve [ExMaquinamon] + <Mind Link>)
//    * ESS [All Turns]             :91-147 (None — ChangeDPClass 최소 1000 DP + ImmuneStackTrashingClass)
//    * ESS De-MindLink             :151-157(OnEndTurn → CardEffectFactory.PlayMindLinkTamerFromDigivolutionCards)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #5):
//    * K:MindLink                            — [End of Your Turn] 몸통의 MindLinkClass 프로세스
//    * P:ChangeDPClass                       — ESS "can't have less than 1000 DP" (AS-IS :101-126)
//    * P:ImmuneStackTrashingClass            — ESS "isn't affected by trashing stacked cards" (AS-IS :129-145)
//    * P:MindLinkClass                       — KeyWordEffects/MindLink.cs 프로세스 클래스 직접 구동 (AS-IS :78-85)
//    * P:PlayMindLinkTamerFromDigivolutionCards — ESS De-MindLink (AS-IS :153-156)
//    * (+P:PlaySelfTamerSecurityEffect, P:SetMemoryTo3TamerEffect, X:Jogress/DNA
//       (DNADigivolvePermanentsIntoHandOrTrashCard), T:OnStartTurn, T:OnEndTurn, T:SecuritySkill)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [End of Your Turn]/[End of All Turns] → OnEndTurn 등록(AS-IS 타이밍 그대로; CardEffectRegistrar
//      AllTimings에 OnEndTurn 포함 — EX8-3 관례) + IsOwnerTurn 게이트(AS-IS :48-52)로 자기-턴 한정.
//    * ESS(상속효과)는 SetIsInheritedEffect(true) — 진화원에 실릴 때만 발화(AS-IS :104/:133 그대로).
//    * [Security]는 SecuritySkill 타이밍 팩토리 등록(SecurityResolver가 해소).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (가변 Permanent 필요
//      지점 — BT8_092 헤더 관례; PermanentOfThisCard()는 PermanentView 반환).
//    * `new MindLinkClass(...).MindLink()` (AS-IS 단일 코루틴: 내부 select+배치) → 미러 MindLinkClass의
//      확립 분할 표면(MindLink.cs:25-27 헤더): BuildRequest() → context.ChoiceProvider.ChooseAsync →
//      MindLink(selectedId). 선택 optional(canNoSelect:true)=skip 허용은 BuildRequest가 1:1 보존.
//    * `TopCard.HasText("Maquinamon")` — 미러 CardSource.HasText 신설 1:1 이식(AS-IS CardSource.cs:2120;
//      본 트랜치에서 미러됨 — CardSource.cs "EXEMPLAR-T1" HasText 주석 참조).
//    * `permanent.cardSources.Contains(card)` / `permanent.DigivolutionCards.Contains(card)` — 미러
//      Permanent 동일 표면(InstanceId 기반 Equals) 그대로.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX11.White;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX11_070 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Security

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        #endregion

        #region Start of turn set to 3

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        #endregion

        #region End of your Turn

        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DNA digivolve into [ExMaquinamon]. Mind Link to [Maquinamon] in text.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[End of Your Turn] 2 of your Digimon may DNA digivolve into [ExMaquinamon] in the hand. Then, this Tamer may <Mind Link> with 1 of your Digimon with [Maquinamon] in its text.";
            }

            bool CanSelectDNACardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && cardSource.CanPlayJogress(true)
                    && cardSource.EqualsCardName("ExMaquinamon");
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            bool IsMyProperDigimon(Permanent permanent)
            {
                return permanent.IsDigimon
                    && permanent.TopCard.HasText("Maquinamon");
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {

                #region DNA digivolve
                await CardEffectCommons.DNADigivolvePermanentsIntoHandOrTrashCard(
                        CanSelectDNACardCondition,
                        payCost: true,
                        isHand: true,
                        activateClass
                    );
                #endregion
                #region Mind Link
                // AS-IS :78-85 `new MindLinkClass(tamer, digimonCondition, activateClass).MindLink()` —
                // 미러 확립 분할 표면(MindLink.cs 헤더): BuildRequest → ChooseAsync → MindLink(선택 id).
                MindLinkClass mindLinkClass = new MindLinkClass(
                    tamer: ICardEffect.ResolvePermanentOfThisCard(card),
                    digimonCondition: IsMyProperDigimon,
                    activateClass: activateClass
                );

                if (mindLinkClass.Candidates().Count >= 1)
                {
                    ChoiceResult mindLinkResult = await card.Context.ChoiceProvider.ChooseAsync(mindLinkClass.BuildRequest());

                    if (!mindLinkResult.IsSkipped && mindLinkResult.SelectedIds.Count >= 1)
                    {
                        await mindLinkClass.MindLink(mindLinkResult.SelectedIds[0]);
                    }
                }
                #endregion
            }
        }

        #endregion

        #region ESS - All turns

        if (timing == EffectTiming.None)
        {
            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && ICardEffect.ResolvePermanentOfThisCard(card).TopCard.HasText("Maquinamon");
            }

            #region Can't have less than 1000 DP
            {
                ChangeDPClass changeDPClass = new ChangeDPClass();
                changeDPClass.SetUpICardEffect("Can't have less than 1000 DP", CanUseCondition, card);
                changeDPClass.SetUpChangeDPClass(ChangeDP: ChangeDP, permanentCondition: PermanentCondition, isUpDown: () => false, isMinusDP: () => false);
                changeDPClass.SetIsInheritedEffect(true);
                cardEffects.Add(changeDPClass);



                int ChangeDP(Permanent permanent, int DP)
                {
                    if (PermanentCondition(permanent) && DP < 1000)
                    {
                        DP = 1000;
                    }

                    return DP;
                }

                bool PermanentCondition(Permanent permanent)
                {
                    return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent)
                        && !permanent.TopCard.CanNotBeAffected(changeDPClass)
                        && permanent.DigivolutionCards.Contains(card)
                        && permanent.TopCard.HasText("Maquinamon");
                }
            }
            #endregion
            #region Immunity to Stack Trashing
            {
                ImmuneStackTrashingClass immuneFromStackTrashingClass = new ImmuneStackTrashingClass();
                immuneFromStackTrashingClass.SetUpICardEffect("Isn't affected by trashing any stacked card", CanUseCondition, card);
                immuneFromStackTrashingClass.SetUpImmuneFromStackTrashingClass(PermanentCondition: PermanentCondition, EffectCondition: EffectCondition);
                immuneFromStackTrashingClass.SetIsInheritedEffect(true);
                cardEffects.Add(immuneFromStackTrashingClass);

                bool EffectCondition(ICardEffect effect)
                {
                    // AS-IS IsOpponentEffect(ICardEffect, card) (GameContextDeterminarion.cs:808-816)는
                    // effect.EffectSourceCard의 소유자를 검사 — 미러 오버로드는 소스카드-형(:4162)이라
                    // EffectSourceCard를 풀어 전달(판정 로직 1:1 동일).
                    return CardEffectCommons.IsOpponentEffect(effect?.EffectSourceCard, card);
                }

                bool PermanentCondition(Permanent permanent)
                {
                    return permanent.cardSources.Contains(card);
                }
            }
            #endregion
        }

        #endregion

        #region ESS - De-MindLink

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.PlayMindLinkTamerFromDigivolutionCards(
                                                card,
                                                "Unchained",
                                                "[End of All Turns] You may play 1 [Unchained] from this Digimon's digivolution cards without paying the cost."));
        }

        #endregion

        return cardEffects;
    }
}
