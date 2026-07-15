namespace HeadlessDCGO.Engine.Headless.Effects;

using System;

/// <summary>Thrown when a trigger-window resolution cannot answer an agent choice synchronously: the port has
/// opened an agent choice on the choice controller and must unwind the C# call stack, leaving the pass state
/// untouched so a later resume re-enters at the recorded cursor. Mirrors the engine's
/// <c>DeferredChoicePendingException</c> for activated-effect bodies. Caught by the seam drivers
/// (GameFlowProcessor / MetadataActionProcessor / SecurityResolver / MatchStateMutationSink) to park the window;
/// thrown from <see cref="SkillWindowContinuation"/>. Scripted test ports never throw it.
///
/// Relocated here from the retired <c>WindowResolver.cs</c> (the old registry-currency window loop) during the
/// C2b dead-path deletion — it remains the live pause signal for the mirror MultipleSkills window engine.</summary>
public sealed class WindowChoicePendingException : Exception
{
    public WindowChoicePendingException(string message) : base(message)
    {
    }

    public WindowChoicePendingException() : base("Window suspended for an agent choice.")
    {
    }
}
