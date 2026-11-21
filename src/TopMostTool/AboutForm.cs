using System.Drawing.Drawing2D;

namespace TopMostTool;

internal sealed class AboutForm : Form
{
    private readonly Panel _content;
    private readonly Panel _heroCard;
    private readonly PictureBox _picture;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly FeatureTile[] _featureTiles;
    private readonly Panel _notesCard;
    private readonly Label[] _noteTitles;
    private readonly Label[] _noteBodies;
    private readonly Label _privacy;
    private readonly RoundedButton _close;
    private int _notesDividerY = -1;
    private bool _fitting;

    internal AboutForm()
    {
        Text = "关于";
        UiTheme.StyleForm(this);
        AutoScaleMode = AutoScaleMode.None;
        AutoSize = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;

        _content = new BufferedPanel { AutoSize = false, BackColor = UiTheme.Background };

        // 1. Brand Identity Card (unified with TrayPopoverForm card language + dark version pill)
        _heroCard = new BufferedPanel { AutoSize = false, BackColor = UiTheme.Surface, Margin = Padding.Empty };
        _heroCard.Paint += PaintHeroCard;

        _picture = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.CenterImage,
            AccessibleName = "程序图标",
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        _title = ManualLabel(UiTheme.Label("窗口置顶"));
        _title.Font = new Font(UiTheme.MenuFont.FontFamily, UiTheme.MenuFont.Size + 6F, FontStyle.Bold);
        _title.BackColor = Color.Transparent;

        _subtitle = ManualLabel(UiTheme.Label("轻量、安静的 Windows 11 窗口置顶与浮动贴图工具", muted: true));
        _subtitle.Font = UiTheme.CaptionFont;
        _subtitle.BackColor = Color.Transparent;
        _heroCard.Controls.AddRange(new Control[] { _picture, _title, _subtitle });

        // 2. 2x2 Feature Grid using the exact same squircle-icon white cards as TrayPopoverForm
        (UiIcon Icon, string Title, string Detail)[] features =
        {
            (UiIcon.SelectWindow, "窗口置顶", "Ctrl + Space 切换当前窗口，或从托盘控制中心一键点选。"),
            (UiIcon.Screenshot, "截图贴图", "Ctrl + Shift + S 框选任意区域，松开生成置顶浮动贴图。"),
            (UiIcon.Settings, "缩放与保存", "拖动移动、滚轮缩放，顶栏一键复制原图或保存为 PNG。"),
            (UiIcon.CloseScreenshots, "集中管理", "托盘浮窗实时查看已置顶窗口与贴图数量，一键清理。")
        };
        _featureTiles = features.Select(f => new FeatureTile(f.Icon, f.Title, f.Detail)).ToArray();

        // 3. Grouped Notes Card (same inset-hairline card style as TrayPopoverForm)
        _notesCard = new BufferedPanel { AutoSize = false, BackColor = UiTheme.Surface, Margin = Padding.Empty };
        _notesCard.Paint += PaintNotesCard;
        string[] noteTitleTexts = { "内存运行", "退出恢复" };
        string[] noteBodyTexts =
        {
            "贴图仅保留在内存中，关闭或退出前请先保存需要的图片。",
            "退出时自动取消本工具设置的窗口置顶；快捷键可在托盘修改。"
        };
        _noteTitles = new Label[noteTitleTexts.Length];
        _noteBodies = new Label[noteBodyTexts.Length];
        for (int i = 0; i < noteTitleTexts.Length; i++)
        {
            _noteTitles[i] = ManualLabel(UiTheme.Label(noteTitleTexts[i]));
            _noteTitles[i].BackColor = Color.Transparent;
            _noteBodies[i] = ManualLabel(UiTheme.Label(noteBodyTexts[i], muted: true));
            _noteBodies[i].Font = UiTheme.CaptionFont;
            _noteBodies[i].BackColor = Color.Transparent;
            _notesCard.Controls.Add(_noteTitles[i]);
            _notesCard.Controls.Add(_noteBodies[i]);
        }

        // 4. Minimal Footer Bar
        _privacy = ManualLabel(UiTheme.Label("本地运行 · 不上传任何数据", muted: true));
        _privacy.Font = UiTheme.CaptionFont;
        _privacy.BackColor = Color.Transparent;

        _close = new RoundedButton
        {
            Text = "关闭",
            AutoSize = false,
            DialogResult = DialogResult.OK,
            AccessibleName = "关闭关于窗口",
            Margin = Padding.Empty
        };
        UiTheme.StyleButton(_close, primary: true);
        _close.AutoSize = false;
        _close.MinimumSize = Size.Empty;
        _close.Click += (_, _) => Close();

        _content.Controls.Add(_heroCard);
        _content.Controls.AddRange(_featureTiles);
        _content.Controls.AddRange(new Control[] { _notesCard, _privacy, _close });
        Controls.Add(_content);
        AcceptButton = _close;
        CancelButton = _close;

        foreach (Control control in AllContentControls())
            control.FontChanged += (_, _) => FitToContent();
        _picture.SizeChanged += (_, _) => RefreshApplicationImage();
        FitToContent();
        RefreshApplicationImage();
    }

    private void PaintHeroCard(object? sender, PaintEventArgs e)
    {
        if (_heroCard.Width <= 2 || _heroCard.Height <= 2) return;
        e.Graphics.Clear(UiTheme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int dpi = _heroCard.DeviceDpi;
        int radius = UiTheme.Scale(12, dpi);
        using (var bg = new SolidBrush(UiTheme.Surface))
            e.Graphics.FillRoundedRectangle(bg, 0.5F, 0.5F, _heroCard.Width - 1, _heroCard.Height - 1, radius);
        using (var border = new Pen(UiTheme.BorderSubtle))
            e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, _heroCard.Width - 1, _heroCard.Height - 1, radius);

        // Dark version pill on top-right matching TrayPopoverForm's signature pills
        using var pillFont = new Font(UiTheme.MenuFont.FontFamily, 8F);
        string verText = UiTheme.AppVersionLabel;
        Size verSize = TextRenderer.MeasureText(e.Graphics, verText, pillFont, Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        int pillPadX = UiTheme.Scale(8, dpi);
        int pillW = verSize.Width + pillPadX * 2;
        int pillH = verSize.Height + UiTheme.Scale(6, dpi);
        int pillX = _heroCard.Width - UiTheme.Scale(18, dpi) - pillW;
        int pillY = UiTheme.Scale(18, dpi);
        if (pillX > _picture.Right + UiTheme.Scale(120, dpi))
        {
            using (var pillBrush = new SolidBrush(UiTheme.Accent))
                e.Graphics.FillRoundedRectangle(pillBrush, pillX, pillY, pillW, pillH, pillH / 2F);
            TextRenderer.DrawText(e.Graphics, verText, pillFont, new Rectangle(pillX, pillY, pillW, pillH),
                UiTheme.AccentText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    private void PaintNotesCard(object? sender, PaintEventArgs e)
    {
        if (_notesCard.Width <= 2 || _notesCard.Height <= 2) return;
        e.Graphics.Clear(UiTheme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int dpi = _notesCard.DeviceDpi;
        int radius = UiTheme.Scale(12, dpi);
        using (var bg = new SolidBrush(UiTheme.Surface))
            e.Graphics.FillRoundedRectangle(bg, 0.5F, 0.5F, _notesCard.Width - 1, _notesCard.Height - 1, radius);
        if (_notesDividerY > 0 && _notesDividerY < _notesCard.Height - 2)
        {
            int inset = UiTheme.Scale(16, dpi);
            using var divider = new Pen(UiTheme.BorderSubtle);
            e.Graphics.DrawLine(divider, inset, _notesDividerY, _notesCard.Width - inset, _notesDividerY);
        }
        using (var border = new Pen(UiTheme.BorderSubtle))
            e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, _notesCard.Width - 1, _notesCard.Height - 1, radius);
    }

    private IEnumerable<Control> AllContentControls()
    {
        yield return _title;
        yield return _subtitle;
        foreach (FeatureTile tile in _featureTiles)
        {
            yield return tile.TitleLabel;
            yield return tile.DetailLabel;
        }
        foreach (Label label in _noteTitles) yield return label;
        foreach (Label label in _noteBodies) yield return label;
        yield return _privacy;
        yield return _close;
    }

    private static Label ManualLabel(Label label)
    {
        label.Dock = DockStyle.None;
        label.AutoSize = false;
        label.Margin = Padding.Empty;
        return label;
    }

    private int Scaled(int value) => UiTheme.Scale(value, DeviceDpi);

    internal static Size MeasureLabel(Control control, int width = 0, bool multiline = false)
    {
        TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
            (multiline ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
        int slack = UiTheme.Scale(4, control.DeviceDpi);
        int measureWidth = width > 0 ? Math.Max(1, width - slack) : int.MaxValue;
        Size measured = TextRenderer.MeasureText(control.Text, control.Font,
            new Size(measureWidth, int.MaxValue), flags);
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

    private Size CloseSize => new(Math.Max(Scaled(84), MeasureLabel(_close).Width + Scaled(30)),
        Math.Max(Scaled(UiTheme.ButtonHeight), _close.Font.Height + Scaled(12)));

    private int PreferredContentWidth()
    {
        int width = Scaled(532) - Scaled(UiTheme.PagePadding) * 2;
        width = Math.Max(width, MeasureLabel(_title).Width + Scaled(160));
        width = Math.Max(width, MeasureLabel(_subtitle).Width + Scaled(110));
        width = Math.Max(width, MeasureLabel(_privacy).Width + CloseSize.Width + Scaled(24));
        return width;
    }

    private int LayoutContent(int width, bool apply)
    {
        int pagePad = Scaled(UiTheme.PagePadding);
        int contentWidth = Math.Max(1, width - pagePad * 2);
        int y = Scaled(16);

        // 1. Hero Card
        int heroPad = Scaled(18);
        int iconSize = Scaled(58);
        int textLeft = heroPad + iconSize + Scaled(16);
        int pillReserve = Scaled(64);
        int titleW = Math.Max(1, contentWidth - textLeft - heroPad - pillReserve);
        int subW = Math.Max(1, contentWidth - textLeft - heroPad);
        Size titleSize = MeasureLabel(_title, titleW, true);
        Size subSize = MeasureLabel(_subtitle, subW, true);
        int textBlockH = titleSize.Height + Scaled(4) + subSize.Height;
        int heroH = Math.Max(iconSize + heroPad * 2, textBlockH + heroPad * 2);
        if (apply)
        {
            _heroCard.SetBounds(pagePad, y, contentWidth, heroH);
            _picture.SetBounds(heroPad, (heroH - iconSize) / 2, iconSize, iconSize);
            int textY = (heroH - textBlockH) / 2;
            _title.SetBounds(textLeft, textY, titleW, titleSize.Height);
            _subtitle.SetBounds(textLeft, textY + titleSize.Height + Scaled(4), subW, subSize.Height);
        }
        y += heroH + Scaled(12);

        // 2. 2x2 Feature Card Grid (falls back to 1 column at large enlarged font scales)
        int gridGap = Scaled(10);
        int colW = (contentWidth - gridGap) / 2;
        bool twoCol = colW >= Scaled(210) && _featureTiles.All(t => MeasureLabel(t.TitleLabel).Width + Scaled(66) <= colW);
        if (twoCol)
        {
            int col2W = contentWidth - colW - gridGap;
            for (int row = 0; row < 2; row++)
            {
                FeatureTile left = _featureTiles[row * 2];
                FeatureTile right = _featureTiles[row * 2 + 1];
                int rowH = Math.Max(left.MeasureHeight(colW), right.MeasureHeight(col2W));
                if (apply)
                {
                    left.SetBounds(pagePad, y, colW, rowH);
                    left.LayoutTile();
                    right.SetBounds(pagePad + colW + gridGap, y, col2W, rowH);
                    right.LayoutTile();
                }
                y += rowH + gridGap;
            }
            y += Scaled(2);
        }
        else
        {
            foreach (FeatureTile tile in _featureTiles)
            {
                int h = tile.MeasureHeight(contentWidth);
                if (apply)
                {
                    tile.SetBounds(pagePad, y, contentWidth, h);
                    tile.LayoutTile();
                }
                y += h + gridGap;
            }
            y += Scaled(2);
        }

        // 3. Grouped Notes Card
        int notePadX = Scaled(16);
        int notePadY = Scaled(12);
        int noteNameW = _noteTitles.Max(l => MeasureLabel(l).Width) + Scaled(4);
        int noteGap = Scaled(14);
        bool stackNotes = noteNameW + noteGap + Scaled(160) > contentWidth - notePadX * 2;
        int noteY = 0;
        int dividerY = -1;
        for (int i = 0; i < _noteTitles.Length; i++)
        {
            if (i == 1) dividerY = noteY;
            noteY += notePadY;
            if (stackNotes)
            {
                int fullW = Math.Max(1, contentWidth - notePadX * 2);
                Size t = MeasureLabel(_noteTitles[i], fullW, true);
                Size b = MeasureLabel(_noteBodies[i], fullW, true);
                if (apply)
                {
                    _noteTitles[i].SetBounds(notePadX, noteY, fullW, t.Height);
                    _noteBodies[i].SetBounds(notePadX, noteY + t.Height + Scaled(3), fullW, b.Height);
                }
                noteY += t.Height + Scaled(3) + b.Height + notePadY;
            }
            else
            {
                int bodyLeft = notePadX + noteNameW + noteGap;
                int bodyW = Math.Max(1, contentWidth - bodyLeft - notePadX);
                Size t = MeasureLabel(_noteTitles[i], noteNameW, true);
                Size b = MeasureLabel(_noteBodies[i], bodyW, true);
                int rowH = Math.Max(t.Height, b.Height);
                if (apply)
                {
                    _noteTitles[i].SetBounds(notePadX, noteY + (rowH - t.Height) / 2, noteNameW, t.Height);
                    _noteBodies[i].SetBounds(bodyLeft, noteY + (rowH - b.Height) / 2, bodyW, b.Height);
                }
                noteY += rowH + notePadY;
            }
        }
        if (apply)
        {
            _notesDividerY = dividerY;
            _notesCard.SetBounds(pagePad, y, contentWidth, noteY);
        }
        y += noteY + Scaled(16);

        // 4. Minimal Footer Bar
        Size close = CloseSize;
        int privacyW = Math.Max(1, contentWidth - close.Width - Scaled(16));
        Size privacy = MeasureLabel(_privacy, privacyW, true);
        int footerH = Math.Max(close.Height, privacy.Height);
        if (apply)
        {
            _privacy.SetBounds(pagePad + Scaled(4), y + (footerH - privacy.Height) / 2, Math.Max(1, privacyW - Scaled(4)), privacy.Height);
            _close.SetBounds(width - pagePad - close.Width, y + (footerH - close.Height) / 2, close.Width, close.Height);
        }
        return y + footerH + Scaled(20);
    }

    private void FitToContent()
    {
        if (_fitting || IsDisposed) return;
        _fitting = true;
        try
        {
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            if (workArea.Width <= 0 || workArea.Height <= 0) workArea = new Rectangle(0, 0, 1920, 1040);
            int pagePad = Scaled(UiTheme.PagePadding);
            int maximumWidth = Math.Max(Scaled(380), workArea.Width - Scaled(32));
            int width = Math.Min(maximumWidth, PreferredContentWidth() + pagePad * 2);
            int layoutWidth = width;
            int contentHeight = LayoutContent(layoutWidth, apply: false);
            int maximumHeight = Math.Max(Scaled(360), workArea.Height - Scaled(32));
            bool scroll = contentHeight > maximumHeight;
            if (scroll)
            {
                layoutWidth = Math.Max(Scaled(340), layoutWidth - SystemInformation.VerticalScrollBarWidth);
                contentHeight = LayoutContent(layoutWidth, apply: false);
            }
            AutoScroll = scroll;
            AutoScrollMinSize = scroll ? new Size(0, contentHeight) : Size.Empty;
            _content.SetBounds(0, 0, layoutWidth, contentHeight);
            ClientSize = new Size(width, Math.Min(contentHeight, maximumHeight));
            LayoutContent(layoutWidth, apply: true);
            PerformLayout();
            Invalidate(true);
        }
        finally { _fitting = false; }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToContent();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        FitToContent();
    }

    private void RefreshApplicationImage()
    {
        int size = Math.Min(_picture.ClientSize.Width, _picture.ClientSize.Height);
        if (size <= 0 || _picture.Image?.Width == size) return;
        Image? previous = _picture.Image;
        _picture.Image = TrayIconFactory.CreateApplicationImage(size);
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _picture.Image?.Dispose();
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

    private sealed class FeatureTile : Panel
    {
        private readonly UiIcon _icon;
        private Bitmap? _iconBitmap;
        private int _iconDpi;
        internal Label TitleLabel { get; }
        internal Label DetailLabel { get; }

        internal FeatureTile(UiIcon icon, string title, string detail)
        {
            _icon = icon;
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = UiTheme.Surface;
            Margin = Padding.Empty;

            TitleLabel = ManualLabel(UiTheme.Label(title));
            TitleLabel.BackColor = Color.Transparent;
            DetailLabel = ManualLabel(UiTheme.Label(detail, muted: true));
            DetailLabel.Font = UiTheme.CaptionFont;
            DetailLabel.BackColor = Color.Transparent;
            Controls.Add(TitleLabel);
            Controls.Add(DetailLabel);
        }

        private int Scaled(int value) => UiTheme.Scale(value, DeviceDpi);

        internal int MeasureHeight(int width)
        {
            int pad = Scaled(14);
            int well = Scaled(30);
            int headerW = Math.Max(1, width - pad * 2 - well - Scaled(10));
            int bodyW = Math.Max(1, width - pad * 2);
            int headerH = Math.Max(well, MeasureLabel(TitleLabel, headerW, true).Height);
            int bodyH = MeasureLabel(DetailLabel, bodyW, true).Height;
            return pad + headerH + Scaled(8) + bodyH + pad;
        }

        internal void LayoutTile()
        {
            if (Width <= 0) return;
            int pad = Scaled(14);
            int well = Scaled(30);
            int titleX = pad + well + Scaled(10);
            int titleW = Math.Max(1, Width - titleX - pad);
            Size titleSize = MeasureLabel(TitleLabel, titleW, true);
            int headerH = Math.Max(well, titleSize.Height);
            TitleLabel.SetBounds(titleX, pad + (headerH - titleSize.Height) / 2, titleW, titleSize.Height);

            int bodyW = Math.Max(1, Width - pad * 2);
            Size bodySize = MeasureLabel(DetailLabel, bodyW, true);
            DetailLabel.SetBounds(pad, pad + headerH + Scaled(8), bodyW, bodySize.Height);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Background);
            if (Width <= 2 || Height <= 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int radius = UiTheme.Scale(12, dpi);
            using (var fill = new SolidBrush(UiTheme.Surface))
                e.Graphics.FillRoundedRectangle(fill, 0.5F, 0.5F, Width - 1, Height - 1, radius);
            using (var border = new Pen(UiTheme.BorderSubtle))
                e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1, radius);

            int pad = UiTheme.Scale(14, dpi);
            int well = UiTheme.Scale(30, dpi);
            using (var wellBrush = new SolidBrush(UiTheme.Background))
                e.Graphics.FillRoundedRectangle(wellBrush, pad, pad, well, well, UiTheme.Scale(8, dpi));

            int iconSize = UiTheme.Scale(17, dpi);
            if (_iconBitmap is null || _iconDpi != dpi)
            {
                _iconBitmap?.Dispose();
                _iconBitmap = UiIconFactory.Create(_icon, iconSize, UiTheme.Text);
                _iconDpi = dpi;
            }
            e.Graphics.DrawImageUnscaled(_iconBitmap,
                pad + (well - _iconBitmap.Width) / 2,
                pad + (well - _iconBitmap.Height) / 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _iconBitmap?.Dispose();
            base.Dispose(disposing);
        }
    }
}
