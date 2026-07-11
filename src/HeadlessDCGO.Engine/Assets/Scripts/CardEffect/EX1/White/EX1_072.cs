// (E-3 witness) EX1_072 (EX1/White Option). AS-IS EX1/White/EX1_072.cs.
//   [Main]     "Your opponent can't use Option cards until the end of their next turn."
//   [Security] "Your opponent can't use Option cards this turn. Then, add this card to its owner's hand."
// Both register a duration-bound CanNotPlayClass at PLAYER scope via CardEffectCommons.AddEffectToPlayer:
// [Main] -> EffectDuration.UntilOpponentTurnEnd, [Security] -> EffectDuration.UntilEachTurnEnd. The headless
// mirror is AddCanNotPlayOptionToPlayer (region ① player-bucket ContinuousCanNotPlayOptionEffect), scanned by
// CanNotPlayOptionScan; the duration bucket expires via EffectDurationExpiry at the matching turn end. AS-IS
// CardCondition(source) = source.Owner == card.Owner.Enemy && source.IsOption; CanUse = always true.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX1.White;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class EX1_072 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // AS-IS CardCondition (shared by [Main] and [Security]): an OPTION owned by the caster's opponent.
        bool CardCondition(CardSource cardSource) =>
            cardSource is not null
            && cardSource.Owner == CardEffectCommons.OpponentOf(card)
            && cardSource.IsOption;

        if (timing == EffectTiming.OptionSkill)
        {
            // AS-IS EX1_072.cs:14-56 — [Main] ActivateCoroutine: AddEffectToPlayer(UntilOpponentTurnEnd, ...,
            // CanNotPlayClass). No-select player-scope grant, mirrored by GrantPlayerScopeRestrictionBody.
            const string desc = "[Main] Your opponent can't use Option cards until the end of their next turn.";
            cardEffects.Add(new ActivatedEffect(
                card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: null,
                body: new GrantPlayerScopeRestrictionBody(
                    grant: c => CardEffectCommons.AddCanNotPlayOptionToPlayer(EffectDuration.UntilOpponentTurnEnd, c, CardCondition)),
                maxCountPerTurn: null, isOptional: false, desc));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            // AS-IS EX1_072.cs:58-104 — [Security] ActivateCoroutine: AddEffectToPlayer(UntilEachTurnEnd, ...,
            // CanNotPlayClass) THEN AddThisCardToHand(card). Ported as sibling activated effects resolved in list
            // order (grant first, then add-to-hand), the same composite shape as BT9_109's [Security].
            const string desc = "[Security] Your opponent can't use Option cards this turn. Then, add this card to its owner's hand.";
            cardEffects.Add(new ActivatedEffect(
                card,
                timing: EffectTiming.SecuritySkill,
                canUse: ctx => CardEffectCommons.CanTriggerSecurityEffect(ctx, card),
                canActivate: null,
                body: new GrantPlayerScopeRestrictionBody(
                    grant: c => CardEffectCommons.AddCanNotPlayOptionToPlayer(EffectDuration.UntilEachTurnEnd, c, CardCondition)),
                maxCountPerTurn: null, isOptional: false, desc));
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
