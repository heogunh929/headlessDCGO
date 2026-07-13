// Source: Assets/Scripts/CardEffect/BT19/Blue/BT19_024.cs (1:1 mirror — partial; see fidelity debt below)
//
// <Decode> ([Blue Lv.4]) — When this Digimon would leave the field by an effect, you may play 1 Blue Lv.4
//   Digimon card from among its digivolution cards without paying the cost.
//   AS-IS (BT19_024.cs:14-23): timing == WhenRemoveField ->
//     CardEffectFactory.DecodeSelfEffect(card, isInheritedEffect:false, decodeStrings:{ "(Blue Lv.4)",
//     "Blue Level 4" }, sourceCondition: SourceCondition, condition:null), where
//     SourceCondition(source) = source.HasCardColor(Blue) && source.HasLevel && source.IsLevel4.
//   Headless mirror: verbatim factory call. The grant registers the Decode keyword (SelfKeywordBatch2Effect ->
//   ContinuousKeywordGate.Decode) AND carries the per-card sourceCondition on the grant binding
//   (Decode.DecodeSourceConditionKey). The C-1 PRE would-be-deleted window (DeletionReplacementTiming /
//   DeletionReplacementGate.TryDecodePlaySourceAsync) recognises the keyword via HasReplacementKeyword and offers
//   Decode ONLY when a digivolution source passes SourceCondition (Blue Lv.4) — battle deletions offer nothing
//   (AS-IS !IsByBattle). decodeStrings is the AS-IS display label (carried for fidelity); the real candidate
//   filter is the predicate.
//
// FIDELITY DEBT (design item C1w-24) — the three interactive hand/source effects below are NOT yet ported: no
// 1:1 headless primitive exists for their shapes, and inventing a bridge would violate the AS-IS-mirror rule.
// Precise gaps (each returns empty so the card still registers its Decode grant + is Decode-testable):
//   * [On Play] / [When Digivolving] (BT19_024.cs:50-238) — "place 1 [Aqua]/[Sea Animal] Digimon card from your
//     hand as 1 of your Digimon's BOTTOM digivolution card." Needs a TWO-STEP interactive body (SelectHand of an
//     Aqua Digimon, canNoSelect:true -> SelectPermanent of an own Digimon -> AddDigivolutionCardsBottom). The
//     uniform ActivatedEffect exposes a single BuildRequest; no sequential hand-then-permanent + place-as-bottom
//     primitive exists. GAP = compound-interactive place-as-bottom-source body.
//   * [End of Attack] (Once Per Turn) ESS (BT19_024.cs:244-325) — "play 1 level 4 or lower [Aqua]/[Sea Animal]
//     Digimon card from THIS Digimon's own digivolution cards without paying the cost." SelectAndPlayFromZoneEffect
//     covers Trash/Hand roots only, not SelectCardEffect.Root.Custom over card.PermanentOfThisCard().
//     DigivolutionCards. GAP = play-from-own-digivolution-sources root.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT19_024 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenRemoveField)
        {
            // AS-IS SourceCondition (BT19_024.cs:16-19): a Blue, level-4 digivolution source. IsLevel4 =
            // HasLevel && Level == 4 (CardSource.cs:2929).
            bool SourceCondition(CardSource source) =>
                source.HasCardColor("Blue") && source.HasLevel && source.IsLevel(4);

            string[] decodeStrings = { "(Blue Lv.4)", "Blue Level 4" };
            cardEffects.Add(CardEffectFactory.DecodeSelfEffect(
                isInheritedEffect: false, card: card, condition: null,
                decodeStrings: decodeStrings, sourceCondition: SourceCondition));
        }

        return cardEffects;
    }
}
