// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_107.cs
// AS-IS has two branches (an Option):
//   [Main] Trigger <De-Digivolve 1> on 1 of your opponent's Digimon. Then, if that Digimon's play cost is
//   4 or less, delete it.
//   [Security] Add this card to its owner's hand.  -> AddThisCardToHandEffect (verbatim shape, mirrors
//     ST3_14 / BT3_106).
//
// STOP [Main] (genuine primitive gap, grepped 2x+ per rule 4): needs "interactively select 1 opponent
// Digimon, de-digivolve it by 1, THEN — reading the RESULTING top card's play cost AFTER that mutation —
// conditionally delete the SAME selected permanent." Grepped Assets/Scripts/Script/CardEffectCommons/
// CardPortingFramework.cs's IActivatedCardEffect catalog: ActivatedSelectAndDeDigivolveEffect.Apply (backing
// CardEffectFactory.SelectAndDeDigivolveEffect, used by this card's sibling BT3_064/BT3_107-Main-first-half)
// applies exactly one DeDigivolveKind mutation per selected id and has no hook to chain a follow-up
// mutation gated on the post-de-digivolve state; DestroyPermanentsEffect (direct-delete a PRE-COMPUTED
// target list) has no way to receive "the target this OTHER effect just selected and mutated" as its input
// — each IActivatedCardEffect is resolved independently by ActivatedEffectResolver.ResolveListAsync's fixed
// switch (ActivatedEffectResolver.cs), with no sequencing/data-passing between two list entries. No factory
// composes "select -> de-digivolve -> conditional-destroy-of-the-same-target based on the post-mutation
// state." Per rule 4 this is a primitive gap, out of scope for a single-card porting pass. — Sonnet

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_107 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "Trigger <De-Digivolve 1> on 1 of your opponent's Digimon. Then, if that Digimon's
        // play cost is 4 or less, delete it." — needs a "select -> de-digivolve -> conditional destroy of
        // the same (mutated) target" primitive that does not exist yet (see file header).
        // if (timing == EffectTiming.OptionSkill) { ... }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
