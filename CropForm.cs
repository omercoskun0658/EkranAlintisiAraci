using System.Drawing.Drawing2D;

namespace EkranAlintisiAraci;

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Tüm ekranı kaplayan, şeffaf seçim formu.
/// Arka plan: önceden alınmış ekran görüntüsü + koyu maske.
/// Seçilen alan: tam parlaklıkta gösterilir (maske kaldırılır).
/// Beyaz kenarlık + köşe tutamaçları + boyut etiketi (A seçeneği).
/// </summary>
public sealed class CropForm : Form
{
    // ── Durum ────────────────────────────────────────────────────────────────
    private readonly Bitmap _screen;
    private Point _start;
    private Point _current;
    private bool  _dragging;

    /// <summary>
    /// Kullanıcının seçtiği dikdörtgen (ekran koordinatları).
    /// Kullanıcı ESC ile iptal ettiyse ya da hiç seçim yapmadıysa Rectangle.Empty.
    /// </summary>
    public Rectangle SelectedRegion { get; private set; } = Rectangle.Empty;

    /// <summary>
    /// ESC ile iptal edildiyse true; sol tuş bırakıldıysa false.
    /// </summary>
    public bool WasCancelled { get; private set; } = true;

    // ── Kurucu ───────────────────────────────────────────────────────────────
    public CropForm(Bitmap screen)
    {
        _screen = screen;

        // Form özellikleri
        AutoScaleMode   = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        TopMost         = true;
        ShowInTaskbar   = false;
        Cursor          = Cursors.Cross;
        BackColor       = Color.Black;
        KeyPreview      = true;

        // Tüm sanal ekranı kapla (çoklu monitör)
        var vs = SystemInformation.VirtualScreen;
        StartPosition = FormStartPosition.Manual;
        Location      = new Point(vs.X, vs.Y);
        Size          = new Size(vs.Width, vs.Height);

        // Çift tampon – titreme yok
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint  |
            ControlStyles.UserPaint, true);

        // Olaylar
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                CancelCapture();
            }
        };
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp   += OnMouseUp;
    }

    private void CancelCapture()
    {
        WasCancelled = true;
        SelectedRegion = Rectangle.Empty;
        Close();
    }

    // ── Fare Olayları ────────────────────────────────────────────────────────
    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            CancelCapture();
            return;
        }

        if (e.Button != MouseButtons.Left) return;
        _start    = e.Location;
        _current  = e.Location;
        _dragging = true;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _current = e.Location;
        Invalidate();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_dragging) return;
        _dragging = false;
        _current  = e.Location;

        var rect = Normalize(_start, _current);
        var vs = SystemInformation.VirtualScreen;

        // Ekran sınırları dışına taşmayı önlemek için clamp et
        int x = Math.Clamp(rect.X, 0, vs.Width);
        int y = Math.Clamp(rect.Y, 0, vs.Height);
        int r = Math.Clamp(rect.Right, 0, vs.Width);
        int b = Math.Clamp(rect.Bottom, 0, vs.Height);

        SelectedRegion = new Rectangle(x, y, Math.Max(0, r - x), Math.Max(0, b - y));
        WasCancelled = false;
        Close();
    }

    // ── Çizim ────────────────────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

        // 1) Ekran görüntüsünü arka plana çiz
        g.DrawImage(_screen, 0, 0, Width, Height);

        using var dark = new SolidBrush(Color.FromArgb(130, 0, 0, 0));

        if (_dragging && _start != _current)
        {
            var sel = Normalize(_start, _current);

            // 2) Seçim dışındaki 4 şerit – koyu maske
            if (sel.Top > 0)
                g.FillRectangle(dark, 0, 0, Width, sel.Top);
            if (sel.Bottom < Height)
                g.FillRectangle(dark, 0, sel.Bottom, Width, Height - sel.Bottom);
            if (sel.Left > 0)
                g.FillRectangle(dark, 0, sel.Top, sel.Left, sel.Height);
            if (sel.Right < Width)
                g.FillRectangle(dark, sel.Right, sel.Top, Width - sel.Right, sel.Height);

            // 3) Seçim alanı zaten parlak görünüyor (screenshot orijinal)

            // 4) Beyaz kenarlık
            using var borderPen = new Pen(Color.White, 2f);
            g.DrawRectangle(borderPen, sel);

            // 5) Köşe tutamaçları
            DrawCornerHandles(g, sel);

            // 6) Boyut etiketi
            DrawDimensionLabel(g, sel, _current);
        }
        else
        {
            // Henüz sürükleme yok – tam koyu maske + ipucu metni
            g.FillRectangle(dark, 0, 0, Width, Height);
            DrawHint(g);
        }
    }

    private static void DrawCornerHandles(Graphics g, Rectangle sel)
    {
        const int S = 8;
        using var b = new SolidBrush(Color.White);
        // Sol-üst
        g.FillRectangle(b, sel.Left - 2,      sel.Top - 2,       S, S);
        // Sağ-üst
        g.FillRectangle(b, sel.Right - S + 2, sel.Top - 2,       S, S);
        // Sol-alt
        g.FillRectangle(b, sel.Left - 2,      sel.Bottom - S + 2, S, S);
        // Sağ-alt
        g.FillRectangle(b, sel.Right - S + 2, sel.Bottom - S + 2, S, S);
    }

    private static void DrawDimensionLabel(Graphics g, Rectangle sel, Point cursor)
    {
        string text = $"  {sel.Width} × {sel.Height} px  ";
        using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
        var sz = g.MeasureString(text, font);

        float lx = cursor.X + 16;
        float ly = cursor.Y + 16;

        // Ekran kenarından taşmasını önle
        float visW = g.VisibleClipBounds.Width;
        float visH = g.VisibleClipBounds.Height;
        if (lx + sz.Width  > visW - 8) lx = cursor.X - sz.Width  - 16;
        if (ly + sz.Height > visH - 8) ly = cursor.Y - sz.Height - 16;

        var bg = new RectangleF(lx - 3, ly - 3, sz.Width + 6, sz.Height + 6);

        using var bgBrush = new SolidBrush(Color.FromArgb(210, 15, 15, 15));
        g.FillRectangle(bgBrush, bg);

        using var borderPen = new Pen(Color.FromArgb(100, 255, 255, 255));
        g.DrawRectangle(borderPen, bg.X, bg.Y, bg.Width, bg.Height);

        g.DrawString(text, font, Brushes.White, lx, ly);
    }

    private void DrawHint(Graphics g)
    {
        const string Hint = "  Ekran alıntısı almak için sürükleyin   •   İptal: ESC  ";
        using var font = new Font("Segoe UI", 13f, FontStyle.Regular);
        var sz = g.MeasureString(Hint, font);

        float x  = (Width  - sz.Width)  / 2f;
        float y  = 18f;
        float ph = sz.Height + 12;

        using var bg = new SolidBrush(Color.FromArgb(185, 10, 10, 10));
        g.FillRectangle(bg, x - 4, y, sz.Width + 8, ph);
        g.DrawString(Hint, font, Brushes.White, x, y + 6f);
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────────
    private static Rectangle Normalize(Point a, Point b) =>
        new(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(b.X - a.X),
            Math.Abs(b.Y - a.Y));
}
