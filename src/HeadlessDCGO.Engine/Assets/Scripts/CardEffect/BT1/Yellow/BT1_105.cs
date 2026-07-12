// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_105.cs — an Option (single timing, no [Security]).
// 1:1 mirror of the AS-IS BT1_105 [Main] (OptionSkill): "Change the original DP of 1 of your opponent's Digimon
//   to 3000 until the end of your opponent's next turn." ActivateClass(CanUseCondition = CanTriggerOptionMainEffect,
//   ORDER=-1, ISOPTIONAL=false). CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon.
//   ActivateCoroutine (SetUp unconditional in AS-IS; headless gates via CanActivate = HasMatchConditionPermanent,
//   equivalent since maxCount = Min(1, count) picks nothing when count == 0): SelectPermanentEffect.SetUp(mode:
//   Custom, maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false).
//   SelectPermanentCoroutine(permanent): CardEffectCommons.ChangeBaseDigimonDP(targetPermanent: permanent,
//   changeValue: 3000, EffectDuration.UntilOpponentTurnEnd, activateClass) — SETS the original/base DP to the
//   absolute value 3000 (delta computed internally as 3000 - current BaseDP), not a fixed delta buff.
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom, opponent-scope target) with the AS-IS
//   SelectPermanentCoroutine follow-up wired via SelectBody.onEachSelected -> ChangeBaseDigimonDP on the picked id.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_105 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[Main] Change the original DP of 1 of your opponent's Digimon to 3000 until the end of your opponent's next turn.",
                    // Resolve the selected OPPONENT target's real owner from the repository — `card.Owner` (self) would
                    // build a Permanent whose OwnerId mismatches the zone owner and ChangeDigimonStat's battle-area
                    // guard would silently reject it (no modifier registered). AS-IS `new Permanent(id)` resolves owner
                    // internally. (Same latent bug class as BT1_054/BT22_003; surfaced by the WhenLinked witness.)
                    // NOTE (out of scope): the set-vs-delta faithfulness of ChangeBaseDigimonDP(changeValue:3000) vs the
                    // AS-IS "SET original DP to 3000" is a SEPARATE question, untouched here.
                    onEachSelected: id => CardEffectCommons.ChangeBaseDigimonDP(
                        new Permanent(card.Context, id,
                            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? tgtRec) && tgtRec is not null
                                ? tgtRec.OwnerId : card.Owner),
                        changeValue: 3000, EffectDuration.UntilOpponentTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Change the original DP of 1 of your opponent's Digimon to 3000 until the end of your opponent's next turn."));
        }

        return cardEffects;
    }
}
