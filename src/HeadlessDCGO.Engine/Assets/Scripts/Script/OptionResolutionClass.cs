// Source: DCGO/Assets/Scripts/Script/OptionResolutionClass.cs
// (EFFECT-MODEL REBUILD / P6 missing-type) 1:1 mirror of the AS-IS `OptionResolutionClass` — a small
// `ICardEffect, IOptionResolutionEffect` kind-class used for "instead of trashing after use" effects
// (IOptionResolutionEffect, CardEffectInterfaces.cs / mirror CardEffectInterfaces.cs:536-540) whose
// resolution is a caller-supplied coroutine + optional condition, rather than a fixed CardEffects() override.
//
// Namespace: `...Script.CardEffectCommons`, matching `ICardEffect` (abstract class, ICardEffect.cs) and
// `IOptionResolutionEffect` (CardEffectInterfaces.cs) which this class derives from/implements — both already
// live there.
//
// ADAPTATIONS (same translation rules as ICardEffect.cs / ActivateICardEffectExtensionClass):
//   - `using UnityEngine;` / `using Photon;` stripped (no Unity/Photon member referenced by this class).
//   - `Func<CardSource, IEnumerator>` -> `Func<CardSource, Task>` (coroutine action -> async action).
//   - `IEnumerator Resolve(CardSource)` -> `Task Resolve(CardSource)` (per IOptionResolutionEffect's own
//     mirror translation note, CardEffectInterfaces.cs:30).
//   - `ContinuousController.instance.StartCoroutine(ResolutionCoroutine(optionCard))` -> `await
//     ResolutionCoroutine(optionCard)` (the established `StartCoroutine(X) -> await X` rule).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

// AS-IS OptionResolutionClass.cs:8-34.
public class OptionResolutionClass : ICardEffect, IOptionResolutionEffect
{
    // AS-IS OptionResolutionClass.cs:10-14.
    public void SetUpOptionResolutionClass(Func<CardSource, Task> resolutionCoroutine, Func<CardSource, bool> resolutionCondition = null)
    {
        ResolutionCoroutine = resolutionCoroutine;
        ResolutionCondition = resolutionCondition;
    }

    // AS-IS OptionResolutionClass.cs:16-17.
    Func<CardSource, bool> ResolutionCondition { get; set; }
    Func<CardSource, Task> ResolutionCoroutine { get; set; }

    // AS-IS OptionResolutionClass.cs:19-22.
    public bool CanResolve(CardSource optionCard)
    {
        return ResolutionCondition == null || ResolutionCondition(optionCard);
    }

    // AS-IS OptionResolutionClass.cs:24-33.
    public async Task Resolve(CardSource optionCard)
    {
        if (CanResolve(optionCard))
        {
            if (ResolutionCoroutine != null)
            {
                await ResolutionCoroutine(optionCard);
            }
        }
    }
}
