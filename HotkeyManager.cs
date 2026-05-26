using System.Runtime.InteropServices;

namespace EkranAlintisiAraci;

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Win32 RegisterHotKey API'sini kullanarak sistem genelinde kısayol tuşu kaydeder.
/// WM_HOTKEY mesajı geldiğinde <see cref="HotkeyPressed"/> event'ini fırlatır.
/// Görünür pencere gerektirmez – gizli bir NativeWindow üzerinde çalışır.
/// </summary>
public sealed class HotkeyManager : NativeWindow, IDisposable
{
    // ── Win32 Sabitleri ──────────────────────────────────────────────────────
    private const int WM_HOTKEY  = 0x0312;
    private const int HOTKEY_ID  = 0x2A17;   // Uygulamaya özgü rastgele ID

    // ── P/Invoke ─────────────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── Durum ────────────────────────────────────────────────────────────────
    private bool _registered;

    /// <summary>Kısayol tuşuna basıldığında UI thread'inde tetiklenir.</summary>
    public event EventHandler? HotkeyPressed;

    // ── Kurucu ───────────────────────────────────────────────────────────────
    public HotkeyManager()
    {
        // Gizli pencere oluştur (mesaj kuyruğu için)
        CreateHandle(new CreateParams());
    }

    // ── API ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verilen tuşu modifier olmadan kaydeder.
    /// Daha önce kayıtlı tuş varsa önce kaldırır.
    /// </summary>
    /// <param name="key">Kayıt edilecek Keys değeri.</param>
    /// <param name="modifiers">0 = modifier yok (varsayılan).</param>
    /// <returns>Kayıt başarılıysa true.</returns>
    public bool Register(Keys key, uint modifiers = 0)
    {
        if (_registered)
            UnregisterHotKey(Handle, HOTKEY_ID);

        _registered = RegisterHotKey(Handle, HOTKEY_ID, modifiers, (uint)key);
        return _registered;
    }

    /// <summary>Kayıtlı kısayolu kaldırır.</summary>
    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, HOTKEY_ID);
            _registered = false;
        }
    }

    // ── Windows Mesaj İşleme ─────────────────────────────────────────────────
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            HotkeyPressed?.Invoke(this, EventArgs.Empty);

        base.WndProc(ref m);
    }

    // ── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        Unregister();
        DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
