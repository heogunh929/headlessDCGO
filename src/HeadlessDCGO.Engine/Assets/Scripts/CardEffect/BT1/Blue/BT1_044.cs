namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_044 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            const string desc = "[When Attacking] Play 1 level 4 or lower digivolution card under this card as another Digimon without paying its memory cost.";
            cardEffects.Add(new ActivatedEffect(
                card, EffectTiming.None, canUse: null, canActivate: null,
                body: new ActivatedPlayFromUnderEffect(card, desc),
                maxCountPerTurn: null, isOptional: false, desc));
        }

        return cardEffects;
    }
}