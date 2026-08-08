# BrownSplits

Have you ever had asplit where you thought to yourself: "This might be the worst I have ever performed in this section of the speedrun."? **BrownSplits** has you covered. It is a LiveSplit component that overrides the usual green, red, and gold comparison colors and adds brown splits for runs that truly deserve public shame.

## Possible Configurations

- **Percentile**: The plugin can be configured to override the split color when the current segment time is slower than a certain percentile of recorded times for that segment. For example, setting it to the 95th percentile means that if your current segment time is slower than 95% of your previous attempts, it will turn brown. This can also be turned off, so only the true worst splits will be brown.
- **Recent History**: You can choose to only consider the most recent `n` recorded times for the segment when determining if a split is brown. This allows you to focus on your recent performance rather than your entire history.
- **Custom Color**: If brown isn't your preferred color to show how shit you are, you can choose any RGB color.

## Installation

- Download the latest release from the Release page
- Go to your LiveSplit installation directory and open the `Components` folder.
- Copy the two DLLs, `BrownSplits.dll` and `0Harmony.dll`, into the `Components` folder.
- Start LiveSplit and Edit your layout.
- Add the **Other > BrownSplits** component to your layout.
- Configure the component settings to your liking.
- Enjoy your new shameful splits!

## How it works

- The plugin follows LiveSplit's active timing method (Real Time or Game Time).
- It reads only real attempts from the current segment's `SegmentHistory`; imported
  helper entries such as best-segment/PB history are not counted.
- The component intercepts LiveSplit's shared semantic color decision. While the
  current segment is running, standard live displays such as Timer, Delta, and Live
  Segment can turn brown immediately when the threshold is crossed. After splitting,
  only the qualifying completed row remains overridden; all other rows keep their
  normal colors.
- The layout's global colors are never modified.

The override applies everywhere that calls LiveSplit's shared `GetSplitColor`
helper, including standard split, delta, previous/live-segment, and timer components.
A component with its own enabled color override, or one that bypasses that helper,
cannot be affected by another plugin.