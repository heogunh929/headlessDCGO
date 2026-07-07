// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_061.cs
//   [On Play] 2 of your opponent's Digimon get -3000 DP for the turn.
// AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone.
//   CanUseCondition = CanTriggerOnPlay. CanActivateCondition = IsExistOnBattleArea(card) &&
//   HasMatchConditionPermanent(CanSelectPermanentCondition), where CanSelectPermanentCondition(permanent) =
//   IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card). ORDER=-1, ISOPTIONAL=false.
//   ActivateCoroutine: maxCount = Math.Min(2, MatchConditionPermanentCount(CanSelectPermanentCondition));
//   SelectPermanentEffect.SetUp(selectPlayer: card.Owner, canTargetCondition, maxCount, canNoSelect: false,
//   canEndNotMax: false, mode: Custom) — a MANDATORY select of exactly maxCount targets (not "up to 2": the
//   card text has no "up to", and canEndNotMax:false forces minCount == maxCount, i.e. the owner must pick
//   min(2, available) opponent Digimon, never fewer). For each selected permanent, calls
//   CardEffectCommons.ChangeDigimonDP(changeValue: -3000, effectDuration: UntilEachTurnEnd).
//
// STOP: no headless primitive exposes a MANDATORY (canEndNotMax:false) multi-target (maxCount>1) DP-delta
// select+buff. The only matching-shape primitive, `CardEffectFactory.SelectAndBuffDpEffect(card, canTarget,
// maxCount, changeValue, duration, description)` (CardPortingFramework.cs, backed by `ActivatedTargetBuffEffect`),
// hardcodes `canEndNotMax: maxCount > 1` in its `_select.SetUp(...)` call — for maxCount=2 that always yields
// canEndNotMax=true (SelectPermanentEffect.cs: minCount = canEndNotMax ? Math.Min(1, maxCount) : maxCount),
// i.e. the owner could stop after selecting only 1 opponent Digimon even with 2 legal targets available. That
// contradicts this card's literal AS-IS canEndNotMax:false (mandatory pick of min(2, available)) — a real
// fidelity divergence, not a cosmetic one (rule 2/4: predicates/selection-cardinality must mirror AS-IS
// exactly, not be diluted toward "up to"). Unlike this DP-buff factory, the sibling
// `CardEffectFactory.SelectAndDestroyEffect` (backed by `ActivatedSelectEffect`) DOES expose `canEndNotMax` as
// an explicit caller parameter (see ST1_15.cs: maxCount:2, canEndNotMax:true for an actual "up to 2" card) —
// so the DP-buff factory is missing that same parameter, which is the real gap. Grepped
// `SelectAndBuffDpEffect` / `ActivatedTargetBuffEffect` (CardPortingFramework.cs) and
// docs/porting/PRIMITIVE-CATALOG.md (SelectAndBuffDpEffect entry, and the "select up to" `Select*Effect`
// family) — no variant taking an explicit canEndNotMax exists for the DP-delta buff shape. Per rule 4, fixing
// this requires adding a canEndNotMax parameter to `SelectAndBuffDpEffect`/`ActivatedTargetBuffEffect`
// (engine-file change) — engine dev, not this pass. No cardEffects registered for this card (single-branch
// card; the only timing is fully blocked). — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_061 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // STOP: [On Play] 2 of your opponent's Digimon get -3000 DP for the turn — needs a MANDATORY
        // (canEndNotMax:false) multi-target DP-delta select+buff primitive that does not exist yet
        // (SelectAndBuffDpEffect hardcodes canEndNotMax:true for maxCount>1). See file header. — 강모델

        return cardEffects;
    }
}
