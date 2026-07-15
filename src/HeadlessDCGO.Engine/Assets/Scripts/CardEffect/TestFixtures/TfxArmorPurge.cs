// TEST FIXTURE (not a real card — no real [Armor Purge] card is ported yet; this carries the REAL factory
// shape). [WhenPermanentWouldBeDeleted] returns the printed-keyword form CardEffectFactory.ArmorPurgeEffect,
// exactly the AS-IS consumer shape (DCGO BT9_038.cs:26: `ArmorPurgeEffect(card: card)` at
// EffectTiming.WhenPermanentWouldBeDeleted). Used by the C-Del 3c-2b F68 window conversion to witness the
// retired-gate Armor Purge firing through the AS-IS PRE cut-in window (trash the top card, promote the
// under-source, willBeRemoveField=false — the permanent survives in its lower form). Inert in actual play
// (no real card numbered "TfxArmorPurge").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxArmorPurge : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.ArmorPurgeEffect(card: card));
        }

        return cardEffects;
    }
}
