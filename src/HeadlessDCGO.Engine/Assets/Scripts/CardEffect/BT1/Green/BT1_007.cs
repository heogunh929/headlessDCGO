namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// STOP: AS-IS CanActivateCondition gates on `card.Owner.DigivolveCount_ThisTurn >= 1`
// (DCGO/Assets/Scripts/Script/Player.cs:1176, reset in TurnStateMachine.cs:3181, incremented in
// CardController.cs:1528) — a per-player "digivolved N times this turn" counter. Grepped the headless
// engine twice (`DigivolveCount_ThisTurn`/`DigivolvedThisTurn`/`HasDigivolved`, and PRIMITIVE-CATALOG.md)
// with zero hits; DigivolveAction.cs/PlayerRuleAdapter.cs track no such per-turn counter. This predicate
// gates the entire effect (not a secondary clause), so registering just the OnAllyAttack trigger without
// it would apply +1000 DP on every attack regardless of digivolve state — not a faithful port. Per porting
// rule 4, no new primitive/counter added here; effect left unregistered pending engine-side
// DigivolveCount_ThisTurn (or equivalent) support.
public sealed class BT1_007 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        return cardEffects;
    }
}
