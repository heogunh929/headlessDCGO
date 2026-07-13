// Source: DCGO/Assets/Scripts/Script/OptionalSkill.cs
// (EFFECT-MODEL REBUILD / P6 stage A) Mirror of the AS-IS `OptionalSkill` — the "Will you use ~?" yes/no
// prompt `ActivateICardEffectExtensionClass.Activate_Optional` reaches through
// `GManager.instance.GetComponent<OptionalSkill>().SelectOptional(cardEffect, hash)` (AS-IS ICardEffect.cs:1054).
//
// The AS-IS body (OptionalSkill.cs:14-133) is ~90% presentation: trash-card display, outline/highlight
// toggling, command-text panels, Photon RPC plumbing (`SetUseOptional` RPC → QueuePlayerSelection), an AI
// 0.9-probability auto-answer. The GAME-LOGIC skeleton ported here 1:1:
//   1. the deciding player = the effect source card's OWNER (AS-IS `Player player = cardEffect.EffectSourceCard.Owner`);
//   2. the message = `Will you use "{EffectName}"?`, with the `EffectTargets(hash)` targeting variant
//      (OptionalSkill.cs:24-33);
//   3. WAIT for the player's yes/no (AS-IS WaitUntil(HasPlayerSelection) ← RPC; mirror = the engine
//      ChoiceProvider, the same substrate seam every other selection flow uses — a select answers YES, a skip
//      answers NO, exactly the established ChoiceType.OptionalEffect prompt shape);
//   4. `cardEffect.SetUseOptional(answer)` (OptionalSkill.cs:130) — the flag Activate_Execute gates on.
// Component lifetime: ONE match-scoped instance via GManager.GetComponent<OptionalSkill>() (context-cached,
// same as SelectPermanentEffect/SelectCardEffect — see GManager.cs bridge W4 note).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public class OptionalSkill
{
    // AS-IS OptionalSkill.cs:11.
    public string waitingText { get; set; } = "The opponent is considering whether to use the effect.";

    private EngineContext? _context;

    public void AttachContext(EngineContext context) => _context = context;

    bool _useOptional = false;

    // AS-IS OptionalSkill.cs:14-133 (game-logic skeleton; UI/Photon stripped — see file header).
    public async Task SelectOptional(ICardEffect cardEffect, Hashtable hash)
    {
        EngineContext context = _context ?? AmbientMatchContext.Require();

        _useOptional = false;

        string _Message;

        List<Permanent> effectTargets = cardEffect.EffectTargets != null ? cardEffect.EffectTargets(hash) : null;

        if (effectTargets == null || effectTargets.Count == 0)
        {
            _Message = $"Will you use \"{cardEffect.EffectName}\"?";
        }
        else
        {
            _Message = $"Will you use \"{cardEffect.EffectName}\" targeting {string.Join(", ", effectTargets.Select(permanent => permanent.TopCard.CardNames[0]))}?";
        }

        // AS-IS OptionalSkill.cs:36-58: trash-card display (UI, stripped).
        // AS-IS OptionalSkill.cs:60-115: outline/highlight + command panel + Photon RPC / AI auto-answer
        // (presentation + transport, stripped — the mirror transport is the ChoiceProvider below).

        HeadlessPlayerId decider = cardEffect.EffectSourceCard.Owner;
        var request = new ChoiceRequest(
            ChoiceType.OptionalEffect,
            decider,
            _Message,
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.Custom,
            new[]
            {
                new ChoiceCandidate(
                    cardEffect.EffectSourceCard.InstanceId,
                    string.IsNullOrEmpty(cardEffect.EffectName) ? cardEffect.EffectDiscription : cardEffect.EffectName,
                    ChoiceZone.Custom,
                    IsSelectable: true,
                    ownerId: decider),
            });

        // AS-IS OptionalSkill.cs:117-119: WaitUntil(player.HasPlayerSelection()) → ValueSelection.ValueAsBool().
        ChoiceResult decision = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
        _useOptional = !decision.IsSkipped && decision.SelectedIds.Count > 0;

        // AS-IS OptionalSkill.cs:121-128: panel/outline teardown (UI, stripped).

        cardEffect.SetUseOptional(_useOptional);

        // AS-IS OptionalSkill.cs:132: TrashHandCard.SetActive(false) (UI, stripped).
    }
}
