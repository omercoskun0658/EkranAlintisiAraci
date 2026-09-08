using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EkranAlintisiAraci;

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Uygulama ve sistem tepsisi (tray) ikonunu sağlar.
/// Öncelikli olarak gömülü favicon.ico dosyasını yükler,
/// bulunamazsa .exe ikonunu veya GDI+ çizimini fallback olarak kullanır.
/// </summary>
public static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Create()
    {
        try
        {
            // 1. Gömülü favicon.ico kaynağından yükle
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("EkranAlintisiAraci.favicon.ico");
            if (stream != null)
            {
                return new Icon(stream);
            }

            // 2. .exe'nin gömülü native ikonunu dene
            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null)
            {
                return (Icon)exeIcon.Clone();
            }
        }
        catch
        {
            // Herhangi bir hata durumunda aşağıdaki GDI+ çizimine geç
        }

        return CreateProceduralIcon();
    }

    private static Icon CreateProceduralIcon()
    {
        using var bmp = new Bitmap(64, 64);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Arka plan çemberi – koyu lacivert
        using (var bg = new SolidBrush(Color.FromArgb(22, 32, 58)))
            g.FillEllipse(bg, 2, 2, 60, 60);

        // Dış halka – accent mavi
        using (var ring = new Pen(Color.FromArgb(79, 140, 255), 3.5f))
            g.DrawEllipse(ring, 2, 2, 60, 60);

        // Crosshair yatay çizgiler (boşluklu)
        using (var cross = new Pen(Color.White, 2.5f))
        {
            g.DrawLine(cross, 8,  32, 24, 32);   // sol kol
            g.DrawLine(cross, 40, 32, 56, 32);   // sağ kol
            g.DrawLine(cross, 32, 8,  32, 24);   // üst kol
            g.DrawLine(cross, 32, 40, 32, 56);   // alt kol
        }

        // İç çember – accent mavi
        using (var inner = new Pen(Color.FromArgb(79, 140, 255), 2.5f))
            g.DrawEllipse(inner, 22, 22, 20, 20);

        // Merkez nokta
        using (var dot = new SolidBrush(Color.White))
            g.FillEllipse(dot, 29, 29, 6, 6);

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
