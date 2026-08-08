using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using HarmonyLib;
using LiveSplit.Model;

namespace BrownSplits;

// Installs the Harmony color hook and connects it to every active BrownSplits component.
internal static class BrownSplitsRuntime
{
    private const string HarmonyId = "BrownSplits.SplitColorOverride";

    // Harmony's postfix and LiveSplit's component lifecycle may enter this class
    // through different paths, so registration and patch state are kept together.
    private static readonly object Locker = new();
    private static readonly Dictionary<LiveSplitState, BrownSplitsStateRegistration> RegisteredStates = new();

    // Getting the method that is hooked into using reflection
    private static readonly MethodInfo SplitColorMethod = AccessTools.Method(
        typeof(LiveSplitStateHelper), // class name
        nameof(LiveSplitStateHelper.GetSplitColor), // method name
        // method signature (parameters)
        new[]
        {
            typeof(LiveSplitState),
            typeof(TimeSpan?),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(string),
            typeof(TimingMethod),
        });

    private static Harmony? harmony;
    private static int nextOwnerId;

    public static IDisposable Register(LiveSplitState state, BrownSplitsSettings settings)
    {
        lock (Locker)
        {
            EnsureHarmonyPatchIsInstalled();

            if (!RegisteredStates.TryGetValue(state, out BrownSplitsStateRegistration? registeredState))
            {
                registeredState = new BrownSplitsStateRegistration(state);
                RegisteredStates.Add(state, registeredState);
            }

            // A layout can technically contain BrownSplits more than once. Each component gets its own ID
            int ownerId = ++nextOwnerId;
            registeredState.AddOwner(ownerId, settings);

            return new BrownSplitsRegistrationToken(() => Unregister(state, ownerId));
        }
    }

    private static void EnsureHarmonyPatchIsInstalled()
    {
        if (harmony is not null)
        {
            return;
        }

        harmony = new Harmony(HarmonyId);

        // Postfix means that the BrownSplitsRuntime hook runs after LiveSplit's original GetSplitColor method, so it can override the color if needed.
        var postfix = new HarmonyMethod(
            typeof(BrownSplitsRuntime),
            nameof(AfterGetSplitColor));

        harmony.Patch(SplitColorMethod, postfix: postfix);
    }

    // Harmony calls this AFTER (postfix) every LiveSplitStateHelper.GetSplitColor invocation.
    // The __result parameter contains LiveSplit's original return value
    private static void AfterGetSplitColor(
        LiveSplitState state,
        int splitNumber,
        TimingMethod method,
        ref Color? __result)
    {
        // Null means that LiveSplit did not choose a semantic color for this value. In that case, LiveSplit's judgement is trusted.
        if (!__result.HasValue)
        {
            return;
        }

        lock (Locker)
        {
            if (RegisteredStates.TryGetValue(state, out BrownSplitsStateRegistration? registeredState)
                && registeredState.TryGetOverrideColor(splitNumber, method, out Color overrideColor))
            {
                __result = overrideColor;
            }
        }
    }

    private static void Unregister(LiveSplitState state, int ownerId)
    {
        lock (Locker)
        {
            if (!RegisteredStates.TryGetValue(state, out BrownSplitsStateRegistration? registeredState))
            {
                return;
            }

            registeredState.RemoveOwner(ownerId);
            if (registeredState.HasOwners)
            {
                return;
            }

            registeredState.Dispose();
            RegisteredStates.Remove(state);

            // Leaving patches installed after the last component is removed would make no sense
            if (RegisteredStates.Count == 0 && harmony is not null)
            {
                harmony.Unpatch(SplitColorMethod, HarmonyPatchType.Postfix, HarmonyId);
                harmony = null;
            }
        }
    }
}
