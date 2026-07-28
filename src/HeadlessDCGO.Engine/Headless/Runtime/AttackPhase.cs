namespace HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>
/// Sub-states of a single attack, mirroring AS-IS <c>AttackProcess.ProcessNextState()</c>
/// (Counter/Block/Battle/End/CleanUp). The AttackProcess pump advances the attack one phase per step;
/// battle/security resolve INLINE (IBattle/ISecurityCheck), so the loop pauses for choices via the pump
/// gate and resumes — the resolver-driven DeletionReplacement/PiercingSecurity park phases are retired.
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

    /// <summary>Battle/security resolved; end-attack triggers not yet collected.</summary>
    Resolved,

    /// <summary>End-attack triggers fired; ready to clear the attack.</summary>
    Completed,
}
