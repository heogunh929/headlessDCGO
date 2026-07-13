// 1:1 mirror of the original ST4_12 (ST4/Green).
//   [When Digivolving] Choose 1 of your opponent's Digimon. That Digimon can't attack or block until the
//   end of their next turn.  -> SelectAndRestrictEffect (WhenDigivolving, UntilOpponentTurnEnd)
// The original keys this under OnEnterFieldAnyone gated by CanUseCondition = CanTriggerWhenDigivolving +
// an inline ActivateClass/SelectPermanentEffect(Mode.Custom) whose SelectPermanentCoroutine applies
// GainCanNotAttack + GainCanNotBlock (both UntilOpponentTurnEnd) to the chosen Permanent. Declared under
// the dedicated WhenDigivolving timing (the DigivolveAction-wired timing resolved via
// ActivatedEffectResolver), matching ST1_08 / ST2_09's idiom, and reuses SelectAndRestrictEffect — the
// same primitive ST2_14 uses for the identical AS-IS GainCanNotAttack/GainCanNotBlock(Custom-select) shape.
// The AS-IS target predicate (IsPermanentExistsOnOpponentBattleAreaDigimon, no further narrowing) is
// mirrored verbatim via IsOpponentBattleAreaDigimon; maxCount:1 is clamped to the available candidate
// pool by SelectPermanentEffect.BuildRequest, matching the AS-IS Math.Min(1, MatchConditionPermanentCount).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_12 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndRestrictEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                duration: EffectDuration.UntilOpponentTurnEnd,
                cannotAttack: true,
                cannotBlock: true,
                description: "[When Digivolving] 1 of your opponent's Digimon can't attack or block until the end of their next turn."));
        }

        return cardEffects;
    }
}
