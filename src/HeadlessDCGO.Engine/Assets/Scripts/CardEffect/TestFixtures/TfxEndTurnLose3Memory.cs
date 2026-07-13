// TEST FIXTURE (not a real card). A STATIC "[End of Your Turn] Lose 3 memory." holder — the shape the RD-6
// end-turn-sequence tests need (a scheduler-bound OnEndTurn memory reactor registered at enter-play).
// BT1_021 previously served this role, but the REAL BT1_021 registers its end-of-turn loss only when its
// [When Attacking] effect activates (AS-IS BT1_021.cs:36-46 UntilEachTurnEndEffects) — statically declaring it
// was a divergence (C-5 adversarial review drive-by P2-9), so the static shape lives here instead. Inert in
// actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxEndTurnLose3Memory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndTurn)
        {
            effects.Add(CardEffectFactory.EoTLose3Memory(card));
        }

        return effects;
    }
}
