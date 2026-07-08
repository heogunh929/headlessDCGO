// STOP: AS-IS BT3_102 [Main] presents the OPPONENT a binary choice (SetBoolSelection: "Discard" vs "Not
// Discard") between trashing the top card of their own security stack, or (if declined / no security
// cards) triggering <Recovery +1 (Deck)> for the owner. No existing headless primitive models a bool
// player-choice branch (no ChoiceType.Bool / SetBoolSelection equivalent in the porting framework), so this
// stays STOP rather than guessing an unconditional branch (which would drop the opponent's actual choice).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_102 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] opponent may discard the top of their security; if not, trigger <Recovery +1
        // (Deck)> — no bool-choice primitive exists (see file header).

        return cardEffects;
    }
}
