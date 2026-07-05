// TEST FIXTURE (not a real card). [Main] (OptionSkill) exercises select-follow-up primitives, branching on a
// "followMode" metadata flag: "seq" returns TWO effects (select+suspend, then draw) to verify the resolver runs
// a returned effect LIST in order (unconditional chaining); "deck" / "sec" select an opponent Digimon and
// return-to-deck / put-to-security via the new SelectAnd* factories. Used by tests/PRIM-P0 (Build Order 3).
// Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSelectFollowUp : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing != EffectTiming.OptionSkill)
        {
            return effects;
        }

        string mode = card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) &&
                      record is not null && record.Metadata.TryGetValue("followMode", out object? raw) && raw is string s
            ? s
            : "seq";

        bool IsOpponentDigimon(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

        switch (mode)
        {
            case "seq":
                // Unconditional chain: select+suspend an opponent Digimon, THEN draw 1. Both steps in the list.
                effects.Add(CardEffectFactory.SelectAndSuspendEffect(card, IsOpponentDigimon, maxCount: 1, canEndNotMax: false, "Suspend 1 of your opponent's Digimon."));
                effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
                break;
            case "deck":
                effects.Add(CardEffectFactory.SelectAndReturnToDeckEffect(card, IsOpponentDigimon, maxCount: 1, toTop: false, canEndNotMax: false, "Return 1 of your opponent's Digimon to the bottom of the deck."));
                break;
            case "sec":
                effects.Add(CardEffectFactory.SelectAndPutSecurityEffect(card, IsOpponentDigimon, maxCount: 1, toTop: true, canEndNotMax: false, "Place 1 of your opponent's Digimon on top of security."));
                break;
        }

        return effects;
    }
}
