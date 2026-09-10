using System.Drawing.Drawing2D;

namespace PetPal.Launcher;

/// <summary>
/// Qeyri-müəyyən yükləmə zolağı.
///
/// Standart WinForms marquee ProgressBar Windows-un öz yaşıl rəngini işlədir və
/// visual styles altında onu dəyişmək mümkün deyil — nəticədə açılış ekranı
/// tətbiqin rəng sisteminə aid olmayan bir yaşılla başlayırdı. Burada segment
/// PetPal-ın üç ailəsini növbə ilə gəzir: ağ (ağıl), yaşıl (canlılıq),
/// narıncı (enerji). Bənövşəyi ailəsini fonun özü təmsil edir.
/// </summary>
internal sealed class AccentBar : Control
{
    private static readonly Color[] Accents =
    [
        // Gecə çalarları: fon tünd bənövşəyi olduğuna görə ailələrin işıqlı
        // variantları lazımdır — hər biri fonla ən azı 4.5:1 verir (qeyri-mətn həddi 3:1).
        Color.White,                        // ağıl — brend
        Color.FromArgb(0x7F, 0xD4, 0xB0),   // canlılıq
        Color.FromArgb(0xF2, 0xC4, 0x8E),   // enerji
    ];

    private readonly System.Windows.Forms.Timer _timer;
    private float _phase;
    private int _accent;

    /// <param name="background">
    /// Fon rəngi valideyndən açıq şəkildə alınır: iç-içə şəffaf kontrol
    /// WinForms-da etibarsız çəkilir, ona görə zolaq öz fonunu özü doldurur.
    /// </param>
    public AccentBar(Color background)
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = background;
        TabStop = false;

        _timer = new System.Windows.Forms.Timer { Interval = 30 };
        _timer.Tick += Advance;
        _timer.Start();
    }

    private void Advance(object? sender, EventArgs e)
    {
        _phase += 0.013f;
        if (_phase >= 1f)
        {
            _phase = 0f;
            _accent = (_accent + 1) % Accents.Length;
        }

        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Tick -= Advance;
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        var track = new Rectangle(0, 0, Width - 1, Height - 1);
        if (track.Width <= 1 || track.Height <= 1)
            return;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var trackBrush = new SolidBrush(Color.FromArgb(54, 255, 255, 255)))
        using (var trackPath = Rounded(track))
            g.FillPath(trackBrush, trackPath);

        // Segment kənardan girib o biri kənardan çıxır; smoothstep ona
        // sürətlənib-yavaşlama verir, beləliklə hərəkət mexaniki görünmür.
        var segment = Math.Max(track.Height * 4, track.Width / 3);
        var eased = _phase * _phase * (3f - (2f * _phase));
        var x = (int)((eased * (track.Width + segment)) - segment);

        var saved = g.Save();
        using (var clip = Rounded(track))
            g.SetClip(clip);

        using (var brush = new SolidBrush(Accents[_accent]))
        using (var path = Rounded(new Rectangle(x, 0, segment, track.Height)))
            g.FillPath(brush, path);

        g.Restore(saved);
    }

    private static GraphicsPath Rounded(Rectangle r)
    {
        var path = new GraphicsPath();
        var d = Math.Max(r.Height, 1);

        if (r.Width <= d)
        {
            path.AddEllipse(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 90, 180);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }
}
