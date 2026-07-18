// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — BT18_098 (Option / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT18/Yellow/BT18_098.cs (226 lines, 4 regions)
//    * When Trashed from Security :15-46  (timing == OnDiscardSecurity — 자기 <Security> 효과 재발화)
//    * Ignore Color Requirements  :50-77  (timing == None — 자기 Yellow+[Data]/[Witchelny] 디지몬 보유 시 색
//      요구 무시)
//    * Main                       :81-156 (timing == OptionSkill — 시큐리티 1장 트래시→상대 DP -6000→조건부
//      바닥 시큐리티 배치)
//    * Security                   :160-220 (timing == SecuritySkill — 6000DP 이하 삭제 or <Recovery +1>)
//
// ② 프리미티브 매핑:
//    * P:CanTriggerOnTrashSelfSecurity(WhenDiscardSecurity bridge) + OptionSecurityEffect 재조립 — [When
//      Trashed from Security] (AS-IS :29/:39-45)
//    * P:IgnoreColorConditionClass — 색 요구 무시(자기 한정) (AS-IS :52-76)
//    * P:IDestroySecurity + E:SelectPermanentEffect Mode.Custom + P:ChangeDigimonDP + CardObjectController.
//      AddSecurityCard — [Main] (AS-IS :112-155)
//    * E:SelectPermanentEffect Mode.Destroy + P:IRecovery — [Security] (AS-IS :193-219)
//
// ③ 배선 관례 근거:
//    * [When Trashed from Security] → OnDiscardSecurity + CanTriggerOnTrashSelfSecurity 게이트(AS-IS :29 그대로).
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect(hashtable, card) 게이트(AS-IS :102 그대로).
//    * [Security] → SecuritySkill + CanTriggerSecurityEffect(hashtable, card) 게이트(AS-IS :188 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT17_026 idiom).
//    * `CardEffectCommons.CanTriggerOnTrashSelfSecurity(hashtable, cardEffect => cardEffect != null, card)` —
//      미러 Hashtable-형 브릿지(WhenDiscardSecurity.cs:12, `(Hashtable, Func<ICardEffect,bool>, CardSource)`)
//      AS-IS 파라미터 순서 그대로.
//    * `CardEffectCommons.OptionSecurityEffect(card)` — 미러에 해당 브릿지 없음. AS-IS 게터 몸통(CardEffectCommons.cs
//      :717: `card.EffectList(EffectTiming.SecuritySkill).Find(e => e is ActivateClass &&
//      e.EffectDiscription.Contains("[Security]")) as ActivateClass`)을 이미 존재하는 CardSource.EffectList/
//      ICardEffect.EffectDiscription 1차 프리미티브로 카드 파일 내부에 인라인 조립(발명 아님 — EX8_037의
//      OptionCardColorsOf와 동일 성격).
//    * `permanet.TopCard.CardColors.Contains(CardColor.Yellow)` → `permanent.TopCard.HasCardColor("Yellow")`
//      (색 enum→string, LM_054/BT2_044 idiom).
//    * `card.Owner.SecurityCards` → `new Player(card.Context, card.Owner).SecurityCards` (미러 Player 접근 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT18.Yellow;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT18_098 : CEntity_Effect
{
    // AS-IS CardEffectCommons.OptionSecurityEffect (CardEffectCommons.cs:717) — no mirror bridge; assembled
    // here from the primitives that DO exist (CardSource.EffectList / ICardEffect.EffectDiscription).
    private static ActivateClass? OptionSecurityEffectOf(CardSource card)
    {
        foreach (ICardEffect cardEffect in card.EffectList(EffectTiming.SecuritySkill))
        {
            if (cardEffect is ActivateClass activateClass && activateClass.EffectDiscription.Contains("[Security]"))
            {
                return activateClass;
            }
        }

        return null;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region When Trashed from Security
        if (timing == EffectTiming.OnDiscardSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Activate the security effect", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When this card is trashed from your security stack,  activate its <Security> effects.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnTrashSelfSecurity(hashtable, cardEffect => cardEffect != null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnTrash(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                ActivateClass? mainActivateClass = OptionSecurityEffectOf(card);

                if (mainActivateClass != null)
                {
                    await mainActivateClass.Activate(CardEffectCommons.OptionMainCheckHashtable(card));
                }
            }
        }
        #endregion

        #region Ignore Color Requirements
        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);

            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).GetBattleAreaDigimons().Count((permanet) => permanet.TopCard.HasCardColor("Yellow") && permanet.TopCard.EqualsTraits("Data") || permanet.TopCard.EqualsTraits("Witchelny")) >= 1)
                {
                    return true;
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
            activateClass.SetUpICardEffect("Trash your 1 security and DP -6000", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] By trashing your top security card, 1 of your opponent's Digimon gets -6000 DP until the end of their turn. Then, if you have 2 or fewer security cards, place this card as your bottom security card.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
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
                if (new Player(card.Context, card.Owner).SecurityCards.Count >= 1)
                {
                    if (CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card))
                    {
                        return true;
                    }
                }

                return false;
            }


            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IDestroySecurity(
                    card.Context,
                    card.Owner,
                    1,
                    activateClass,
                    fromTop: true).DestroySecurity();

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

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

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon that will get DP -6000.",
                        "The opponent is selecting 1 Digimon that will get DP -6000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: -6000, effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass: activateClass);
                    }
                }

                if (new Player(card.Context, card.Owner).SecurityCards.Count <= 2)
                {
                    await CardObjectController.AddSecurityCard(card, toTop: false);
                }
            }
        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 opponent Digimon with 6000DP or less and Recovery +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Delete 1 of your opponent's Digimon with 6000 DP or less. Then, if you have 0 security cards, <Recovery +1>";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= 6000)
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
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

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

                if (new Player(card.Context, card.Owner).SecurityCards.Count == 0)
                {
                    await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
