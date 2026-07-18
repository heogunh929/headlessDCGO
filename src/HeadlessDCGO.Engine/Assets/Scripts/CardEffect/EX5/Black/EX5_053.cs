// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 카드 — Baihumon (EX5_053, Digimon / Black / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX5/Black/EX5_053.cs (200 lines, 4 regions)
//    * [None] alt-digivolve   :13-21  (AddSelfDigivolutionRequirementStaticEffect: [Deva]Lv.5 위 코스트3)
//    * [Counter] Blast Digi   :23-26  (OnCounterTiming — BlastDigivolveEffect)  ← 잠복 STOP(아래 ③ 참조)
//    * [Security check]        :28-127 (OnSecurityCheck — 체크 카드가 [Deva] 디지몬이면 전투없이·무료 플레이)
//    * [On Deletion]          :129-196(OnDestroyedAnyone — 상대 최고 플레이코스트 디지몬 삭제)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #27):
//    * P:DontBattleSecurityDigimonClass — [Security check] 몸통 (AS-IS :84-87; UntilSecurityCheckEndEffects)
//    * T:OnSecurityCheck                — [Security check] 창 (AS-IS :28; SecurityResolver W4 창)
//    * (+P:PlayPermanentCards(root Security) (AS-IS :117-123), CanPlayAsNewPermanent, IsMaxCost,
//       E:SelectPermanentEffect Mode.Destroy, GetCardFromHashtable, IsOpponentTurn, CanUseIgnoreBattle)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules) + 잠복 STOP:
//    * [Security check] → OnSecurityCheck 창(SecurityResolver.RunSecurityCheckLoopAsync W4가 실해소 —
//      SecurityResolver.cs:158-180). CanUseCondition의 hashtable-체크-카드 술어는 AS-IS 1:1.
//      · 행동-수확 RD-EXT2B-02(EXEMPLAR-T2B W1 실측): OnSecurityCheck 창은 발화하고 이 리액터가 활성화(Deva 감지
//        + DontBattle 부여 + 자기-플레이 시도)하나, 미러 시큐리티-체크 루프가 공개 카드를 소비/이동한 뒤라
//        PlayPermanentCards(root:Security)가 착지하지 못한다(play-from-security 순서 갭). 카드 포팅은 AS-IS 1:1로
//        정확 — 갭은 SecurityResolver 루프 측. 상환 시 witness를 뒤집어 자기-플레이 착지를 검증할 것.
//    * [Counter] BlastDigivolveEffect(card, null) — 미러 팩토리(KeyWordEffects/BlastDigivolution.cs)는
//      **잠복 STOP RD-P6C2-11**: 등록·CanUse는 안전하나 카운터가 실제 해소되면 CanActivate/ActivateCoroutine이
//      NotSupportedException(Permanent.PermanentFrame 미러 부재). 정본 1:1 유지 — 이 카드의 T2B 타깃 축
//      (OnSecurityCheck)은 별개 리전이라 클린. Blast 카운터를 실발화하는 witness는 만들지 않음(수확 원장 참조).
//    * [On Deletion] → OnDestroyedAnyone + CanTriggerOnDeletion/CanActivateOnDeletion(AS-IS :154-170).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X (BT8_092 idiom).
//    * `card.Owner.UntilSecurityCheckEndEffects` → `new Player(card.Context, card.Owner).*` (미러 Player idiom).
//    * AS-IS :102-106 `CanPlayAsNewPermanent(cardSource, payCost, cardEffect, root: Security)` — 미러
//      CanPlayAsNewPermanent는 root 파라미터 없음(substrate drop, BT19_091 동일 idiom) → root 인자 생략.
//    * AS-IS :108 `GManager.instance.attackProcess.SecurityDigimon = null` — 미러 AttackProcess.SecurityDigimon
//      (게임 상태) 동일 대입.
//    * AS-IS :110-115 `brainStormObject.CloseBrainstrorm`/`ShowUseHandCard`/`ShrinkUpUseHandCard` = UI 연출 —
//      미러 확립 관례상 스트립(앵커 병기).
//    * SelectPermanentEffect.canTargetCondition는 id-형 — Permanent-술어에 PermanentOf(id) 어댑터 덧댐.
//    * `IsMaxCost(permanent, card.Owner.Enemy, true)` → `IsMaxCost(permanent, OpponentOf(card), true)` (미러 HeadlessPlayerId).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX5.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX5_053 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.CardTraits.Contains("Deva") && targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 5;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Blast Digivolve (latent STOP RD-P6C2-11)
        if (timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CardEffectFactory.BlastDigivolveEffect(card: card, condition: null));
        }
        #endregion

        #region Security check - play without battle
        if (timing == EffectTiming.OnSecurityCheck)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play the security Digimon without battle", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("PlaySecurity_EX5_053");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Opponent's Turn] [Once Per Turn] When your security is checked, if that card is a Digimon card with the [Deva] trait, play it without battling and without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOpponentTurn(card))
                    {
                        CardSource securityCard = CardEffectCommons.GetCardFromHashtable(hashtable);

                        if (securityCard != null)
                        {
                            if (securityCard.Owner == card.Owner)
                            {
                                if (securityCard.IsDigimon)
                                {
                                    if (securityCard.CardTraits.Contains("Deva"))
                                    {
                                        return true;
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
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                CardSource securityCard = CardEffectCommons.GetCardFromHashtable(_hashtable);

                if (securityCard != null)
                {
                    DontBattleSecurityDigimonClass dontBattleSecurityDigimonClass = new DontBattleSecurityDigimonClass();
                    dontBattleSecurityDigimonClass.SetUpICardEffect("Ignore Battle", CanUseCondition, card);
                    dontBattleSecurityDigimonClass.SetUpDontBattleSecurityDigimonClass(CardSourceCondition: CardSourceCondition);
                    new Player(card.Context, card.Owner).UntilSecurityCheckEndEffects.Add(_timing => dontBattleSecurityDigimonClass);

                    bool CanUseCondition(Hashtable hashtable)
                    {
                        return CardEffectCommons.CanUseIgnoreBattle(hashtable, securityCard);
                    }

                    bool CardSourceCondition(CardSource cardSource)
                    {
                        return cardSource == securityCard;
                    }
                }

                if (securityCard != null)
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(
                        cardSource: securityCard,
                        payCost: false,
                        cardEffect: activateClass))
                    {
                        // AS-IS :108 게임 상태: 이후 W5 배틀이 이 시큐리티 디지몬을 배틀하지 않도록 클리어.
                        GManager.instance.attackProcess.SecurityDigimon = null;

                        // AS-IS :110-115 brainStormObject.CloseBrainstrorm / ShowUseHandCard / ShrinkUpUseHandCard
                        //   = UI 연출 — 미러 확립 관례상 스트립.

                        await CardEffectCommons.PlayPermanentCards(
                            cardSources: new List<CardSource>() { securityCard },
                            activateClass: activateClass,
                            payCost: false,
                            isTapped: false,
                            root: SelectCardEffect.Root.Security,
                            activateETB: true);
                    }
                }
            }
        }
        #endregion

        #region On Deletion
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete opponent's all Digimon with the highest DP", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Deletion] Delete 1 of your opponent's highest play cost Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (CardEffectCommons.IsMaxCost(permanent, CardEffectCommons.OpponentOf(card), true))
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

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanActivateOnDeletion(hashtable, card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
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
