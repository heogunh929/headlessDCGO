namespace HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>
/// Sub-states of a single attack, mirroring Unity AS-IS <c>AttackProcess.ProcessNextState()</c>
/// (Counter/Block/Battle/Security/End/CleanUp). Each <see cref="AttackPipeline"/> step advances
/// the attack by exactly one phase so the common loop can pause for choices and resume.
/// </summary>
public enum AttackPhase
{
    /// <summary>No active attack.</summary>
    None = 0,

    /// <summary>Attack declared; block timing has not been offered yet.</summary>
    Declared,

    /// <summary>Block choice requested and pending; the loop pauses until it is resolved.</summary>
    Blocking,

    /// <summary>Block timing finished (blocked or skipped); ready for battle/security resolution.</summary>
    Combat,

    /// <summary>(F-6.8) Battle deletion is deferred for an OPTIONAL would-be-deleted replacement choice
    /// (Barrier/Evade/…): the pipeline parks here while the owner decides, then finalizes the battle.</summary>
    DeletionReplacement,

    /// <summary>Battle/security resolved; end-attack triggers not yet collected.</summary>
    Resolved,

    /// <summary>End-attack triggers fired; ready to clear the attack.</summary>
    Completed,
}
