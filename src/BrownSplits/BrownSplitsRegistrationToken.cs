using System;

namespace BrownSplits;

// Represents one component's runtime registration and unregisters it exactly once when disposed.
internal sealed class BrownSplitsRegistrationToken : IDisposable
{
    private Action? unregister;

    public BrownSplitsRegistrationToken(Action unregister)
    {
        this.unregister = unregister;
    }

    public void Dispose()
    {
        Action? unregisterOnce = unregister;
        unregister = null;

        unregisterOnce?.Invoke();
    }
}
