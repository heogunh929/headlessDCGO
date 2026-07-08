// Source: Assets/Scripts/CardEffect/BT2/BT2_045.cs
// Decision: STOP
// STOP: BeforePayCost — <Digisorption -2> is an interactive optional activation: the player chooses to suspend 1
//       own battle-area Digimon as a cost, after which the digivolution cost is reduced by 2. No headless factory
//       covers this pattern. BeforePayCostReductionEffect is a static/conditional delta only (no player-selection
//       or suspension step). SelectAndSuspendEffect is a resolved activated effect, not a cost-gate mechanism.
//       The ActivateClass coroutine flow (present choice → player selects Digimon → suspend → apply cost delta)
//       has no compose path in available primitives.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_045 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: BeforePayCost — Digisorption -2: interactive optional suspend-1-own-Digimon-as-cost → digivolution
        //       cost −2. BeforePayCostReductionEffect covers static conditional reduction only; no factory exists
        //       that gates cost reduction on a player-driven suspension action. Cannot faithfully port.

        return cardEffects;
    }
}