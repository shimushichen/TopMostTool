using System.Drawing.Drawing2D;

namespace TopMostTool;

internal sealed class HotKeySettingsForm : Form
{
    private readonly HotKeyEditor _windowEditor;
    private readonly HotKeyEditor _screenshotEditor;
    private readonly Label _heading;
    private readonly Label _subtitle;
    private readonly Panel _fixedCard;
    private readonly Label _fixedName;
    private readonly Label _fixedKey;
    private readonly Label _status;
    private readonly Label _statusMark;
    private readonly Panel _statusSurface;
    private readonly RoundedButton _save;
    private readonly RoundedButton _reset;
    private readonly RoundedButton _cancel;
    private readonly Func<HotKeyBinding, HotKeyBinding, string?>? _apply;
    private readonly Panel _content;
    private Bitmap? _fixedIconBitmap;
    private int _fixedIconDpi;
    private bool _fitting;
    private int _contentBottom;
    private bool _hasError;

    internal HotKeyBinding Binding => _windowEditor.Binding;
    internal HotKeyBinding ScreenshotBinding => _screenshotEditor.Binding;
    internal int ContentBottom => _contentBottom;

    internal HotKeySettingsForm(HotKeyBinding current, HotKeyBinding screenshot,
        Func<HotKeyBinding, HotKeyBinding, string?>? apply = null)
    {
        _apply = apply;
        Text = "快捷键设置";
        UiTheme.StyleForm(this);
        AutoScaleMode = AutoScaleMode.None;
        AutoSize = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;

        _heading = ManualLabel(UiTheme.Label("快捷键设置", heading: true));
        _heading.Font = UiTheme.HeadingFont;
        _heading.BackColor = Color.Transparent;

        _subtitle = ManualLabel(UiTheme.Label("点击快捷键框按下组合键，或点选下方修饰键，保存后立即生效。", muted: true));
        _subtitle.Font = UiTheme.CaptionFont;
        _subtitle.BackColor = Color.Transparent;

        _windowEditor = new HotKeyEditor(UiIcon.SelectWindow, "切换窗口置顶", current);
        _screenshotEditor = new HotKeyEditor(UiIcon.Screenshot, "截图贴图", screenshot);

        _fixedCard = new BufferedPanel { AutoSize = false, BackColor = UiTheme.Surface, Margin = Padding.Empty };
        _fixedCard.Paint += PaintFixedCard;
        _fixedName = ManualLabel(UiTheme.Label("取消当前窗口置顶"));
        _fixedName.BackColor = Color.Transparent;
        _fixedKey = ManualLabel(UiTheme.Label("Ctrl + Shift + Space（固定）", muted: true));
        _fixedKey.Font = UiTheme.CaptionFont;
        _fixedKey.TextAlign = ContentAlignment.MiddleRight;
        _fixedKey.BackColor = Color.Transparent;
        _fixedCard.Controls.Add(_fixedName);
        _fixedCard.Controls.Add(_fixedKey);

        _statusSurface = new BufferedPanel { BackColor = UiTheme.Background };
        _statusSurface.Paint += PaintStatusSurface;
        _statusMark = new Label
        {
            Text = "✓",
            AutoSize = false,
            Font = UiTheme.EmphasisFont,
            ForeColor = UiTheme.Success,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _status = ManualLabel(UiTheme.Label("", muted: true));
        _status.AccessibleName = "快捷键校验状态";
        _status.Font = UiTheme.CaptionFont;
        _status.BackColor = Color.Transparent;
        _statusSurface.Controls.Add(_statusMark);
        _statusSurface.Controls.Add(_status);

        _reset = new RoundedButton { Text = "恢复默认", AutoSize = false, AccessibleName = "恢复默认快捷键" };
        _cancel = new RoundedButton { Text = "取消", AutoSize = false, DialogResult = DialogResult.Cancel };
        _save = new RoundedButton { Text = "保存", AutoSize = false, AccessibleName = "保存快捷键" };
        UiTheme.StyleButton(_reset);
        UiTheme.StyleButton(_cancel);
        UiTheme.StyleButton(_save, primary: true);
        foreach (Button button in new[] { _reset, _cancel, _save })
        {
            button.AutoSize = false;
            button.MinimumSize = Size.Empty;
            button.Margin = Padding.Empty;
        }

        _content = new BufferedPanel
        {
            AutoSize = false,
            BackColor = UiTheme.Background,
            Margin = Padding.Empty
        };
        _content.Paint += PaintContentHeaderPill;
        _content.Controls.AddRange(new Control[]
        {
            _heading, _subtitle, _windowEditor, _screenshotEditor, _fixedCard,
            _statusSurface, _reset, _cancel, _save
        });
        Controls.Add(_content);
        AcceptButton = _save;
        CancelButton = _cancel;

        _reset.Click += (_, _) =>
        {
            _windowEditor.SetBinding(HotKeyBinding.Default);
            _screenshotEditor.SetBinding(HotKeyBinding.ScreenshotDefault);
            RefreshValidation();
        };
        _save.Click += (_, _) => SaveBindings();
        _windowEditor.BindingChanged += (_, _) => RefreshValidation();
        _screenshotEditor.BindingChanged += (_, _) => RefreshValidation();
        _windowEditor.ContentMetricsChanged += (_, _) => FitToContent();
        _screenshotEditor.ContentMetricsChanged += (_, _) => FitToContent();
        foreach (Control control in new Control[]
                 {
                     _heading, _subtitle, _fixedName, _fixedKey, _status, _statusMark,
                     _reset, _cancel, _save
                 })
            control.FontChanged += (_, _) => FitToContent();

        RefreshValidation();
        FitToContent();
    }

    private void PaintContentHeaderPill(object? sender, PaintEventArgs e)
    {
        if (_content.Width <= 4 || _heading.Height <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int dpi = DeviceDpi;
        using var pillFont = new Font(UiTheme.MenuFont.FontFamily, 8F);
        string text = _hasError ? "存在冲突" : "保存后生效";
        Size size = TextRenderer.MeasureText(e.Graphics, text, pillFont, Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        int padX = UiTheme.Scale(8, dpi);
        int pillW = size.Width + padX * 2;
        int pillH = size.Height + UiTheme.Scale(6, dpi);
        int pad = UiTheme.Scale(UiTheme.PagePadding, dpi);
        int pillX = _content.Width - pad - pillW;
        int pillY = _heading.Top + (_heading.Height - pillH) / 2;
        if (pillX < _heading.Left + TextSize(_heading).Width + UiTheme.Scale(12, dpi)) return;
        Color bg = _hasError ? UiTheme.ErrorSoft : UiTheme.SurfaceMuted;
        Color fg = _hasError ? UiTheme.Error : UiTheme.MutedText;
        using (var brush = new SolidBrush(bg))
            e.Graphics.FillRoundedRectangle(brush,
                _hasError ? pillX + 0.5F : pillX,
                _hasError ? pillY + 0.5F : pillY,
                _hasError ? pillW - 1 : pillW,
                _hasError ? pillH - 1 : pillH,
                pillH / 2F);
        if (_hasError)
        {
            using var pen = new Pen(UiTheme.ErrorBorder);
            e.Graphics.DrawRoundedRectangle(pen, pillX + 0.5F, pillY + 0.5F, pillW - 1, pillH - 1, pillH / 2F);
        }
        TextRenderer.DrawText(e.Graphics, text, pillFont, new Rectangle(pillX, pillY, pillW, pillH), fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    private void PaintFixedCard(object? sender, PaintEventArgs e)
    {
        if (_fixedCard.Width <= 2 || _fixedCard.Height <= 2) return;
        e.Graphics.Clear(UiTheme.Background);
        int dpi = _fixedCard.DeviceDpi;
        int radius = UiTheme.Scale(11, dpi);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(UiTheme.Surface))
            e.Graphics.FillRoundedRectangle(bg, 0.5F, 0.5F, _fixedCard.Width - 1, _fixedCard.Height - 1, radius);
        using (var border = new Pen(UiTheme.BorderSubtle))
            e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, _fixedCard.Width - 1, _fixedCard.Height - 1, radius);

        // Matching squircle icon well on the left of the fixed shortcut card
        int padX = UiTheme.Scale(14, dpi);
        int well = UiTheme.Scale(26, dpi);
        int wellY = (_fixedCard.Height - well) / 2;
        using (var wellBrush = new SolidBrush(UiTheme.Background))
            e.Graphics.FillRoundedRectangle(wellBrush, padX, wellY, well, well, UiTheme.Scale(7, dpi));
        int iconSize = UiTheme.Scale(15, dpi);
        if (_fixedIconBitmap is null || _fixedIconDpi != dpi)
        {
            _fixedIconBitmap?.Dispose();
            _fixedIconBitmap = UiIconFactory.Create(UiIcon.UnpinWindow, iconSize, UiTheme.MutedText);
            _fixedIconDpi = dpi;
        }
        e.Graphics.DrawImageUnscaled(_fixedIconBitmap,
            padX + (well - _fixedIconBitmap.Width) / 2,
            wellY + (well - _fixedIconBitmap.Height) / 2);
    }

    private void PaintStatusSurface(object? sender, PaintEventArgs e)
    {
        if (_statusSurface.Width <= 1 || _statusSurface.Height <= 1) return;
        e.Graphics.Clear(UiTheme.Background);
        if (!_hasError) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int radius = UiTheme.Scale(10, _statusSurface.DeviceDpi);
        using (var brush = new SolidBrush(UiTheme.ErrorSoft))
            e.Graphics.FillRoundedRectangle(brush, 0.5F, 0.5F, _statusSurface.Width - 1, _statusSurface.Height - 1, radius);
        using (var pen = new Pen(UiTheme.ErrorBorder))
            e.Graphics.DrawRoundedRectangle(pen, 0.5F, 0.5F, _statusSurface.Width - 1, _statusSurface.Height - 1, radius);
    }

    private static Label ManualLabel(Label label)
    {
        label.Dock = DockStyle.None;
        label.AutoSize = false;
        label.Margin = Padding.Empty;
        return label;
    }

    private static Size TextSize(Control control, string? text = null, int width = 0,
        bool multiline = false)
    {
        TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        flags |= multiline ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine;
        int slack = UiTheme.Scale(4, control.DeviceDpi);
        int measureWidth = width > 0 ? Math.Max(1, width - slack) : int.MaxValue;
        Size measured = TextRenderer.MeasureText(text ?? control.Text, control.Font,
            new Size(measureWidth, int.MaxValue), flags);
        int finalWidth = width > 0 ? width : measured.Width + slack;
        int finalHeight = measured.Height;
        if (text is null && control is Label label)
        {
            Size preferred = label.GetPreferredSize(width > 0 ? new Size(finalWidth, 0) : Size.Empty);
            finalHeight = Math.Max(finalHeight, preferred.Height);
            if (width <= 0) finalWidth = Math.Max(finalWidth, preferred.Width);
        }
        return new Size(finalWidth, finalHeight);
    }

    private int Scaled(int value) => UiTheme.Scale(value, DeviceDpi);

    private static Rectangle WorkingArea(Control control)
    {
        Rectangle area = Screen.FromControl(control).WorkingArea;
        return area.Width > 0 && area.Height > 0 ? area : new Rectangle(0, 0, 1920, 1040);
    }

    private Size ButtonSize(Button button)
    {
        Size text = TextSize(button);
        return new Size(Math.Max(Scaled(82), text.Width + Scaled(28)),
            Math.Max(Scaled(UiTheme.ButtonHeight), button.Font.Height + Scaled(12)));
    }

    private int PreferredContentWidth()
    {
        int width = Math.Max(Scaled(504) - Scaled(UiTheme.PagePadding) * 2,
            Math.Max(TextSize(_heading).Width + Scaled(100), TextSize(_subtitle).Width));
        width = Math.Max(width, TextSize(_fixedName).Width + TextSize(_fixedKey).Width + Scaled(84));
        width = Math.Max(width, _windowEditor.MinimumWidth);
        width = Math.Max(width, _screenshotEditor.MinimumWidth);
        width = Math.Max(width, TextSize(_status, "组合有效，点击“保存”应用更改。").Width + Scaled(50));
        Size reset = ButtonSize(_reset), cancel = ButtonSize(_cancel), save = ButtonSize(_save);
        width = Math.Max(width, reset.Width + cancel.Width + save.Width + Scaled(40));
        return width;
    }

    private int LayoutContent(int width, bool apply)
    {
        int pad = Scaled(UiTheme.PagePadding);
        int contentWidth = Math.Max(1, width - pad * 2);
        int y = Scaled(16);

        Size heading = TextSize(_heading, width: contentWidth, multiline: true);
        Size subtitle = TextSize(_subtitle, width: contentWidth, multiline: true);
        if (apply)
        {
            _heading.SetBounds(pad, y, contentWidth, heading.Height);
            _subtitle.SetBounds(pad, _heading.Bottom + Scaled(4), contentWidth, subtitle.Height);
        }
        y += heading.Height + Scaled(4) + subtitle.Height + Scaled(14);

        int windowHeight = _windowEditor.MeasureHeight(contentWidth);
        int screenshotHeight = _screenshotEditor.MeasureHeight(contentWidth);
        if (apply)
        {
            _windowEditor.SetBounds(pad, y, contentWidth, windowHeight);
            _screenshotEditor.SetBounds(pad, y + windowHeight + Scaled(12), contentWidth, screenshotHeight);
        }
        y += windowHeight + Scaled(12) + screenshotHeight + Scaled(12);

        // Fixed shortcut white card with squircle icon well
        int fixedIconLeft = Scaled(14) + Scaled(26) + Scaled(10);
        int fixedPadRight = Scaled(16);
        int fixedPadY = Scaled(11);
        int fixedInnerWidth = Math.Max(1, contentWidth - fixedIconLeft - fixedPadRight);
        Size fixedNameSingle = TextSize(_fixedName);
        Size fixedKeySingle = TextSize(_fixedKey);
        bool stackFixed = fixedNameSingle.Width + fixedKeySingle.Width + Scaled(12) > fixedInnerWidth;
        int fixedHeight;
        if (stackFixed)
        {
            Size fn = TextSize(_fixedName, width: fixedInnerWidth, multiline: true);
            Size fk = TextSize(_fixedKey, width: fixedInnerWidth, multiline: true);
            fixedHeight = Math.Max(Scaled(44), fixedPadY * 2 + fn.Height + Scaled(3) + fk.Height);
            if (apply)
            {
                _fixedKey.TextAlign = ContentAlignment.MiddleLeft;
                _fixedCard.SetBounds(pad, y, contentWidth, fixedHeight);
                _fixedName.SetBounds(fixedIconLeft, fixedPadY, fixedInnerWidth, fn.Height);
                _fixedKey.SetBounds(fixedIconLeft, fixedPadY + fn.Height + Scaled(3), fixedInnerWidth, fk.Height);
            }
        }
        else
        {
            int keyW = fixedKeySingle.Width;
            int nameW = Math.Max(1, fixedInnerWidth - keyW - Scaled(12));
            Size fn = TextSize(_fixedName, width: nameW, multiline: true);
            Size fk = TextSize(_fixedKey, width: keyW, multiline: true);
            int rowH = Math.Max(fn.Height, fk.Height);
            fixedHeight = Math.Max(Scaled(44), fixedPadY * 2 + rowH);
            if (apply)
            {
                _fixedKey.TextAlign = ContentAlignment.MiddleRight;
                _fixedCard.SetBounds(pad, y, contentWidth, fixedHeight);
                _fixedName.SetBounds(fixedIconLeft, (fixedHeight - fn.Height) / 2, nameW, fn.Height);
                _fixedKey.SetBounds(contentWidth - fixedPadRight - keyW, (fixedHeight - fk.Height) / 2, keyW, fk.Height);
            }
        }
        y += fixedHeight + Scaled(10);

        int statusPadX = _hasError ? Scaled(12) : Scaled(4);
        int statusPadY = _hasError ? Scaled(9) : Scaled(4);
        int statusGap = Scaled(6);
        int markWidth = Scaled(20);
        int statusTextWidth = Math.Max(1, contentWidth - statusPadX * 2 - markWidth - statusGap);
        Size statusText = TextSize(_status, width: statusTextWidth, multiline: true);
        int statusMarkHeight = TextSize(_statusMark).Height;
        int statusContentHeight = Math.Max(statusText.Height, statusMarkHeight);
        int statusHeight = statusContentHeight + statusPadY * 2;
        if (apply)
        {
            _statusSurface.SetBounds(pad, y, contentWidth, statusHeight);
            _statusMark.SetBounds(statusPadX, (statusHeight - statusContentHeight) / 2, markWidth, statusContentHeight);
            _status.SetBounds(statusPadX + markWidth + statusGap,
                (statusHeight - statusText.Height) / 2,
                statusTextWidth, statusText.Height);
        }
        y += statusHeight + Scaled(14);

        Size reset = ButtonSize(_reset), cancel = ButtonSize(_cancel), save = ButtonSize(_save);
        int actionHeight = Math.Max(reset.Height, Math.Max(cancel.Height, save.Height));
        if (apply)
        {
            _reset.SetBounds(pad, y, reset.Width, actionHeight);
            _save.SetBounds(width - pad - save.Width, y, save.Width, actionHeight);
            _cancel.SetBounds(_save.Left - Scaled(8) - cancel.Width, y, cancel.Width, actionHeight);
        }
        return y + actionHeight + Scaled(20);
    }

    private void FitToContent()
    {
        if (_fitting || IsDisposed) return;
        _fitting = true;
        try
        {
            Rectangle workArea = WorkingArea(this);
            int pad = Scaled(UiTheme.PagePadding);
            int maximumWidth = Math.Max(Scaled(360), workArea.Width - Scaled(32));
            int width = Math.Min(maximumWidth, PreferredContentWidth() + pad * 2);
            int layoutWidth = width;
            int contentHeight = LayoutContent(layoutWidth, apply: false);
            int maximumHeight = Math.Max(Scaled(360), workArea.Height - Scaled(32));
            bool scroll = contentHeight > maximumHeight;
            if (scroll)
            {
                layoutWidth = Math.Max(Scaled(320), layoutWidth - SystemInformation.VerticalScrollBarWidth);
                contentHeight = LayoutContent(layoutWidth, apply: false);
            }
            AutoScroll = scroll;
            AutoScrollMinSize = scroll ? new Size(0, contentHeight) : Size.Empty;
            _content.SetBounds(0, 0, layoutWidth, contentHeight);
            ClientSize = new Size(width, Math.Min(contentHeight, maximumHeight));
            _contentBottom = LayoutContent(layoutWidth, apply: true);
            PerformLayout();
            Invalidate(true);
        }
        finally
        {
            _fitting = false;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToContent();
        BeginInvoke((MethodInvoker)(() =>
        {
            if (IsDisposed) return;
            _save.Focus();
            _windowEditor.ClearInputSelection();
            _screenshotEditor.ClearInputSelection();
        }));
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        FitToContent();
    }

    private void SetStatus(string text, bool error)
    {
        _hasError = error;
        _status.ForeColor = error ? UiTheme.Error : UiTheme.MutedText;
        _status.Text = text;
        _statusMark.ForeColor = error ? UiTheme.Error : UiTheme.Success;
        _statusMark.Text = error ? "!" : "✓";
        _statusSurface.BackColor = error ? UiTheme.ErrorSoft : UiTheme.Background;
        _statusSurface.Invalidate(true);
        _content.Invalidate();
        FitToContent();
    }

    private void RefreshValidation()
    {
        string? error = ValidateBindings(Binding, ScreenshotBinding);
        SetStatus(error ?? "组合有效，点击“保存”应用更改。", error is not null);
        _save.Enabled = error is null;
        _windowEditor.SetInvalid(error is not null);
        _screenshotEditor.SetInvalid(error is not null);
    }

    private void SaveBindings()
    {
        string? error = ValidateBindings(Binding, ScreenshotBinding) ?? _apply?.Invoke(Binding, ScreenshotBinding);
        if (error is not null)
        {
            const string retained = "原有快捷键继续生效。";
            string message = "保存失败：" + error;
            if (!error.Contains(retained, StringComparison.Ordinal)) message += "\n" + retained;
            SetStatus(message, error: true);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fixedIconBitmap?.Dispose();
        base.Dispose(disposing);
    }

    private sealed class BufferedPanel : Panel
    {
        internal BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}

internal sealed class HotKeyEditor : UserControl
{
    private const string IdleHint = "点击输入框录入，也可勾选修饰键。";
    private readonly UiIcon _icon;
    private readonly Label _titleLabel;
    private readonly Label _currentLabel;
    private readonly TextBox _keyTextBox;
    private readonly HotKeyInputSurface _inputSurface;
    private readonly CheckBox[] _modifiers;
    private readonly Label _hint;
    private Bitmap? _iconBitmap;
    private int _iconDpi;
    private int _iconTop;
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
    internal event EventHandler? ContentMetricsChanged;
    internal HotKeyBinding Binding => new()
    {
        Key = _key,
        Modifiers = _modifiers.Select((box, i) => box.Checked ? ModifierValues[i] : NativeMethods.HotKeyModifiers.None)
            .Aggregate(NativeMethods.HotKeyModifiers.None, (value, next) => value | next)
    };

    internal HotKeyEditor(UiIcon icon, string title, HotKeyBinding current)
    {
        _icon = icon;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoScaleMode = AutoScaleMode.None;
        AutoSize = false;
        BackColor = UiTheme.Surface;
        Font = UiTheme.MenuFont;
        Margin = Padding.Empty;

        _titleLabel = ManualLabel(UiTheme.Label(title));
        _titleLabel.BackColor = Color.Transparent;

        _currentLabel = ManualLabel(UiTheme.Label("当前：" + current, muted: true));
        _currentLabel.Font = UiTheme.CaptionFont;
        _currentLabel.TextAlign = ContentAlignment.MiddleRight;
        _currentLabel.BackColor = Color.Transparent;

        _inputSurface = new HotKeyInputSurface();
        _keyTextBox = new TextBox
        {
            AutoSize = false,
            ReadOnly = true,
            BackColor = UiTheme.Input,
            ForeColor = UiTheme.Text,
            AccessibleName = title + "快捷键",
            BorderStyle = BorderStyle.None,
            Font = UiTheme.EmphasisFont,
            Margin = Padding.Empty,
            ShortcutsEnabled = false,
            TextAlign = HorizontalAlignment.Center
        };
        _inputSurface.Controls.Add(_keyTextBox);

        string[] names = { "Ctrl", "Alt", "Shift", "Win" };
        _modifiers = names.Select(name =>
        {
            var box = new ModifierKeyCap
            {
                Text = name,
                AccessibleName = title + " " + name,
                Font = UiTheme.CaptionFont
            };
            UiTheme.StyleModifier(box);
            return box;
        }).Cast<CheckBox>().ToArray();
        foreach (CheckBox modifier in _modifiers)
        {
            modifier.CheckedChanged += (_, _) =>
            {
                _keyTextBox.Text = Binding.ToString();
                if (!_updating) BindingChanged?.Invoke(this, EventArgs.Empty);
            };
            modifier.FontChanged += (_, _) => NotifyMetricsChanged();
        }
        _hint = ManualLabel(UiTheme.Label(IdleHint, muted: true));
        _hint.Font = UiTheme.CaptionFont;
        _hint.BackColor = Color.Transparent;
        _hint.TextChanged += (_, _) => NotifyMetricsChanged();

        Controls.AddRange(new Control[]
        {
            _titleLabel, _currentLabel, _inputSurface, _hint
        });
        Controls.AddRange(_modifiers);
        foreach (Control control in new Control[] { _titleLabel, _currentLabel, _keyTextBox })
            control.FontChanged += (_, _) => NotifyMetricsChanged();

        Height = MeasureHeight(MinimumWidth);
        LayoutEditor();
        SetBinding(current);
        _bindingBeforeCapture = current;
        _keyTextBox.KeyDown += CaptureKey;
        _keyTextBox.Enter += (_, _) =>
        {
            _bindingBeforeCapture = Binding;
            _capturing = true;
            _inputSurface.Capturing = true;
            _keyTextBox.BackColor = UiTheme.Surface;
            _hint.Text = "正在录入 · Tab 完成 · Esc 恢复原值";
            _hint.ForeColor = UiTheme.Text;
            Invalidate();
            _inputSurface.Invalidate();
        };
        _keyTextBox.Leave += (_, _) =>
        {
            _capturing = false;
            _inputSurface.Capturing = false;
            _keyTextBox.BackColor = UiTheme.Input;
            _hint.Text = IdleHint;
            _hint.ForeColor = UiTheme.MutedText;
            Invalidate();
            _inputSurface.Invalidate();
        };
    }

    private static Label ManualLabel(Label label)
    {
        label.Dock = DockStyle.None;
        label.AutoSize = false;
        label.Margin = Padding.Empty;
        return label;
    }

    private static Size TextSize(Control control, int width = 0)
    {
        int slack = UiTheme.Scale(4, control.DeviceDpi);
        int measureWidth = width > 0 ? Math.Max(1, width - slack) : int.MaxValue;
        Size measured = TextRenderer.MeasureText(control.Text, control.Font,
            new Size(measureWidth, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        int finalWidth = width > 0 ? width : measured.Width + slack;
        int finalHeight = measured.Height;
        if (control is Label label)
        {
            Size preferred = label.GetPreferredSize(width > 0 ? new Size(finalWidth, 0) : Size.Empty);
            finalHeight = Math.Max(finalHeight, preferred.Height);
            if (width <= 0) finalWidth = Math.Max(finalWidth, preferred.Width);
        }
        return new Size(finalWidth, finalHeight);
    }

    private int Scaled(int value) => UiTheme.Scale(value, DeviceDpi);
    private int WellSize => Scaled(26);
    private int InputHeight => Math.Max(Scaled(38), _keyTextBox.Font.Height + Scaled(14));
    private int ModifierHeight => _modifiers.Max(modifier => modifier.GetPreferredSize(Size.Empty).Height);

    internal int MinimumWidth
    {
        get
        {
            int pad = Scaled(16);
            int modifierWidth = _modifiers.Max(modifier => modifier.GetPreferredSize(Size.Empty).Width);
            int headerInline = WellSize + Scaled(10) + TextSize(_titleLabel).Width + TextSize(_currentLabel).Width + Scaled(20);
            int content = Math.Max(headerInline, TextSize(_hint).Width);
            content = Math.Max(content, modifierWidth * _modifiers.Length + Scaled(8) * (_modifiers.Length - 1));
            content = Math.Max(content, TextSize(_keyTextBox).Width + Scaled(28));
            return content + pad * 2;
        }
    }

    internal int MeasureHeight(int totalWidth)
    {
        int pad = Scaled(16);
        int contentWidth = Math.Max(1, totalWidth - pad * 2);
        int headerLeftOffset = WellSize + Scaled(10);
        int headerAvail = Math.Max(1, contentWidth - headerLeftOffset);
        Size titleSingle = TextSize(_titleLabel);
        Size currentSingle = TextSize(_currentLabel);
        bool stackHeader = titleSingle.Width + currentSingle.Width + Scaled(12) > headerAvail;
        int headerH = stackHeader
            ? Math.Max(WellSize, TextSize(_titleLabel, headerAvail).Height + Scaled(2) + TextSize(_currentLabel, headerAvail).Height)
            : Math.Max(WellSize, Math.Max(titleSingle.Height, currentSingle.Height));
        return Scaled(14) + headerH + Scaled(10) + InputHeight + Scaled(10) +
            ModifierHeight + Scaled(8) + TextSize(_hint, contentWidth).Height + Scaled(14);
    }

    private void NotifyMetricsChanged()
    {
        LayoutEditor();
        ContentMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LayoutEditor()
    {
        if (_titleLabel is null || Width <= 0) return;
        int pad = Scaled(16);
        int contentWidth = Math.Max(1, Width - pad * 2);
        int y = Scaled(14);
        int well = WellSize;
        int textStart = pad + well + Scaled(10);
        int headerAvail = Math.Max(1, contentWidth - well - Scaled(10));

        Size titleSingle = TextSize(_titleLabel);
        Size currentSingle = TextSize(_currentLabel);
        bool stackHeader = titleSingle.Width + currentSingle.Width + Scaled(12) > headerAvail;
        if (stackHeader)
        {
            _currentLabel.TextAlign = ContentAlignment.MiddleLeft;
            int th = TextSize(_titleLabel, headerAvail).Height;
            int ch = TextSize(_currentLabel, headerAvail).Height;
            int blockH = th + Scaled(2) + ch;
            int rowH = Math.Max(well, blockH);
            _iconTop = y + (rowH - well) / 2;
            _titleLabel.SetBounds(textStart, y + (rowH - blockH) / 2, headerAvail, th);
            _currentLabel.SetBounds(textStart, _titleLabel.Bottom + Scaled(2), headerAvail, ch);
            y += rowH + Scaled(10);
        }
        else
        {
            _currentLabel.TextAlign = ContentAlignment.MiddleRight;
            int currentW = currentSingle.Width;
            int titleW = Math.Max(1, headerAvail - currentW - Scaled(12));
            int rowH = Math.Max(well, Math.Max(titleSingle.Height, currentSingle.Height));
            _iconTop = y + (rowH - well) / 2;
            _titleLabel.SetBounds(textStart, y + (rowH - titleSingle.Height) / 2, titleW, titleSingle.Height);
            _currentLabel.SetBounds(Width - pad - currentW, y + (rowH - currentSingle.Height) / 2, currentW, currentSingle.Height);
            y += rowH + Scaled(10);
        }

        _inputSurface.SetBounds(pad, y, contentWidth, InputHeight);
        int textHeight = Math.Max(_keyTextBox.Font.Height + Scaled(4), Scaled(22));
        int textWidth = Math.Max(1, contentWidth - Scaled(24));
        _keyTextBox.SetBounds(Scaled(12), (_inputSurface.Height - textHeight) / 2, textWidth, textHeight);
        y = _inputSurface.Bottom + Scaled(10);

        int modifierHeight = ModifierHeight;
        int modifierWidth = _modifiers.Max(modifier => modifier.GetPreferredSize(Size.Empty).Width);
        for (int i = 0; i < _modifiers.Length; i++)
            _modifiers[i].SetBounds(pad + i * (modifierWidth + Scaled(8)), y, modifierWidth, modifierHeight);
        y += modifierHeight + Scaled(8);
        _hint.SetBounds(pad, y, contentWidth, TextSize(_hint, contentWidth).Height);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutEditor();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        NotifyMetricsChanged();
    }

    internal void SetInvalid(bool invalid)
    {
        _invalid = invalid;
        _inputSurface.Invalid = invalid;
        Invalidate();
        _inputSurface.Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        OnPaintBackground(e);
        if (Width <= 1 || Height <= 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int dpi = DeviceDpi;
        int radius = UiTheme.Scale(12, dpi);
        using (var fill = new SolidBrush(UiTheme.Surface))
            e.Graphics.FillRoundedRectangle(fill, 0.5F, 0.5F, Width - 1, Height - 1, radius);
        Color borderColor = _invalid ? UiTheme.ErrorBorder : _capturing ? UiTheme.Accent : UiTheme.BorderSubtle;
        using (var border = new Pen(borderColor))
            e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1, radius);

        // Squircle icon well matching TrayPopoverForm's ActionTile & AboutForm's FeatureTile
        int pad = UiTheme.Scale(16, dpi);
        int well = WellSize;
        using (var wellBrush = new SolidBrush(UiTheme.Background))
            e.Graphics.FillRoundedRectangle(wellBrush, pad, _iconTop, well, well, UiTheme.Scale(7, dpi));
        int iconSize = UiTheme.Scale(15, dpi);
        if (_iconBitmap is null || _iconDpi != dpi)
        {
            _iconBitmap?.Dispose();
            _iconBitmap = UiIconFactory.Create(_icon, iconSize, UiTheme.Text);
            _iconDpi = dpi;
        }
        e.Graphics.DrawImageUnscaled(_iconBitmap,
            pad + (well - _iconBitmap.Width) / 2,
            _iconTop + (well - _iconBitmap.Height) / 2);
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
        LayoutEditor();
        BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ClearInputSelection()
    {
        _keyTextBox.SelectionStart = 0;
        _keyTextBox.SelectionLength = 0;
        if (!_keyTextBox.Focused)
        {
            _hint.Text = IdleHint;
            _hint.ForeColor = UiTheme.MutedText;
        }
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

    protected override void Dispose(bool disposing)
    {
        if (disposing) _iconBitmap?.Dispose();
        base.Dispose(disposing);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int key);

    private sealed class HotKeyInputSurface : Panel
    {
        internal bool Invalid { get; set; }
        internal bool Capturing { get; set; }

        internal HotKeyInputSurface()
        {
            BackColor = UiTheme.Input;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface);
            if (Width <= 1 || Height <= 1) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool active = Capturing || ContainsFocus;
            Color wellFill = active ? UiTheme.Surface : UiTheme.Input;
            using var fill = new SolidBrush(wellFill);
            e.Graphics.FillRoundedRectangle(fill, 0.5F, 0.5F, Width - 1, Height - 1,
                UiTheme.Scale(8, DeviceDpi));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 1 || Height <= 1) return;
            int dpi = DeviceDpi;
            bool active = Capturing || ContainsFocus;
            Color borderColor = Invalid ? UiTheme.Error : active ? UiTheme.Accent : UiTheme.BorderSubtle;
            float stroke = active ? 1.5F : 1F;
            float inset = stroke / 2F;
            using var border = new Pen(borderColor, stroke);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRoundedRectangle(border, inset, inset, Width - stroke, Height - stroke,
                UiTheme.Scale(8, dpi));
        }
    }
}
