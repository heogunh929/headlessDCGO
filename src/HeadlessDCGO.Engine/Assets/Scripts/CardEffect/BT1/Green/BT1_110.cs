// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_110.cs — an Option.
//   [Main]     Suspend 1 of your opponent's Digimon.
//   [Security] Suspend all of your opponent's Digimon without <Blocker>.
// AS-IS [Main] (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1,
// ISOPTIONAL=false). ActivateCoroutine: guarded by HasMatchConditionPermanent(CanSelectPermanentCondition)
// (CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon), maxCount =
// Math.Min(1, MatchConditionPermanentCount(...)), then a SelectPermanentEffect(Mode.Tap) (canNoSelect:false,
// canEndNotMax:false) over opponent battle-area Digimon.
// Headless mirror: CardEffectFactory.SelectAndSuspendEffect (AS-IS SelectPermanentEffect Mode.Tap), maxCount:1
// — same shape as ST4_15's [Main] ("Suspend 1 of your opponent's Digimon."). CanTriggerOptionMainEffect is
// subsumed by the OptionSkill activation gate (ST1_13/ST1_15/BT1_090/BT1_092/BT1_094/ST4_15 precedent);
// HasMatchConditionPermanent + Min(1,count) are subsumed by SelectAndSuspendEffect's own "select up to
// maxCount matching permanents" behaviour (no-op / BuildRequest clamp when nothing matches, same precedent).
//
// AS-IS [Security] (SecuritySkill): ActivateClass(CanUseCondition = CanTriggerSecurityEffect, ORDER=-1,
// ISOPTIONAL=false, IsSecurityEffect=true). ActivateCoroutine has NO SelectPermanentEffect step at all: it
// computes `card.Owner.Enemy.GetBattleAreaDigimons().Filter(PermanentCondition)` — every opponent
// battle-area Digimon that (a) IsPermanentExistsOnOpponentBattleAreaDigimon, (b) `!permanent.HasBlocker`, and
// (c) `!permanent.TopCard.CanNotBeAffected(activateClass)` — then directly runs
// `new SuspendPermanentsClass(suspendTargetPermanents, ...).Tap()` on that whole precomputed list, with zero
// player choice (unlike [Main], which does select).
// Headless mirror ([Security]): the no-select "apply a mutation to EVERY matching permanent" gap (shared with
// BT1_101 trash-all) is now covered by ApplyToAllMatchingBody (ActivatedEffect.cs) — a non-interactive uniform
// body that, at resolve time, enumerates CardEffectCommons.MatchConditionPermanentIds(match) and runs a
// per-target sink action (here CardEffectCommons.SuspendPermanent). The AS-IS !CanNotBeAffected guard is folded
// into the sink's centralised immunity gate (applied to every mutation), so the match predicate carries only
// "opponent battle-area Digimon && !HasKeyword(Blocker)". See the [Security] block below.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_110 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndSuspendEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[Main] Suspend 1 of your opponent's Digimon."));
        }

        // [Security] "Suspend all of your opponent's Digimon without <Blocker>." (SecuritySkill)
        // AS-IS: ActivateClass(CanUseCondition = CanTriggerSecurityEffect, ORDER=-1, ISOPTIONAL=false,
        // IsSecurityEffect=true). ActivateCoroutine has NO SelectPermanentEffect — it computes
        // card.Owner.Enemy.GetBattleAreaDigimons().Filter(opponent battle-area Digimon && !HasBlocker &&
        // !CanNotBeAffected) and directly SuspendPermanentsClass(...).Tap() on that whole precomputed list.
        // Headless mirror: uniform ActivatedEffect whose body is ApplyToAllMatchingBody — per opponent battle-area
        // Digimon without <Blocker> it stages SuspendPermanent. The AS-IS !CanNotBeAffected guard is handled by the
        // sink's centralised immunity gate (applied to EVERY mutation, source = this card), so the match predicate
        // carries only "opponent battle-area Digimon && !HasKeyword(Blocker)".
        if (timing == EffectTiming.SecuritySkill)
        {
            bool Match(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && !ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.SecuritySkill,
                canUse: ctx => CardEffectCommons.CanTriggerSecurityEffect(ctx, card),
                canActivate: null,
                body: new ApplyToAllMatchingBody(
                    match: Match,
                    perTarget: (c, sink, id) => CardEffectCommons.SuspendPermanent(sink, c, id)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Security] Suspend all of your opponent's Digimon without <Blocker>."));
        }

        return cardEffects;
    }
}
