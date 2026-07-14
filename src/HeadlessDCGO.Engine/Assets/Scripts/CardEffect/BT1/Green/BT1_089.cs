// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_089.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Main] branch (the
// [Start of Your Turn] SetMemoryTo3TamerEffect and [Security] PlaySelfTamerSecurityEffect factory branches
// were already new-model and are untouched).
//   [Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to hatch 1
//   Digi-Egg card to an empty space in your breeding area, or move 1 level 3 or higher Digimon from your
//   breeding area to your battle area.
// AS-IS: an ActivateClass on EffectTiming.OnDeclaration -- CanUseCondition = IsExistOnBattleArea(card) &&
// CanActivateSuspendCostEffect(card) && HasMatchConditionOwnersPermanent(IsDigimon && green && Level >= 5 &&
// HasLevel); CanActivateCondition = null; ORDER=-1 (uncapped), ISOPTIONAL=false. ActivateCoroutine =
// SuspendPermanentsClass(self).Tap() THEN: if (Owner.CanHatch) HatchDigiEggClass.Hatch(); ELSE IF (a breeding
// permanent with HasLevel && Level >= 3 && CanMove exists AND an empty field frame exists)
// MovePermanent(breeding[0]) -- the "or" is NOT a player choice: hatch wins whenever possible.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + local functions; the hatch-else-move branch
// mirrors the AS-IS statement order 1:1 (this exact substrate translation was already established and
// verified in the previous pass's IEffectBody, moved here unchanged). Substrate translations only:
// IEnumerator->Task; `card.Owner.CanHatch` -> an inline zone check (empty breeding area AND an available
// digi-egg -- the same check HeadlessLegalActionDispatcher's Hatch gate and the AS-IS-mirror HatchDigiEggEffect
// (ActivatedEffects.cs) use) THEN `context.ZoneMover.HatchDigitamaAsync`; `card.Owner.GetBreedingAreaPermanents()
// .Count(...) >= 1 && card.Owner.fieldCardFrames.Count(...) >= 1` -- the AS-IS empty-field-frame clause has NO
// headless analog (no frame-capacity model, matching every other move path; CanMove mirrored via
// RestrictionScan.IsRestricted(CannotMoveKey)) -> `context.ZoneMover.MoveBreedingToBattleAsync`;
// `new SuspendPermanentsClass(List<Permanent>, Hashtable).Tap()` -> the mirror ctor
// `(List<Permanent>, HeadlessEntityId? causeEffectSourceId, bool isBlock)`; `card.PermanentOfThisCard()` ->
// `ICardEffect.ResolvePermanentOfThisCard(card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_089 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Hatch 1 Digiegg or move", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to hatch 1 Digi-Egg card to an empty space in your breeding area, or move 1 level 3 or higher Digimon from your breeding area to your battle area. ";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanActivateSuspendCostEffect(card))
                    {
                        if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, permanent => permanent.IsDigimon && permanent.TopCard.HasCardColor("Green") && permanent.Level >= 5 && permanent.TopCard.HasLevel))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new SuspendPermanentsClass(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass.EffectSourceCard?.InstanceId,
                    isBlock: false).Tap();

                EngineContext context = card.Context;
                if (context.ZoneMover is not IZoneStateReader zones)
                {
                    return;
                }

                // AS-IS `if (card.Owner.CanHatch)` -- an empty breeding area AND an available digi-egg.
                if (zones.GetCards(card.Owner, ChoiceZone.BreedingArea).Count == 0
                    && zones.GetCards(card.Owner, ChoiceZone.DigitamaLibrary).Count > 0)
                {
                    await context.ZoneMover.HatchDigitamaAsync(card.Owner);
                    return;
                }

                // AS-IS `else if` a breeding permanent with HasLevel && Level >= 3 && CanMove -- move it to the
                // battle area (MovePermanent). The AS-IS empty-field-frame clause has no headless analog (no
                // frame-capacity model).
                IReadOnlyList<HeadlessEntityId> breeding = zones.GetCards(card.Owner, ChoiceZone.BreedingArea);
                if (breeding.Count > 0)
                {
                    var top = new CardSource(context, breeding[0], card.Owner, card.Owner);
                    if (top.IsDigimon && top.HasLevel && top.Level >= 3
                        && !HeadlessDCGO.Engine.Headless.Runtime.RestrictionScan.IsRestricted(context, RestrictionHelpers.CannotMoveKey, breeding[0], default))
                    {
                        await context.ZoneMover.MoveBreedingToBattleAsync(card.Owner, 1);
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
