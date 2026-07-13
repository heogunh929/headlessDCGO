// TEST FIXTURE (not a real card). (B-5) A CAPPED ([Once Per Turn]) uniform activated effect whose BODY is a
// per-shape SELECT (ActivatedSelectEffect, Mode.Tap = suspend). Before B-5 the resolver's per-shape select case
// had NO once-per-turn cap; the migration makes the select a composable IEffectBody of a uniform ActivatedEffect,
// so the shared cap gate now applies. Proves cap was wired onto a per-shape body.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class TfxCappedSelectSuspend : CEntity_Effect
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
                    SelectPermanentEffect.Mode.Tap, "[Once Per Turn] Suspend 1 of your opponent's Digimon."),
                maxCountPerTurn: 1,
                isOptional: false,
                description: "[Once Per Turn] Suspend 1 of your opponent's Digimon."));
        }

        return effects;
    }
}
