// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_075.cs
// 1:1 mirror of the original BT3_075 (two [None] branches, neither inherited).
//   [All Turns] <Blocker>.  -> BlockerSelfStaticEffect (verbatim factory match).
//   [All Turns] Your Digimon with Blocker can't be deleted by your opponent's effects.  -> a continuous
//     player-scope restriction: scope = card.Owner's "Digimon" cards (AS-IS PermanentCondition =
//     IsPermanentExistsOnOwnerBattleAreaDigimon + HasBlocker; the owner+Digimon-type half is already the
//     scopePlayerId/scopeCardType, so the extra scopePredicate only needs the HasBlocker check),
//     restriction key = CannotBeDeletedBySkillKey (the same key CanNotBeDestroyedBySkillStaticEffect uses
//     for a SELF-only grant — CardPortingFramework.cs — but this card needs it scoped to a live SET of
//     permanents, which that self-only factory cannot express), gated by a causingEffectPredicate (AS-IS
//     CardEffectCondition = the deleting effect's source is the OPPONENT's — CardEffectCommons.
//     IsOpponentEffect is the exact existing mirror) so only the OPPONENT's delete-by-skill is blocked
//     (mirrors AS-IS exactly; own effects still work). ContinuousPlayerScopeRestrictionEffect is the
//     existing primitive that carries both a scopePredicate and a causingEffectPredicate (consumed by
//     MatchStateMutationSink.IsRestrictedFromCause) — used directly (public class, public constructor) the
//     same way ST3_13/14 directly construct AddThisCardToHandEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class BT3_075 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return ContinuousKeywordGate.HasKeyword(card.Context, permanent.InstanceId, ContinuousKeywordGate.Blocker);
            }

            bool CanUseCondition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            cardEffects.Add(new ContinuousPlayerScopeRestrictionEffect(
                card,
                scopePlayerId: card.Owner,
                restrictionKey: RestrictionHelpers.CannotBeDeletedBySkillKey,
                scopeCardType: "Digimon",
                isInheritedEffect: false,
                condition: CanUseCondition,
                scopePredicate: CardEffectFactory.ScopePred(PermanentCondition),
                causingEffectPredicate: causingEffectSource => CardEffectCommons.IsOpponentEffect(causingEffectSource, card)));
        }

        return cardEffects;
    }
}
