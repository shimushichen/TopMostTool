namespace TopMostTool;

internal sealed class HotKeySettingsForm : Form
{
    private readonly HotKeyEditor _windowEditor;
    private readonly HotKeyEditor _screenshotEditor;
    private readonly Label _status;
    private readonly Button _save;
    private readonly Func<HotKeyBinding, HotKeyBinding, string?>? _apply;
    private TableLayoutPanel? _contentLayout;
    private bool _adjustingContentSize;
    internal HotKeyBinding Binding => _windowEditor.Binding;
    internal HotKeyBinding ScreenshotBinding => _screenshotEditor.Binding;

    internal HotKeySettingsForm(HotKeyBinding current, HotKeyBinding screenshot,
        Func<HotKeyBinding, HotKeyBinding, string?>? apply = null)
    {
        _apply = apply;
        Text = "快捷键设置 · 窗口置顶";
        UiTheme.StyleForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(520, 0);
        var layout = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 7,
            Padding = new Padding(16), BackColor = UiTheme.Background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var heading = UiTheme.Label("快捷键设置", heading: true);
        heading.Font = UiTheme.HeadingFont;
        layout.Controls.Add(heading);
        layout.Controls.Add(UiTheme.Label("点击输入框后按下组合键，保存后生效。", muted: true));
        _windowEditor = new HotKeyEditor("切换窗口置顶", current) { Dock = DockStyle.Top };
        _screenshotEditor = new HotKeyEditor("截图贴图", screenshot) { Dock = DockStyle.Top };
        layout.Controls.Add(_windowEditor);
        layout.Controls.Add(_screenshotEditor);
        layout.Controls.Add(UiTheme.Label("取消当前窗口置顶：Ctrl + Shift + Space（固定）", muted: true));
        _status = UiTheme.Label("", muted: true);
        _status.AccessibleName = "快捷键校验状态";
        _status.Dock = DockStyle.Top;
        _status.AutoSize = true;
        _status.MaximumSize = new Size(480, 0);
        _status.MinimumSize = Size.Empty;
        _status.Margin = new Padding(0, 4, 0, 8);
        layout.Controls.Add(_status);
        var actions = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0)
        };
        actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var reset = new RoundedButton { Text = "恢复默认", Anchor = AnchorStyles.Left, AccessibleName = "恢复默认快捷键" };
        var cancel = new RoundedButton { Text = "取消", DialogResult = DialogResult.Cancel };
        var save = new RoundedButton { Text = "保存", AccessibleName = "保存快捷键" };
        _save = save;
        UiTheme.StyleButton(reset);
        UiTheme.StyleButton(cancel);
        UiTheme.StyleButton(save, primary: true);
        reset.Click += (_, _) =>
        {
            _windowEditor.SetBinding(HotKeyBinding.Default);
            _screenshotEditor.SetBinding(HotKeyBinding.ScreenshotDefault);
            RefreshValidation();
        };
        _save.Click += (_, _) => SaveBindings();
        actions.Controls.Add(reset, 0, 0);
        actions.Controls.Add(cancel, 1, 0);
        actions.Controls.Add(_save, 2, 0);
        void RefreshActionHeight()
        {
            int preferredHeight = new[] { reset, cancel, _save }
                .Max(button => button.PreferredSize.Height + button.Margin.Vertical);
            actions.MinimumSize = new Size(0, preferredHeight);
        }
        reset.FontChanged += (_, _) => RefreshActionHeight();
        cancel.FontChanged += (_, _) => RefreshActionHeight();
        _save.FontChanged += (_, _) => RefreshActionHeight();
        RefreshActionHeight();
        layout.Controls.Add(actions);
        for (int row = 0; row < layout.RowCount; row++)
        {
            if (row < layout.RowStyles.Count)
                layout.RowStyles[row].SizeType = SizeType.AutoSize;
            else
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        _contentLayout = layout;
        Controls.Add(layout);
        AcceptButton = _save;
        CancelButton = cancel;
        _windowEditor.BindingChanged += (_, _) => RefreshValidation();
        _screenshotEditor.BindingChanged += (_, _) => RefreshValidation();
        RefreshValidation();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_contentLayout is null || _adjustingContentSize || !IsHandleCreated) return;
        int preferredWidth = _contentLayout.PreferredSize.Width;
        int nonClientWidth = Math.Max(0, Width - ClientSize.Width);
        int requiredWidth = preferredWidth + nonClientWidth;
        if (requiredWidth <= MinimumSize.Width) return;
        int workAreaWidth = Screen.FromControl(this).WorkingArea.Width;
        if (requiredWidth > workAreaWidth - 24) return;
        _adjustingContentSize = true;
        try
        {
            MinimumSize = new Size(requiredWidth, MinimumSize.Height);
            Width = Math.Max(Width, requiredWidth);
        }
        finally
        {
            _adjustingContentSize = false;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // A settings dialog should open as a review surface.  Put focus on the
        // primary action instead of silently arming the first key recorder.
        BeginInvoke((MethodInvoker)(() =>
        {
            if (IsDisposed) return;
            _save.Focus();
            _windowEditor.ClearInputSelection();
            _screenshotEditor.ClearInputSelection();
        }));
    }

    private void RefreshValidation()
    {
        string? error = ValidateBindings(Binding, ScreenshotBinding);
        _status.ForeColor = error is null ? UiTheme.MutedText : UiTheme.Error;
        _status.Text = error ?? "组合有效，点击“保存”应用更改。";
        _save.Enabled = error is null;
        _windowEditor.SetInvalid(error is not null);
        _screenshotEditor.SetInvalid(error is not null);
    }

    private void SaveBindings()
    {
        string? error = ValidateBindings(Binding, ScreenshotBinding) ?? _apply?.Invoke(Binding, ScreenshotBinding);
        if (error is not null)
        {
            _status.ForeColor = UiTheme.Error;
            _status.Text = "保存失败：" + error + "\n原有快捷键继续生效。";
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    internal static string? ValidateBindings(HotKeyBinding window, HotKeyBinding screenshot)
    {
        if (!window.IsValid || !screenshot.IsValid)
            return "请选择一个普通按键，并勾选至少一个修饰键（Ctrl、Alt、Shift 或 Win）。";
        if (window.ConflictsWithCancel || screenshot.ConflictsWithCancel)
            return "Ctrl + Shift + Space 已用于取消当前窗口置顶，请使用其他组合。";
        if (window == screenshot)
            return "窗口置顶与截图贴图的快捷键不能相同，请修改其中一项。";
        return null;
    }
}

internal sealed class HotKeyEditor : UserControl
{
    private TableLayoutPanel? _contentLayout;
    private readonly TextBox _keyTextBox;
    private readonly CheckBox[] _modifiers;
    private readonly Label _hint;
    private bool _updating;
    private bool _invalid;
    private bool _capturing;
    private HotKeyBinding _bindingBeforeCapture;
    private static readonly NativeMethods.HotKeyModifiers[] ModifierValues =
    {
        NativeMethods.HotKeyModifiers.Control, NativeMethods.HotKeyModifiers.Alt,
        NativeMethods.HotKeyModifiers.Shift, NativeMethods.HotKeyModifiers.Win
    };
    private uint _key;
    internal event EventHandler? BindingChanged;
    internal HotKeyBinding Binding => new()
    {
        Key = _key,
        Modifiers = _modifiers.Select((box, i) => box.Checked ? ModifierValues[i] : NativeMethods.HotKeyModifiers.None)
            .Aggregate(NativeMethods.HotKeyModifiers.None, (value, next) => value | next)
    };

    internal HotKeyEditor(string title, HotKeyBinding current)
    {
        AutoScaleMode = AutoScaleMode.Inherit;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        UiTheme.StyleCard(this);
        Font = UiTheme.MenuFont;
        Margin = new Padding(0, 6, 0, 6);
        var layout = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 5, Padding = new Padding(16, 14, 16, 14)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(UiTheme.Label(title, heading: true));
        layout.Controls.Add(UiTheme.Label("当前快捷键：" + current, muted: true));
        _keyTextBox = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BackColor = UiTheme.Background,
            ForeColor = UiTheme.Text, AccessibleName = title + "快捷键", BorderStyle = BorderStyle.FixedSingle,
            Font = UiTheme.EmphasisFont, Margin = new Padding(0, 2, 0, 8), ShortcutsEnabled = false
        };
        layout.Controls.Add(_keyTextBox);
        var modifiers = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = Padding.Empty };
        string[] names = { "Ctrl", "Alt", "Shift", "Win" };
        _modifiers = names.Select(name => new CheckBox
        {
            Text = name, AutoSize = true, AccessibleName = title + " " + name,
            Margin = new Padding(0, 0, 20, 6)
        }).ToArray();
        foreach (CheckBox modifier in _modifiers)
        {
            modifier.CheckedChanged += (_, _) =>
            {
                _keyTextBox.Text = Binding.ToString();
                if (!_updating) BindingChanged?.Invoke(this, EventArgs.Empty);
            };
            modifiers.Controls.Add(modifier);
        }
        layout.Controls.Add(modifiers);
        _hint = UiTheme.Label("点击输入框录入，也可勾选修饰键。", muted: true);
        layout.Controls.Add(_hint);
        for (int row = 0; row < layout.RowCount; row++)
        {
            if (row < layout.RowStyles.Count)
                layout.RowStyles[row].SizeType = SizeType.AutoSize;
            else
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        Controls.Add(layout);
        _contentLayout = layout;
        SetBinding(current);
        _bindingBeforeCapture = current;
        _keyTextBox.KeyDown += CaptureKey;
        _keyTextBox.Enter += (_, _) =>
        {
            _bindingBeforeCapture = Binding;
            _capturing = true;
            _hint.Text = "请按下组合键 · Tab 完成录入 · Esc 恢复原值";
            Invalidate();
        };
        _keyTextBox.Leave += (_, _) =>
        {
            _capturing = false;
            _hint.Text = "点击输入框录入，也可勾选修饰键。";
            Invalidate();
        };
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (_contentLayout is null) return base.GetPreferredSize(proposedSize);
        Size child = _contentLayout.GetPreferredSize(proposedSize);
        return new Size(Math.Max(child.Width, MinimumSize.Width), child.Height);
    }

    internal void SetInvalid(bool invalid) { _invalid = invalid; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(_invalid ? UiTheme.Error : ContainsFocus ? UiTheme.Accent : UiTheme.Border);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1,
            UiTheme.Scale(UiTheme.CornerRadius, DeviceDpi));
    }

    private void CaptureKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _capturing)
        {
            e.SuppressKeyPress = true;
            SetBinding(_bindingBeforeCapture);
            _keyTextBox.SelectionStart = 0;
            _keyTextBox.SelectionLength = 0;
            FindForm()?.SelectNextControl(_keyTextBox, true, true, true, true);
            return;
        }
        if (e.Modifiers == Keys.None && e.KeyCode is Keys.Tab or Keys.Enter) return;
        e.SuppressKeyPress = true;
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        var modifiers = NativeMethods.HotKeyModifiers.None;
        if (e.Control) modifiers |= NativeMethods.HotKeyModifiers.Control;
        if (e.Alt) modifiers |= NativeMethods.HotKeyModifiers.Alt;
        if (e.Shift) modifiers |= NativeMethods.HotKeyModifiers.Shift;
        if (GetKeyState((int)Keys.LWin) < 0 || GetKeyState((int)Keys.RWin) < 0)
            modifiers |= NativeMethods.HotKeyModifiers.Win;
        SetBinding(new HotKeyBinding { Key = (uint)e.KeyCode, Modifiers = modifiers });
    }

    internal void SetBinding(HotKeyBinding binding)
    {
        _updating = true;
        _key = binding.Key;
        for (int i = 0; i < _modifiers.Length; i++)
            _modifiers[i].Checked = binding.Modifiers.HasFlag(ModifierValues[i]);
        _keyTextBox.Text = binding.ToString();
        _keyTextBox.SelectionStart = 0;
        _keyTextBox.SelectionLength = 0;
        _updating = false;
        BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ClearInputSelection()
    {
        _keyTextBox.SelectionStart = 0;
        _keyTextBox.SelectionLength = 0;
        if (!_keyTextBox.Focused)
            _hint.Text = "点击输入框录入，也可勾选修饰键。";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_keyTextBox.Focused && (keyData & Keys.KeyCode) == Keys.Escape && _capturing)
        {
            CaptureKey(_keyTextBox, new KeyEventArgs(keyData));
            return true;
        }
        if (_keyTextBox.Focused && (keyData & Keys.Modifiers) != Keys.None &&
            (keyData & Keys.KeyCode) is Keys.Enter or Keys.Tab)
        {
            CaptureKey(_keyTextBox, new KeyEventArgs(keyData));
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int key);
}
