// Source: Assets/Scripts/CardEffect/BT16/Blue/BT16_025.cs (1:1 mirror — partial; see fidelity debt below)
//
// <Partition> ([Blue Lv.4]/[Green Lv.4]) — When this Digimon would leave the field by an effect (not by battle,
//   not by its owner's own effect), you may play one Blue Lv.4 and one Green Lv.4 Digimon card from among its
//   digivolution cards without paying the cost.
//   AS-IS (BT16_025.cs:13-32): partitionConditions = { PartitionCondition(4, Blue), PartitionCondition(4, Green) };
//     timing == WhenRemoveField -> PartitionSelfEffect(isInheritedEffect:false, card, condition:null,
//     cardSourceConditions:partitionConditions) AND PartitionSelfEffect(isInheritedEffect:true, ...) — the
//     non-inherited + inherited pair (both grants carry the same two colour groups).
//   Headless mirror: verbatim factory calls. The grants register the Partition keyword (SelfKeywordBatch2Effect
//   -> ContinuousKeywordGate.Partition) and carry the two-entry PartitionCondition list on the grant binding
//   (PartitionCondition.PartitionConditionsKey). The C-1 PRE would-be-deleted window (DeletionReplacementTiming /
//   DeletionReplacementGate.TryPartitionPlaySourceAsync) recognises the keyword via HasReplacementKeyword and
//   plays exactly one source per colour group (pick #1 = group[0] Blue, pick #2 = group[1] Green) as a repeated
//   single-select; effect deletions only (AS-IS !IsByBattle) and not by the owner's own effect (!IsOwnerEffect).
//
// DNA Digivolution ([Blue Lv.4] + [Green Lv.4]) — AS-IS (BT16_025.cs:38-125): timing == None ->
//   AddJogressConditionClass with GetJogress returning two JogressConditionElements: an own-battle-area level-4
//   Blue Digimon and an own-battle-area level-4 Green Digimon (Levels_ForJogress contains 4).
//   Headless mirror: CardEffectFactory.GetJogressConditionClass with the two Permanent predicates (colour +
//   jogress-level-4; the primitive already scopes each slot to the owner's battle-area Digimon).
//
// FIDELITY DEBT (design item C1w-25) — the two interactive suspend effects are NOT yet ported: no 1:1 headless
// primitive exists for their shapes. Each returns empty so the card still registers its Partition grants + DNA:
//   * [When Digivolving] (BT16_025.cs:131-186) — "Suspend all of your opponent's Digimon with as many or fewer
//     digivolution cards as this Digimon. Then, if DNA digivolving, none of your opponent's Digimon can unsuspend
//     until the end of their turn." The suspend-all half maps to ApplyToAllMatchingBody + sink Suspend, and the
//     jogress marker IS surfaced — FusionDigivolveHelpers stamps FusionDigivolveHelpers.IsJogressKey onto the
//     WhenDigivolving event metadata (FusionDigivolveHelpers.cs:145-148), readable by canUse via the resolve
//     context's "event.<key>" values. The REAL gaps (C-3 재상환 P2-4② corrected): ① the driving event does not
//     reach the BODY — IEffectBody.Apply/ApplyAsync receives (card, sink, selected) with no resolve-context, so
//     the "if DNA digivolving" branch cannot be decided where the grant is emitted; ② no composite
//     "apply-to-all-matching THEN conditionally grant a player-scope restriction" body exists. Dropping the DNA
//     clause would be an infidelity. GAP = resolve-context plumbing into the body + the composite body.
//   * [When Attacking] [Once Per Turn] (BT16_025.cs:192-271) — "Suspend 1 of your opponent's unsuspended Digimon.
//     If this effect didn't suspend, unsuspend this Digimon." The conditional fallback (suspend-1 when a target
//     exists, ELSE unsuspend self) is not expressible via SelectBody(Mode.Tap) — it needs a body whose no-target
//     path unsuspends self. GAP = suspend-else-unsuspend-self fallback body.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT16.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;

public sealed class BT16_025 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // AS-IS BT16_025.cs:13-16 — the two colour groups, shared by both Partition grants.
        var partitionConditions = new List<PartitionCondition>
        {
            new PartitionCondition(4, "Blue"),
            new PartitionCondition(4, "Green"),
        };

        if (timing == EffectTiming.WhenRemoveField)
        {
            cardEffects.Add(CardEffectFactory.PartitionSelfEffect(
                isInheritedEffect: false, card: card, condition: null, cardSourceConditions: partitionConditions));
            cardEffects.Add(CardEffectFactory.PartitionSelfEffect(
                isInheritedEffect: true, card: card, condition: null, cardSourceConditions: partitionConditions));
        }

        if (timing == EffectTiming.None)
        {
            // AS-IS PermanentCondition1/2 (BT16_025.cs:55-109): own battle-area Digimon whose top card is
            // Blue/Green and whose Jogress levels contain 4. The primitive scopes each slot to the owner's
            // battle-area Digimon, so only the colour + jogress-level-4 predicate remains here.
            bool BlueLevel4(Permanent permanent) =>
                permanent.TopCard.HasCardColor("Blue") && permanent.TopCard.JogressLevelsAgainst(card).Contains(4);

            bool GreenLevel4(Permanent permanent) =>
                permanent.TopCard.HasCardColor("Green") && permanent.TopCard.JogressLevelsAgainst(card).Contains(4);

            cardEffects.Add(CardEffectFactory.GetJogressConditionClass(
                BlueLevel4, "a level 4 Blue Digimon",
                GreenLevel4, "a level 4 Green Digimon",
                card));
        }

        return cardEffects;
    }
}
