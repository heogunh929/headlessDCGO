// 1:1 mirror of the original BT1_089 (BT1/Green) — a Tamer.
//   [Start of Your Turn] If you have 2 or less memory, set your memory to 3.
//     -> CardEffectFactory.SetMemoryTo3TamerEffect(card) (OnStartTurn).
//   [Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to hatch 1
//   Digi-Egg card to an empty space in your breeding area, or move 1 level 3 or higher Digimon from your
//   breeding area to your battle area.
//     AS-IS: an ActivateClass on EffectTiming.OnDeclaration — CanUseCondition = IsExistOnBattleArea(card)
//     && CanActivateSuspendCostEffect(card) && HasMatchConditionOwnersPermanent(IsDigimon && green &&
//     Level >= 5 && HasLevel); CanActivateCondition = null; ORDER=-1 (uncapped), ISOPTIONAL=false.
//     ActivateCoroutine = SuspendPermanentsClass(self).Tap() THEN: if (Owner.CanHatch) HatchDigiEggClass.Hatch();
//     ELSE IF (a breeding permanent with HasLevel && Level >= 3 && CanMove exists AND an empty field frame
//     exists) MovePermanent(breeding[0]) — the "or" is NOT a player choice: hatch wins whenever possible.
//     Headless: a uniform ActivatedEffect (declared via the B-2 ActivateMain action); the body suspends self
//     then mirrors the same hatch-else-move branch (the empty-field-frame clause is substrate-absent — the
//     headless battle area has no frame-capacity model, matching every other move path).
//     Formerly STOP for the missing main-phase declaration ACTION; that subsystem exists since B-2 (P1-5).
//   [Security] Play this Tamer. -> PlaySelfTamerSecurityEffect (security-skill flow, mirrors ST1_12/ST2_12/ST3_12/ST4_14)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_089 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnDeclaration)
        {
            // AS-IS CanUseCondition (BT1_089.cs — identical to BT1_088's): on the battle area, the suspend
            // cost is payable, and a level 5+ green Digimon (with a printed level) is among the owner's permanents.
            bool CanUseCondition(CardEffectResolveContext _) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanActivateSuspendCostEffect(card)
                && CardEffectCommons.HasMatchConditionOwnersPermanent(card, permanent =>
                    permanent.IsDigimon && permanent.TopCard.HasCardColor("Green")
                    && permanent.Level >= 5 && permanent.TopCard.HasLevel);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDeclaration,
                canUse: CanUseCondition,
                canActivate: null,
                body: new SuspendSelfThenHatchOrMoveBody(),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to hatch 1 Digi-Egg card to an empty space in your breeding area, or move 1 level 3 or higher Digimon from your breeding area to your battle area."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }

    /// <summary>AS-IS ActivateCoroutine: suspend self (cost), then <c>if (Owner.CanHatch)</c> hatch;
    /// <c>else if</c> the breeding Digimon has a printed level >= 3 (and can move) move it to the battle
    /// area — hatch takes precedence, no player choice. Async-only (direct zone moves).</summary>
    private sealed class SuspendSelfThenHatchOrMoveBody : IEffectBody
    {
        public bool IsInteractive => false;

        public ChoiceRequest? BuildRequest(CardSource card, IReadOnlyList<HeadlessPlayerId> players) => null;

        public void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected) =>
            throw new NotSupportedException("BT1_089 [Main] body must be applied via ApplyAsync (awaited hatch/move).");

        async ValueTask IEffectBody.ApplyAsync(
            CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected, CancellationToken cancellationToken)
        {
            // Cost: AS-IS SuspendPermanentsClass(card.PermanentOfThisCard()).Tap().
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.SuspendKind, card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));

            EngineContext context = card.Context;
            if (context.ZoneMover is not IZoneStateReader zones)
            {
                return;
            }

            // AS-IS branch 1: `if (card.Owner.CanHatch)` — empty breeding area AND an available digi-egg.
            if (zones.GetCards(card.Owner, ChoiceZone.BreedingArea).Count == 0
                && zones.GetCards(card.Owner, ChoiceZone.DigitamaLibrary).Count > 0)
            {
                await context.ZoneMover.HatchDigitamaAsync(card.Owner, cancellationToken).ConfigureAwait(false);
                return;
            }

            // AS-IS branch 2: `else if` a breeding permanent with HasLevel && Level >= 3 && CanMove — move it
            // to the battle area (MovePermanent). The AS-IS empty-field-frame clause has no headless analog
            // (no frame-capacity model). CanMove = not a DP<=0 egg (implied by Level >= 3) AND no
            // ICanNotMoveEffect restriction (Permanent.cs:2010-2040) — mirrored by the CannotMove restriction
            // scan the general move action also consults.
            IReadOnlyList<HeadlessEntityId> breeding = zones.GetCards(card.Owner, ChoiceZone.BreedingArea);
            if (breeding.Count > 0)
            {
                var top = new CardSource(context, breeding[0], card.Owner, card.Owner);
                if (top.IsDigimon && top.HasLevel && top.Level >= 3
                    && !Headless.Runtime.RestrictionScan.IsRestricted(context, RestrictionHelpers.CannotMoveKey, breeding[0], default))
                {
                    await context.ZoneMover.MoveBreedingToBattleAsync(card.Owner, 1, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
