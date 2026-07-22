// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — BT25_034 "Angemon" (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Yellow/BT25_034.cs (4 regions)
//    * timing==None                       : AddSelfDigivolutionRequirementStaticEffect(level 3, [TS] 위 cost 2).
//    * timing==OnDiscardSecurity          : [When Trashed] 시큐리티에서 트래시될 때 손패의 level≤4 [Angel]/[Iliad]
//      1장 무료 플레이 (SelectHandEffect Mode.PlayForFree).
//    * timing==OnDestroyedAnyone          : AscensionSelfEffect (PRIMARY covered element: Ascension).
//    * timing==WhenPermanentWouldBeDeleted: BarrierSelfEffect(inherited).
// 치환(substrate translations only): IEnumerator→async Task, `yield return StartCoroutine(X)`→`await X`.
//   AS-IS 팩토리(AscensionSelfEffect/BarrierSelfEffect/AddSelfDigivolutionRequirementStaticEffect)는 미러 1:1.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_034 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool Condition(Permanent permanent)
            {
                return permanent.TopCard.HasTSTraits;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(level: 3, permanentCondition: Condition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region When Trashed
        if (timing == EffectTiming.OnDiscardSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 level 4 or lower [Angel] or [Iliad] card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "When effects trashing this card from the security stack, you may play 1 level 4 or lower [Angel] or [Iliad] trait card from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnTrashSelfSecurity(hashtable, cardEffect => cardEffect != null, card);
            }

            bool CanPlayCardCondition(CardSource cardSource)
            {
                return cardSource.HasPlayCost
                    && cardSource.HasLevel
                    && cardSource.Level <= 4
                    && (cardSource.EqualsTraits("Angel") || cardSource.EqualsTraits("Iliad"))
                    && CardEffectCommons.CanPlayAsNewPermanent(card, false, activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnTrash(card)
                    && CardEffectCommons.HasMatchConditionOwnersHand(card, CanPlayCardCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanPlayCardCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.PlayForFree,
                    cardEffect: activateClass);

                await selectHandEffect.Activate();
            }
        }
        #endregion

        #region Ascension
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.AscensionSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Barrier
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(true, card, null));
        }
        #endregion

        return cardEffects;
    }
}
