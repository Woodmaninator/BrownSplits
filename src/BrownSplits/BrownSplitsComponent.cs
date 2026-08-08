using System;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace BrownSplits;

// LogicComponent is used for invisible LiveSplit components that don't render anything on the layout, but still need to run logic.
internal sealed class BrownSplitsComponent : LogicComponent
{
    private readonly BrownSplitsSettings settings = new();
    private readonly IDisposable runtimeRegistration;

    public BrownSplitsComponent(LiveSplitState state)
    {
        runtimeRegistration = BrownSplitsRuntime.Register(state, settings);
    }

    public override string ComponentName => "BrownSplits";

    public override Control GetSettingsControl(LayoutMode mode) => new BrownSplitsSettingsControl(settings);

    public override XmlNode GetSettings(XmlDocument document) => settings.ToXml(document);

    public override void SetSettings(XmlNode settingsNode) => settings.FromXml(settingsNode);

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        // LogicComponent requires an Update method, but BrownSplits works using the Harmony hook registered in the constructor. No polling is needed here.
    }

    public override void Dispose() => runtimeRegistration.Dispose();
}
