using System.Reflection;
using LiveSplit.UI.Components;
using Xunit;

namespace BrownSplits.Tests;

// Verifies that LiveSplit can discover the plugin's public component factory.
public sealed class AssemblyRegistrationTests
{
    [Fact]
    public void RegistersThePublicComponentFactory()
    {
        ComponentFactoryAttribute? registration =
            typeof(BrownSplitsFactory).Assembly.GetCustomAttribute<ComponentFactoryAttribute>();

        Assert.NotNull(registration);
        Assert.Equal(typeof(BrownSplitsFactory), registration!.ComponentFactoryClassType);
    }
}
