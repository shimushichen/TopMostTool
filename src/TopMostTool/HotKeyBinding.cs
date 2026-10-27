namespace TopMostTool;

internal sealed record HotKeyBinding
{
    internal static HotKeyBinding Default { get; } = new()
    {
        Modifiers = NativeMethods.HotKeyModifiers.Control,
        Key = (uint)Keys.Space
    };

    internal static HotKeyBinding ScreenshotDefault { get; } = new()
    {
        Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Shift,
        Key = (uint)Keys.S
    };

    public NativeMethods.HotKeyModifiers Modifiers { get; init; }
    public uint Key { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal bool IsValid =>
        Key is >= 0x08 and <= 0xFE &&
        (Keys)Key is not (Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) &&
        Modifiers != NativeMethods.HotKeyModifiers.None &&
        (Modifiers & ~(NativeMethods.HotKeyModifiers.Alt |
                       NativeMethods.HotKeyModifiers.Control |
                       NativeMethods.HotKeyModifiers.Shift |
                       NativeMethods.HotKeyModifiers.Win)) == 0;

    internal bool ConflictsWithCancel =>
        Modifiers == (NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Shift) &&
        Key == (uint)Keys.Space;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(NativeMethods.HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(NativeMethods.HotKeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(NativeMethods.HotKeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(NativeMethods.HotKeyModifiers.Win)) parts.Add("Win");
        parts.Add(GetFriendlyKeyName((Keys)Key));
        return string.Join(" + ", parts);
    }

    private static string GetFriendlyKeyName(Keys key) => key switch
    {
        Keys.Space => "Space",
        Keys.Oemtilde => "`",
        Keys.OemMinus => "-",
        Keys.Oemplus => "+",
        >= Keys.D0 and <= Keys.D9 => ((int)key - (int)Keys.D0).ToString(),
        _ => key.ToString()
    };
}
