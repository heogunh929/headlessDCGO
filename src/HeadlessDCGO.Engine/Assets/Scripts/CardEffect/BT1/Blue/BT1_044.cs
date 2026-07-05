namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_044 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(new ActivatedPlayFromUnderEffect(card,
                "[When Attacking] Play 1 level 4 or lower digivolution card under this card as another Digimon without paying its memory cost."));
        }

        return cardEffects;
    }
}