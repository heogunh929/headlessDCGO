// TEST FIXTURE (not a real card). A SELF-scope [Your Turn] OnMove reactor: when THIS Digimon itself is promoted
// from the breeding area to the battle area, draw 1 card. This is the SELF witness for the F1 Tier1 OnMove bridge
// — the majority shape (18 of 30 real OnMove cards gate `permanent == card.PermanentOfThisCard()`, e.g.
// EX11_007/ST22_03/ST24_04), which all real self cards wrap around primitive-heavy bodies (reveal-3 / select-grant /
// destroy). This fixture isolates the self gate + a single DrawBody so the test observes exactly the subject-as-
// reactor path of the EventBroadcast bridge (the field scan visits the promoted subject itself, whose own gate
// `permanent == self` passes) without a compound body. Inert in actual play.
//
// Mirrors the AS-IS self shape verbatim (EX11_007.cs:104-113 / ST22_03.cs:63-73):
//   CanUse = IsExistOnBattleAreaDigimon(card) && CanTriggerOnMove(permanent => permanent == card.PermanentOfThisCard()).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnMoveSelfDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnMove)
        {
            // AS-IS self gate: the moved permanent IS this card's own permanent (permanent == PermanentOfThisCard()).
            bool PermanentCondition(Permanent permanent) => permanent.InstanceId == card.InstanceId;

            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && CardEffectCommons.CanTriggerOnMove(ctx, card, PermanentCondition);

            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnMove,
                canUse: CanUse,
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card),
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Your Turn] When this Digimon moves from the breeding area to the battle area, draw 1 (uncapped)."));
        }

        return effects;
    }
}
