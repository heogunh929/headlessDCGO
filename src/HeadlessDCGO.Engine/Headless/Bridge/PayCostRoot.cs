namespace HeadlessDCGO.Engine.Headless.Bridge;

/// <summary>(PRIM-P0 B.O.4 #1) Which action's cost is being paid in the current BeforePayCost window — lets a
/// card's [BeforePayCost] effect fire only for the intended action (AS-IS ChangeCostClass rootCondition:
/// Hand-play vs digivolve). See docs/porting/before_pay_cost_root_gating_design.md.</summary>
public enum PayCostRoot
{
    None = 0,
    Play,
    Digivolve,
    Option,
}
