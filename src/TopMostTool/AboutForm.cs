namespace TopMostTool;

internal sealed class AboutForm : Form
{
    private readonly PictureBox _picture;
    internal AboutForm()
    {
        Text = "关于 · 窗口置顶";
        UiTheme.StyleForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(500, 0);
        var layout = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(UiTheme.PagePadding) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var identity = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 0, 0, 20) };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _picture = new PictureBox { Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.CenterImage, AccessibleName = "程序图标" };
        _picture.SizeChanged += (_, _) => RefreshApplicationImage();
        identity.Controls.Add(_picture, 0, 0);
        identity.SetRowSpan(_picture, 2);
        var title = UiTheme.Label("窗口置顶", heading: true);
        title.Font = UiTheme.HeadingFont;
        identity.Controls.Add(title, 1, 0);
        identity.Controls.Add(UiTheme.Label("TopMostTool   ·   版本 1.0.0", muted: true), 1, 1);
        layout.Controls.Add(identity);
        layout.Controls.Add(UiTheme.Label("窗口置顶，截图贴图。", heading: true));
        layout.Controls.Add(UiTheme.Label("让常用窗口和屏幕上的重要内容随时可见。", muted: true));
        var guide = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, BackColor = UiTheme.Surface, Padding = new Padding(16), Margin = new Padding(0, 10, 0, 16) };
        guide.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        guide.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        UiTheme.StyleCard(guide, 10);
        var guideTitle = UiTheme.Label("使用方法", heading: true);
        guide.Controls.Add(guideTitle, 0, 0);
        guide.SetColumnSpan(guideTitle, 2);
        AddGuideRow(1, "窗口置顶", "从托盘选择窗口，或用快捷键切换前台窗口的置顶状态。");
        AddGuideRow(2, "截图贴图", "拖动框选屏幕区域，松开鼠标生成贴图。");
        AddGuideRow(3, "调整贴图", "拖动移动，滚轮缩放；右键复制、保存或关闭。");
        AddGuideRow(4, "批量管理", "在托盘取消全部窗口置顶，或关闭全部截图贴图。");
        void AddGuideRow(int row, string name, string description)
        {
            Label label = UiTheme.Label(name, heading: true);
            label.Margin = new Padding(0, 0, 14, UiTheme.Spacing);
            guide.Controls.Add(label, 0, row);
            guide.Controls.Add(UiTheme.Label(description), 1, row);
        }
        layout.Controls.Add(guide);
        layout.Controls.Add(UiTheme.Label("贴图关闭后不会保留，请先保存需要的图片。\n退出会取消本工具设置的窗口置顶，并关闭全部贴图。", muted: true));
        layout.Controls.Add(UiTheme.Label("快捷键可在托盘“快捷键设置”中修改。", muted: true));
        var close = new RoundedButton { Text = "关闭", DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right };
        UiTheme.StyleButton(close, primary: true);
        close.Click += (_, _) => Close();
        layout.Controls.Add(close);
        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
        RefreshApplicationImage();
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
}
