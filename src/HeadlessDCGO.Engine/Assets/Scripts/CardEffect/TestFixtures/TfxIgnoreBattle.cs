// TEST FIXTURE (not a real card). Returns the intrinsic DontBattleSecurityDigimon marker (AS-IS EX4_013
// "Ignore Battle") at EffectTiming.None, so when it is revealed as a security Digimon the attacker does NOT
// battle it. Used by tests/FAILd-05. Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxIgnoreBattle : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.DontBattleSecurityDigimonStaticEffect(
                cardSourceCondition: cs => cs is not null && cs.InstanceId == card.InstanceId, card, condition: null));
        }
        return effects;
    }
}
