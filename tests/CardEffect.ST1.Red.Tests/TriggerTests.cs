namespace ST1RedTests;

using System.Collections;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Triggered memory effects: ST1_06 (<Blocker> + [When Attacking] lose 2) and ST1_09 ([Your Turn] +3 on block).
//
// (deferred-queue re-aim, ST2.Blue toolbox) These were OLD-model white-box reads against the retired invention
// bridge: the Blocker was probed via EffectRegistry.GetKeywordEffects (there is no keyword table in AS-IS), and
// the triggers were driven via an EffectBinding.ResolveAsync that no longer exists for a new-model kind-class
// (RegisterOnEnterPlay yields NO binding for an ActivateClass, hence "Sequence contains no matching element").
// Re-aimed onto the live AS-IS surface exactly as ST2_07/11: <Blocker> is a live IBlockerEffect in the
// permanent's None-timing EffectList; the [When Attacking] / [When Blocked] triggers are ActivateClasses whose
// self-scope gate (CanTriggerOnAttack -> CanUseCondition) is evaluated against the live per-attacker Hashtable
// (OnAttackCheckHashtableOfPermanent), and whose body is driven live through Activate.
internal static class TriggerTests
{
    private static readonly HeadlessPlayerId P1 = new(1);
    private static readonly HeadlessPlayerId P2 = new(2);

    public static (string Name, Func<Task> Body)[] Cases => new (string, Func<Task>)[]
    {
        ("ST1_06: <Blocker> is a live IBlockerEffect in the permanent's EffectList", ST1_06_Blocker),
        ("ST1_06: [When Attacking] resolves to -2 memory", ST1_06_LoseMemory),
        ("ST1_06: another ally attacking does NOT trigger it (self-scope)", ST1_06_OtherAllyNoFire),
        ("ST1_09: gains 3 memory on the owner's turn", () => ST1_09_Memory(ownerTurn: true, expected: 3)),
        ("ST1_09: no memory gain on the opponent's turn", () => ST1_09_Memory(ownerTurn: false, expected: 0)),
        ("ST1_09: another ally being blocked does NOT trigger it (self-scope)", ST1_09_OtherAllyNoFire),
    };

    // (re-aim) <Blocker> is a new-model kind-class (BlockerSelfStaticEffect) read LIVE from the permanent's
    // None-timing EffectList — there is no EffectRegistry keyword table in AS-IS. RegisterOnEnterPlay attaches
    // the effect to this instance's cEntity_EffectController (no binding for a new-model kind-class), which is
    // what the live EffectList scan reads. Same surface ST2_07 asserts.
    private static async Task ST1_06_Blocker()
    {
        (EngineContext context, HeadlessEntityId card) = await Card("ST1_06");
        CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_06(), "ST1_06", new CardSource(context, card, P1));
        using (AmbientMatchContext.Enter(context))
        {
            bool hasBlocker = new Permanent(context, card, P1).EffectList(EffectTiming.None)
                .Any(e => e is IBlockerEffect && string.Equals(e.EffectName, "Blocker", StringComparison.Ordinal));
            AssertTrue(hasBlocker, "ST1_06 carries <Blocker> (live IBlockerEffect in its EffectList)");
        }
    }

    private static async Task ST1_06_LoseMemory()
    {
        (EngineContext context, HeadlessEntityId card) = await Card("ST1_06");
        // Attacking happens on the owner's turn; memory deltas are turn-relative, so seat P1 as the turn player
        // (AddMemory(-2) then moves the absolute count away from the turn player: 3 -> 1).
        context.TurnController.Initialize(new[] { P1, P2 }, P1);
        context.TurnController.SetPhase(HeadlessPhase.Main);
        var effect = (ActivateClass)new ST1_06().CardEffects(EffectTiming.OnAllyAttack, new CardSource(context, card, P1)).Single();
        context.MemoryController.Set(3);

        using (AmbientMatchContext.Enter(context))
        {
            Hashtable self = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(context, card, P1), effect);
            AssertTrue(effect.CanUseCondition(self), "[When Attacking] triggers when THIS card attacks");
            await effect.Activate(self);
        }

        AssertEqual(1, context.MemoryController.Current.Current, "3 - 2 = 1 memory when THIS card attacks");
    }

    // Self-scope: when ANOTHER ally attacks (AttackingPermanent != this card), the -2 must NOT trigger.
    private static async Task ST1_06_OtherAllyNoFire()
    {
        (EngineContext context, HeadlessEntityId card) = await Card("ST1_06");
        HeadlessEntityId other = await Place(context, P1, "p1:battle:OTHER");
        var effect = (ActivateClass)new ST1_06().CardEffects(EffectTiming.OnAllyAttack, new CardSource(context, card, P1)).Single();

        using (AmbientMatchContext.Enter(context))
        {
            Hashtable notSelf = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(context, other, P1), effect);
            AssertTrue(!effect.CanUseCondition(notSelf), "another ally attacking does NOT trigger the -2");
        }
    }

    private static async Task ST1_09_Memory(bool ownerTurn, int expected)
    {
        (EngineContext context, HeadlessEntityId card) = await Card("ST1_09");
        context.TurnController.Initialize(new[] { P1, P2 }, ownerTurn ? P1 : P2);
        context.TurnController.SetPhase(HeadlessPhase.Main);
        var effect = (ActivateClass)new ST1_09().CardEffects(EffectTiming.OnBlockAnyone, new CardSource(context, card, P1)).Single();
        context.MemoryController.Set(0);

        using (AmbientMatchContext.Enter(context))
        {
            // "When this Digimon is blocked" — the attacking permanent that got blocked is THIS card
            // (CanUseCondition = CanTriggerOnAttack && IsOwnerTurn). On the opponent's turn the [Your Turn] arm
            // of the gate reads false, so the effect does not fire (a live condition read, not a driven no-op).
            Hashtable self = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(context, card, P1), effect);
            AssertTrue(effect.CanUseCondition(self) == ownerTurn, ownerTurn ? "+3 fires on owner turn" : "does not fire on opponent turn");
            if (effect.CanUseCondition(self) && effect.CanActivateCondition(self))
            {
                await effect.Activate(self);
            }
        }

        AssertEqual(expected, context.MemoryController.Current.Current, ownerTurn ? "+3 on owner turn" : "no change on opponent turn");
    }

    // Self-scope: when ANOTHER ally is blocked (AttackingPermanent != this card), the +3 must NOT fire.
    private static async Task ST1_09_OtherAllyNoFire()
    {
        (EngineContext context, HeadlessEntityId card) = await Card("ST1_09");
        HeadlessEntityId other = await Place(context, P1, "p1:battle:OTHER");
        context.TurnController.Initialize(new[] { P1, P2 }, P1);
        context.TurnController.SetPhase(HeadlessPhase.Main);
        var effect = (ActivateClass)new ST1_09().CardEffects(EffectTiming.OnBlockAnyone, new CardSource(context, card, P1)).Single();

        using (AmbientMatchContext.Enter(context))
        {
            Hashtable notSelf = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(context, other, P1), effect);
            AssertTrue(!effect.CanUseCondition(notSelf), "another ally being blocked does NOT trigger the +3");
        }
    }

    // A ported Digimon on the owner's battle area (so its Permanent has a live TopCard and self-scope resolves).
    private static async Task<(EngineContext, HeadlessEntityId)> Card(string number)
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
        CardDatabase cards = (CardDatabase)context.CardRepository;
        cards.Upsert(new CardRecord(new HeadlessEntityId(number), number, number, new Dictionary<string, object?>(), CardType: "Digimon"));
        var id = new HeadlessEntityId($"p1:battle:{number}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
        return (context, id);
    }

    private static async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string idValue)
    {
        CardDatabase cards = (CardDatabase)context.CardRepository;
        var defId = new HeadlessEntityId($"DEF:{idValue}");
        cards.Upsert(new CardRecord(defId, defId.Value, idValue, new Dictionary<string, object?>(), CardType: "Digimon"));
        var id = new HeadlessEntityId(idValue);
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
        return id;
    }

    private static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
