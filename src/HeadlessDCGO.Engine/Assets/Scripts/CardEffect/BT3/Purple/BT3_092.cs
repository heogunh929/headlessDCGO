// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_092.cs
// 1:1 mirror of the original BT3_092 (BT3/Purple).
//   [When Attacking] <Piercing> (self, main, unconditional).
//   -> OnDetermineDoSecurityCheck: CardEffectFactory.PierceSelfEffect(isInheritedEffect:false, card,
//      condition:null) — same shape as ST7_10/BT1_022 (a query-time keyword, not a trigger emission).
//
//   [All Turns] When another Digimon is deleted, gain 1 memory for each Digimon deleted.
//   AS-IS: PermanentCondition(permanent) = IsPermanentExistsOnBattleArea(permanent) && permanent.IsDigimon
//   && permanent != card.PermanentOfThisCard(); CanUseCondition = IsExistOnBattleArea(card) &&
//   CanTriggerOnPermanentDeleted(hashtable, PermanentCondition); CanActivateCondition sums
//   hashtables.Count (AS-IS batches multiple simultaneous deletions into one hashtable list). The headless
//   deletion trigger (OnDestroyedAnyone / TriggerEntityId) is single-subject per firing (mirrors ST3_04),
//   so "1 memory PER Digimon deleted" is expressed as a fixed amount:1 AddMemoryTriggerEffect that fires
//   once per single-deletion event — mathematically equivalent to AS-IS's hashtables.Count summation under
//   this per-event-per-deletion firing model. The "existsOnBattleArea" check on the deleted permanent is
//   dropped (zone-agnostic per the ST3_04 / IsOpponentOwnedDigimon precedent — the deleted card has
//   already left the battle area by the time the trigger observes it in this engine). No [Your Turn] /
//   [Once Per Turn] restriction (AS-IS text is "[All Turns]", unconditional owner + no cap).
//   -> CardEffectFactory.AddMemoryTriggerEffect(OnDestroyedAnyone, amount:1, isInheritedEffect:false,
//      condition:null, triggerGate: any-other-Digimon-deleted, maxCountPerTurn:null, isOptional:false).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_092 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool IsAnotherDigimon(HeadlessEntityId id)
            {
                if (id.IsEmpty || id == card.PermanentOfThisCard().TopInstanceId)
                {
                    return false;
                }

                return new CardSource(card.Context, id, card.Owner, card.Owner).IsDigimon;
            }

            bool TriggerGate(CardEffectResolveContext ctx) =>
                CardEffectCommons.CanTriggerOnPermanentDeleted(card, ctx, IsAnotherDigimon);

            bool Condition() => CardEffectCommons.IsExistOnBattleArea(card);

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 1,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                description: "[All Turns] When another Digimon is deleted, gain 1 memory for each Digimon deleted.",
                triggerGate: TriggerGate,
                maxCountPerTurn: null,
                hash: null,
                isOptional: false));
        }

        return cardEffects;
    }
}
