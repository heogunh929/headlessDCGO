// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_112.cs (a Green Option, two timings)
// AS-IS:
//   [Main] (OptionSkill) "1 of your Digimon gains the following effect for the turn: When this Digimon
//   deletes an opponent's Digimon in battle and survives, unsuspend it."
//     ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//     ActivateCoroutine: maxCount = Math.Min(1, MatchConditionPermanentCount(CanSelectPermanentCondition))
//     where CanSelectPermanentCondition(permanent) = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
//     SelectPermanentEffect.SetUp(selectPlayer: owner, maxCount, canNoSelect:false, canEndNotMax:false,
//     mode: Custom); then (SelectPermanentCoroutine, once the target is picked) build a NESTED ActivateClass
//     ("Unsuspend this Digimon", CanUseCondition1 = IsPermanentExistsOnBattleArea(selected) &&
//     CanTriggerWhenDeleteOpponentDigimonByBattle(hashtable1, winnerCondition = permanent contains selected.TopCard,
//     loserCondition = IsOpponentPermanent(permanent, card), isOnlyWinnerSurvive:true), CanActivateCondition1 =
//     IsPermanentExistsOnBattleArea(selected) && CanUnsuspend(selected), ActivateCoroutine1 =
//     IUnsuspendPermanents([selected]).Unsuspend(), ORDER=-1, ISOPTIONAL=false) and register it on the SELECTED
//     permanent via CardEffectCommons.AddEffectToPermanent(targetPermanent: selected,
//     effectDuration: UntilEachTurnEnd, card, cardEffect: nested, timing: OnEndBattle) + a buff VFX.
//   [Security] (SecuritySkill) "Add this card to its owner's hand."
//     ActivateClass(CanUseCondition = CanTriggerSecurityEffect, SetIsSecurityEffect(true), ORDER=-1,
//     ISOPTIONAL=false). ActivateCoroutine: CardEffectCommons.AddThisCardToHand(card, activateClass).
//
// STOP: [Main] (OptionSkill) "1 of your Digimon gains 'When this Digimon deletes an opponent's Digimon in battle
// and survives, unsuspend it.' for the turn." — this needs an activated BODY of "interactively select 1 own
// battle-area Digimon (Mode.Custom-shaped: mandatory pick of min(1, available), canNoSelect:false,
// canEndNotMax:false), THEN — using the SPECIFIC selected id — grant THAT permanent an arbitrary caller-supplied
// nested activated ICardEffect for the turn via CardEffectCommons.AddEffectToPermanent". Every underlying piece
// exists individually:
//   - The registration primitive exists: CardEffectCommons.AddEffectToPermanent(Permanent, EffectDuration,
//     CardSource, ICardEffect, EffectTiming) (CardPortingFramework.cs:8434) registers any ICardEffect on a target
//     permanent with a duration, and Permanent is trivially constructible from the selected id
//     (new Permanent(card.Context, selectedId, card.Owner), CardPortingFramework.cs:1716).
//   - The exact nested payload exists: CardEffectFactory.UnsuspendSelfTriggerEffect(OnEndBattle, targetCard, "...",
//     triggerGate: ctx => CanTriggerWhenDeleteOpponentDigimonByBattle(ctx, targetCard, winner, loser,
//     isOnlyWinnerSurvive:true)) (CardPortingFramework.cs:5750) — already live-wired with this exact
//     delete-survive trigger shape on BT1_077 / ST4_11 (its CanActivate CanUnsuspend gate is the AS-IS
//     CanActivateCondition1's CanUnsuspend(selected), CardPortingFramework.cs:9650).
// But there is NO IEffectBody in the uniform catalog (Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs:
// DrawBody / MemoryBody / RecoveryBody / TrashSecurityBody / SelectTrashHandThenSelfMutationBody /
// SuspendSelfAndGainMemoryBody / SelfToHandBody / GrantContinuousBody / SelectBody) that composes "interactive
// select -> per-selected-id grant of a caller-supplied nested effect". SelectBody delegates entirely to
// SelectPermanentEffect.Apply, and Mode.Custom is EXPLICITLY a no-op there
// (SelectPermanentEffect.cs:213: "Mode.Attack or Mode.Custom => null"), with no follow-up hook to run
// AddEffectToPermanent against the chosen id. GrantContinuousBody grants a continuous effect on THIS card only
// (no interactive target select). Also grepped the legacy per-shape factories (CardPortingFramework.cs): the
// closest analog, ActivatedTargetBuffEffect (:2395) / SelectAndBuffDpEffect (:5719), does "select N Mode.Custom
// permanents then register a duration binding per target", but it registers a FIXED continuous DP/attack delta —
// it is hard-wired to a numeric buff, not a caller-supplied arbitrary nested ActivatedEffect (the "grant a
// TRIGGERED unsuspend ability" this card needs). No existing body/factory generalizes it to "select then grant an
// arbitrary nested ICardEffect via AddEffectToPermanent". Grepped the whole CardEffect tree: no ported card calls
// AddEffectToPermanent yet, and every card that hit a missing composed body (BT1_086/090/084/…) left the timing
// UNREGISTERED with a STOP rather than defining a card-local body (no card-local IEffectBody precedent exists; the
// convention is that new composed primitives live in the shared engine files, built in the strong-model primitive
// pass). Per rule 4 this is a genuine primitive gap requiring a new composed primitive (e.g. a
// "SelectThenGrantEffectBody" generalizing ActivatedTargetBuffEffect from a fixed delta to an arbitrary nested
// ICardEffect), out of scope for a single-card porting pass — no engine change / no new primitive / no throw.
// Left UNREGISTERED for OptionSkill (no ICardEffect added). — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_112 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // STOP: [Main] "1 of your Digimon gains 'When this Digimon deletes an opponent's Digimon in battle and
        // survives, unsuspend it.' for the turn" — needs a "select 1 own Digimon -> grant it an arbitrary nested
        // activated effect for the turn (AddEffectToPermanent)" body that does not exist yet (see file header).
        // if (timing == EffectTiming.OptionSkill) { ... }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
