// Source: Assets/Scripts/CardEffect/BT2/Purple/BT2_080.cs
// [Retaliation] (OnDestroyedAnyone)
// [On Play] You may play up to 2 level 4 or lower purple Digimon cards from your trash without paying their
//   memory costs. Any [On Play] effects on Digimon played with this effect don't activate.
// AS-IS: RetaliationSelfEffect at OnDestroyedAnyone (condition:null).
//   ActivateClass at OnEnterFieldAnyone: CanUseCondition=CanTriggerOnPlay, CanActivateCondition=
//   IsExistOnBattleArea(card)&&HasMatchConditionOwnersCardInTrash. CanSelectCardCondition=IsDigimon&&
//   HasCardColor(Purple)&&Level<=4&&HasLevel&&CanPlayAsNewPermanent(payCost:false). maxCount=min(2,
//   matchingTrash,emptyBattleAreaFrames), root=Trash only, canEndNotMax=true, activateETB=false.
//
// Headless mirror: RetaliationSelfEffect direct port + ActivatedSelectAndPlayFromZonesEffect (Trash only),
//   maxCount=2, canEndNotMax=true. CanTarget mirrors CanSelectCardCondition via real card accessors.
// STOP: activateETB=false (AS-IS suppresses [On Play] effects on Digimon played by this effect) —
//   ActivatedSelectAndPlayFromZonesEffect has no activateETB param; ETB effects will fire (partial port).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_080 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.RetaliationSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // AS-IS CanSelectCardCondition: IsDigimon && HasCardColor(Purple) && HasLevel && Level<=4 &&
            // CanPlayAsNewPermanent(payCost:false).
            bool CanTarget(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner);
                return candidate.IsDigimon
                    && candidate.HasCardColor("Purple")
                    && candidate.HasLevel
                    && candidate.Level <= 4
                    && CardEffectCommons.CanPlayAsNewPermanent(candidate, payCost: false, cardEffect: null);
            }

            // STOP: activateETB=false (AS-IS suppresses [On Play] effects on Digimon played by this effect)
            //   — ActivatedSelectAndPlayFromZonesEffect has no activateETB param; ETB effects will fire (partial port).
            const string desc = "[On Play] You may play up to 2 level 4 or lower purple Digimon cards from your trash without paying their memory costs. Any [On Play] effects on Digimon played with this effect don't activate.";
            cardEffects.Add(new ActivatedEffect(
                card, EffectTiming.None, canUse: null, canActivate: null,
                body: new ActivatedSelectAndPlayFromZonesEffect(
                    card,
                    fromZones: new[] { ChoiceZone.Trash },
                    canTarget: CanTarget,
                    maxCount: 2,
                    canEndNotMax: true,
                    description: desc),
                maxCountPerTurn: null, isOptional: false, desc)); // (B-5) AS-IS isOptional=true is folded into the body canNoSelect (canEndNotMax:true) — result-equivalent; a true 2-decision restore needs the optional-prompt protocol (deferred).
        }

        return cardEffects;
    }
}