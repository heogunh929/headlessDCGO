// Source: Assets/Scripts/CardEffect/BT2/BT2_053.cs
// [Your Turn][Inherited] When you play another Digimon with the same name as this Digimon → Draw 1.
// STOP: CanTriggerOnPermanentPlay(same-name owner Digimon ∧ not self) + IsOwnerTurn → DrawCardsEffect has no headless
//       equivalent — DrawCardsEffect(card, 1) at OnEnterFieldAnyone only covers the implicit CanTriggerOnPlay (self-play)
//       path; no factory accepts a permanentCondition trigger guard to fire on a different permanent's play event.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_053 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // STOP: CanTriggerOnPermanentPlay(IsOwnerBattleAreaDigimon ∧ not self ∧ same card name) + IsOwnerTurn
            //       required, but DrawCardsEffect(card, int) carries no permanentCondition parameter and no other
            //       factory models "watch-other-permanent-enter → draw". Faithful trigger cannot be registered.
        }
        return cardEffects;
    }
}