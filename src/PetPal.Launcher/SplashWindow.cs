namespace PetPal.Launcher;

/// <summary>
/// Açılış pəncərəsi. Yığının qalxması (xüsusən Docker soyuq başlayanda) yarım
/// dəqiqədən artıq çəkə bilər — bu müddətdə heç nə görünməsə istifadəçi ikonaya
/// təkrar-təkrar basır, ona görə hər addım burada adı ilə göstərilir.
///
/// Rəng sistemi tətbiqin özü ilə eynidir: fon bənövşəyi ailəsidir (brend),
/// yükləmə zolağı isə hər üç ailəni növbə ilə göstərir — bax <see cref="AccentBar"/>.
///
/// Yerləşdirmə tam axına buraxılıb (TableLayoutPanel + AutoSize), sabit piksel
/// koordinatları yoxdur: 150% miqyaslı ekranda şrift böyüyür, sabit koordinatlar
/// isə böyümür — nəticədə mətnlər üst-üstə düşür və pəncərə kəsilir.
/// </summary>
internal sealed class SplashWindow : Form
{
    /// <summary>--pp-primary-dark — fon tünd olmalıdır ki, işıqlı aksentlər üstündə oxunsun.</summary>
    private static readonly Color Brand = Color.FromArgb(0x54, 0x41, 0x9E);

    /// <summary>Bənövşəyi fonda ikinci dərəcəli mətn — 6.8:1.</summary>
    private static readonly Color Muted = Color.FromArgb(0xEF, 0xEB, 0xFA);

    private const int LogicalWidth = 460;

    private readonly Label _status;
    private readonly TableLayoutPanel _layout;

    public SplashWindow()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        // Ölçü konstruktorda yekunlaşdığına görə CenterScreen düzgün işləyir —
        // özümüz mərkəzləşdirsək DPI miqyaslı ekranda ekran koordinatları ilə
        // pəncərə koordinatlarını qarışdırmaq riski var.
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        BackColor = Brand;
        ForeColor = Color.White;
        TopMost = true;
        Text = "AI Pets for Kids";
        Icon = LoadAppIcon();

        var width = LogicalToDeviceUnits(LogicalWidth);
        var contentWidth = width - LogicalToDeviceUnits(56);

        var title = CreateLabel("AI Pets for Kids", 24F, FontStyle.Bold, Color.White, contentWidth);
        title.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(2));

        var subtitle = CreateLabel(
            LauncherText.T("Uşaqlar üçün ağıllı AI pet", "A smart AI pet for kids"),
            10F, FontStyle.Regular, Muted, contentWidth);
        subtitle.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(20));

        _status = CreateLabel(
            LauncherText.T("Başladılır…", "Starting…"),
            9.75F, FontStyle.Regular, Color.White, contentWidth);
        _status.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(14));

        var bar = new AccentBar(Brand)
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Height = LogicalToDeviceUnits(8),
            Margin = Padding.Empty,
        };

        // Panel eni sabitdir, hündürlüyü məzmuna görə böyüyür. Control.MaximumSize-da
        // 0 həqiqətən "məhdudiyyət yoxdur" deməkdir (Form-da isə yox — ona görə
        // pəncərənin ölçüsü aşağıda paneldən hesablanır, AutoSize ilə deyil).
        _layout = new TableLayoutPanel
        {
            Location = Point.Empty,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(width, 0),
            MaximumSize = new Size(width, 0),
            ColumnCount = 1,
            BackColor = Brand,
            Padding = new Padding(
                LogicalToDeviceUnits(28),
                LogicalToDeviceUnits(30),
                LogicalToDeviceUnits(28),
                LogicalToDeviceUnits(28)),
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        foreach (var control in new Control[] { title, subtitle, _status, bar })
        {
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(control);
        }

        Controls.Add(_layout);
        ResumeLayout(performLayout: true);
        ClientSize = new Size(width, _layout.PreferredSize.Height);
    }

    public void SetStatus(string text) => _status.Text = text;

    private static Label CreateLabel(string text, float size, FontStyle style, Color color, int maxWidth) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", size, style),
        ForeColor = color,
        BackColor = Brand,
        AutoSize = true,
        // Uzun status mətni kəsilmək əvəzinə alt sətrə keçsin.
        MaximumSize = new Size(maxWidth, 0),
        TextAlign = ContentAlignment.MiddleCenter,
        Anchor = AnchorStyles.None,
    };

    private static Icon? LoadAppIcon()
    {
        try
        {
            return Environment.ProcessPath is { } path ? Icon.ExtractAssociatedIcon(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
