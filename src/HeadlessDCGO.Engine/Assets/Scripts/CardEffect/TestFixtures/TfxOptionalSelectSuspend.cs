// TEST FIXTURE (not a real card). (B-5) An OPTIONAL ("you may") uniform activated effect whose BODY is a
// per-shape SELECT (ActivatedSelectEffect, Mode.Tap = suspend). Before B-5 the resolver's per-shape select case
// could not present the AS-IS OptionalSkill yes/no; the migration makes the select a composable IEffectBody of a
// uniform ActivatedEffect, so the shared optional gate (ConfirmOptionalAsync) now applies. Proves the optional
// yes/no was wired onto a per-shape body — declining is a no-op.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class TfxOptionalSelectSuspend : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            bool CanTarget(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.None,
                canUse: null,
                canActivate: null,
                body: new ActivatedSelectEffect(card, CanTarget, maxCount: 1, canNoSelect: false, canEndNotMax: false,
                    SelectPermanentEffect.Mode.Tap, "You may suspend 1 of your opponent's Digimon."),
                maxCountPerTurn: null,
                isOptional: true,
                description: "You may suspend 1 of your opponent's Digimon."));
        }

        return effects;
    }
}
