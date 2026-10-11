namespace TopperUpper
{
    /// <summary>The loose-item pickup's two hooks in the module (Module.cs declares them): its turn each frame, last in
    /// Update (the log fix's minute line and notice first, then the pickup), and its undo step after an error.</summary>
    internal sealed partial class Module
    {
        static partial void TickLooseItems()
        {
            Loose.QuietRepeatAsk.Tick();
            Loose.Pickup.Tick();
        }

        static partial void ForgetLooseItems()
        {
            Loose.Pickup.Forget();
            Loose.QuietRepeatAsk.Forget();
        }
    }
}
