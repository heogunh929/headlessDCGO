// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_017.cs
// 1:1 headless mirror. Original: [On Play] (OnEnterFieldAnyone, CanTriggerOnPlay) -> select 1 of your Digimon,
// give it <Security Attack +1> for the turn (ChangeDigimonSAttack, UntilEachTurnEnd). Same
// SelectPermanentEffect(Mode.Custom)+ChangeDigimonSAttack shape as BT1_025 [When Digivolving], here on a selected
// target instead of self -> SelectAndBuffSAttackEffect (owner Digimon, +1, UntilEachTurnEnd).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_017 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffSAttackEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: 1,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[On Play] 1 of your Digimon gains <Security Attack +1> (This Digimon checks 1 additional security card) for the turn."));
        }

        return cardEffects;
    }
}
