// PRIM fidelity: CanNotBeBlockedStaticSelfEffect(defenderCondition) — the attacker can't be blocked ONLY BY
// defenders matching the predicate (AS-IS defenderCondition, e.g. "Digimon with no digivolution cards").
// Restored param (was simplified to unconditional). Verified via ContinuousRestrictionGate.EvaluateBeBlocked.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
EngineContext ctx = EngineContext.CreateDefault(randomSeed: 88);

HeadlessEntityId Card(string tag, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId(tag);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner, Metadata: new Dictionary<string, object?>()));
    return id;
}

var attacker = Card("ATT", P1);
var noblock = Card("NOBLOCK", P2);   // the defender the attacker can't be blocked by
var other = Card("OTHER", P2);       // an ordinary blocker

// attacker: "can't be blocked by NOBLOCK" (defenderCondition matches only NOBLOCK)
// MIGRATION-NOTE (P7 test-fix): CanNotBeBlockedStaticSelfEffect returns CannotBlockClass
// (Assets/Scripts/Script/CardEffects/CannotBlockClass.cs), a new-model kind-class with no
// ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The gate this test
// checks (ContinuousRestrictionGate.EvaluateBeBlocked -> RestrictionScan.IsRestricted) reads only the
// substrate EffectRegistry (Continuous/Restriction query roles) — it has no new-model interface-scan union
// (unlike ContinuousKeywordGate.HasKeyword), so there is no buildable way to make this restriction
// observable yet. The factory call is kept (still exercises construction); Register/ToBinding is dropped.
// Assertions below are UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not silently weakened.
CardEffectFactory.CanNotBeBlockedStaticSelfEffect(
    defenderCondition: p => p.InstanceId == noblock,
    isInheritedEffect: false,
    card: new CardSource(ctx, attacker, P1),
    condition: null,
    effectName: "CantBeBlocked");

bool restrictedByNoblock = ContinuousRestrictionGate.EvaluateBeBlocked(ctx, attacker, noblock).IsRestricted;
bool restrictedByOther = ContinuousRestrictionGate.EvaluateBeBlocked(ctx, attacker, other).IsRestricted;

int fails = 0;
void Check(bool cond, string label) { Console.WriteLine((cond ? "PASS " : "FAIL ") + label); if (!cond) fails++; }
Check(restrictedByNoblock, "can't be blocked BY the matching defender (NOBLOCK) -> restricted");
Check(!restrictedByOther, "CAN be blocked by a non-matching defender (OTHER) -> not restricted");
if (fails > 0) { Environment.Exit(1); }
Console.WriteLine("\n2 test(s) passed.");
