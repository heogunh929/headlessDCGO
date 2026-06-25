namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with AS-IS memory/cost flow after card payment logic is ported.
public interface IHeadlessMemoryController : IHeadlessMatchStateResettable
{
    HeadlessMemoryState Current { get; }

    void Initialize(int initialMemory, int minimum = -10, int maximum = 10);

    HeadlessMemoryState Set(int value);

    HeadlessMemoryState Add(int amount);

    bool CanPay(int cost);

    HeadlessMemoryState Pay(int cost);
}
