// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_086.cs (a Blue Tamer, mixed timings)
// AS-IS has three branches:
//   [Start of Your Turn] If you have 2 or less memory, set your memory to 3.
//     -> CardEffectFactory.SetMemoryTo3TamerEffect(card) (OnStartTurn). 1:1, verbatim factory match.
//   [Your Turn] When you play a blue Digimon, you can suspend this Tamer to trash the bottom
//   digivolution card of 1 of your opponent's Digimon.
//     -> ActivateClass on OnEnterFieldAnyone. CanUseCondition = IsExistOnBattleArea(card) &&
//        IsOwnerTurn(card) && CanTriggerOnPermanentPlay(hashtable, PermanentCondition), where
//        PermanentCondition(permanent) = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
//        permanent.TopCard.CardColors.Contains(Blue). CanActivateCondition = IsExistOnBattleArea(card) &&
//        CanActivateSuspendCostEffect(card). ORDER=-1 (no once-per-turn cap), ISOPTIONAL=true ("you can").
//        ActivateCoroutine: (1) cost — SuspendPermanentsClass(this Tamer's permanent).Tap(); (2) select —
//        maxCount = Math.Min(1, MatchConditionPermanentCount(CanSelectPermanentCondition)) where
//        CanSelectPermanentCondition(permanent) = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
//        && permanent.DigivolutionCards.Count(c => !c.CanNotTrashFromDigivolutionCards(activateClass)) >= 1;
//        SelectPermanentEffect.SetUp(selectPlayer: owner, maxCount, canNoSelect:false, canEndNotMax:false,
//        mode: Custom) — a mandatory pick of min(1, available) (note CanActivateCondition does NOT itself
//        require a legal target to exist, so the suspend cost can be paid even with 0 legal opponent
//        Digimon, in which case maxCount=0 and the select degenerates); (3) effect — for the selected
//        permanent, TrashDigivolutionCardsFromTopOrBottom(trashCount:1, isFromTop:false).
//
// STOP: the [Your Turn] branch (OnEnterFieldAnyone) needs a composed activated-effect BODY of
// "pay a suspend-self cost, THEN interactively select 1 opponent Digimon (mandatory pick of
// min(1, available), Mode.Custom-shaped), THEN trash N of the selected Digimon's digivolution cards
// from the bottom" and no such primitive exists. Grepped (2x) the uniform IEffectBody catalog
// (Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs): DrawBody, MemoryBody, RecoveryBody,
// TrashSecurityBody, SelectTrashHandThenSelfMutationBody (select+trash is from the OWN hand, then a
// fixed self-mutation — not a suspend-self cost, and its follow-up is a single fixed mutation on THIS
// card, not "trash digivolution cards of an externally-selected opponent permanent"),
// SuspendSelfAndGainMemoryBody (suspend-self cost, but its follow-up is a fixed non-interactive
// AddMemory mutation, no target selection at all), SelfToHandBody, GrantContinuousBody, and SelectBody
// (interactive select, but Mode.Custom is EXPLICITLY a no-op in SelectPermanentEffect.BuildMutation —
// "Mode.Attack or Mode.Custom => null" — Assets/Scripts/Script/SelectPermanentEffect.cs:213 — so it
// cannot emit the TrashDigivolutionCardsKind mutation, and it has no self-suspend cost step either).
// Also grepped the legacy per-shape IActivatedCardEffect factories (CardPortingFramework.cs):
// CardEffectFactory.SelectAndTrashDigivolutionEffect / ActivatedSelectTrashDigivolutionEffect (used by
// ST2_03/ST2_06/ST2_09) select+trash exactly the AS-IS TrashDigivolutionCardsFromTopOrBottom shape, but
// that factory has NO self-suspend cost step and no CanUse/CanActivate/isOptional gate hooks at all (it
// is auto-resolved whenever the timing fires and a legal target exists, i.e. mandatory-shaped, not the
// "you may pay a cost to activate" shape this card needs). No factory composes a self-suspend cost with
// an externally-targeted select+trash-digivolution effect. Per rule 4 this is a genuine primitive gap
// (not a per-card shortcut to invent): fixing it means adding a new composed IEffectBody to the shared
// ActivatedEffect.cs catalog, which is engine-file work out of scope for a single-card porting pass.
// No cardEffects registered for OnEnterFieldAnyone (this branch only; OnStartTurn and SecuritySkill are
// unaffected and fully ported below). — 강모델
//
//   [Security] Play this Tamer. -> CardEffectFactory.PlaySelfTamerSecurityEffect(card) (SecuritySkill).
//     1:1, verbatim factory match (mirrors ST4_14 / ST1_12 / ST2_12 / ST3_12 pattern).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_086 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        // [Your Turn] When you play a blue Digimon, you can suspend this Tamer to trash the bottom digivolution
        // card of 1 of your opponent's Digimon. AS-IS: ActivateClass on OnEnterFieldAnyone (broadcast when-play),
        // CanUseCondition = IsExistOnBattleArea && IsOwnerTurn && CanTriggerOnPermanentPlay(PermanentCondition =
        // owner's own battle-area blue Digimon), CanActivateCondition = IsExistOnBattleArea &&
        // CanActivateSuspendCostEffect, ORDER=-1, ISOPTIONAL=true. ActivateCoroutine: SuspendPermanentsClass(self)
        // .Tap() as cost, THEN SelectPermanentEffect(Mode.Custom, maxCount=Min(1,count), opponent Digimon with a
        // trashable digivolution card) -> TrashDigivolutionCardsFromTopOrBottom(trashCount:1, isFromTop:false).
        // Headless mirror: uniform ActivatedEffect + SuspendSelfCostThenBody(SelectBody) — the suspend-self cost
        // wrapper runs first, then the interactive select trashes the bottom digivolution card of the pick via the
        // sink-scoped follow-up.
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.TopCard.HasCardColor("Blue");

            bool CanSelect(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.TrashableDigivolutionCount(card, id) >= 1;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: ctx => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerOnPermanentPlay(ctx, card, PermanentCondition),
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.CanActivateSuspendCostEffect(card),
                body: new SuspendSelfCostThenBody(new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[Your Turn] You can suspend this Tamer to trash the bottom digivolution card of 1 of your opponent's Digimon.",
                    onEachSelectedWithSink: (c, sink, id) => CardEffectCommons.TrashDigivolutionCards(sink, c, id, count: 1, fromBottom: true))),
                maxCountPerTurn: null,
                isOptional: true,
                description: "[Your Turn] When you play a blue Digimon, you can suspend this Tamer to trash the bottom digivolution card of 1 of your opponent's Digimon."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
