// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnLeaveFieldAnyone reactor: whenever any Digimon
// leaves the battle area, its controller loses 1 memory — with NO once-per-turn cap. This is the witness the
// real AD1_025 cannot be (AD1_025 is [Once Per Turn], so its cap ALONE collapses N fires to one, hiding whether
// the D-2 batch-collapse is doing anything). Uncapped, the observable splits: a batch of N simultaneous leaves
// costs -1 IFF the collapse fires the reactor once, -N if the bridge fired per CardMoved. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOnLeaveFieldCounter : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnLeaveFieldAnyone)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnLeaveFieldAnyone,
                // Anyone-scoped, unconditional (the batch-collapse itself is under test, not a scope gate); the
                // reactor stays on the field while OTHER cards leave.
                canUse: null,
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new MemoryBody(-1),
                maxCountPerTurn: null,   // UNCAPPED — the whole point: no cap to mask a per-event over-fire.
                isOptional: false,
                description: "[All Turns] When any Digimon leaves the battle area, lose 1 memory (uncapped)."));
        }

        return effects;
    }
}
