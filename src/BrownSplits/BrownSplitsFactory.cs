using System;
using LiveSplit.Model;
using LiveSplit.UI.Components;

namespace BrownSplits;

// Component Factory used by LiveSplit to create the BrownSplits component.
public sealed class BrownSplitsFactory : IComponentFactory
{
    public string ComponentName => "BrownSplits";

    public string Description => "Colors each completed split brown when its segment time is among the configured worst times.";

    public ComponentCategory Category => ComponentCategory.Other;

    public string UpdateName => ComponentName;

    public string XMLURL => string.Empty;

    public string UpdateURL => string.Empty;

    public Version Version => typeof(BrownSplitsFactory).Assembly.GetName().Version;

    public IComponent Create(LiveSplitState state) => new BrownSplitsComponent(state);
}
