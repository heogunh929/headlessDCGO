namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST6;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST6_12 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // [When Digivolving] Up to 2 of your Digimon gain <Retaliation> until the end of your opponent's next turn.
            // The original uses OnEnterFieldAnyone with a CanTriggerWhenDigivolving guard — port as WhenDigivolving timing.
            // The effect is a triggered activated effect (gain Retaliation to up to 2 own Digimon).
            // Since headless bridge covers [When Digivolving] (WhenDigivolving) for activated effects, return SelectAndBuffSAttackEffect
            // is not correct (Retaliation is a keyword, not DP/S-Attack). The headless has no factory for "gain keyword to scoped set".
            // STOP: no factory for "grant Retaliation keyword to up to 2 own Digimon until end of opponent's next turn".
            // The primitive exists for player-scope keyword grants (RetaliationStaticEffect), but not for selecting specific permanents
            // and granting a keyword with a custom duration (UntilOpponentTurnEnd). The original uses a custom SelectPermanentEffect
            // with GainRetaliation(targetPermanent, effectDuration, activateClass) — no headless equivalent.
            // STOP: no factory for "select up to 2 own Digimon and grant Retaliation until end of opponent's next turn".
        }
        return effects;
    }
}