using System.Diagnostics;
using System.Drawing.Imaging;

namespace EkranAlintisiAraci;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeyManager;
    private SettingsForm? _settingsForm;
    private bool _isCapturing;

    public TrayApplicationContext()
    {
        // 1. Ayarları Yükle (JSON yoksa RAM'den)
        _settings = AppSettings.Load();

        // 2. Tray Icon
        _trayIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Visible = true,
            Text = "Ekran Alıntısı Aracı (Hazır)"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Ayarlar", null, (s, e) => ShowSettings());
        menu.Items.Add("-");
        menu.Items.Add("Çıkış", null, (s, e) => Exit());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) => ShowSettings();
        _trayIcon.BalloonTipClicked += OnBalloonTipClicked;

        // 3. Hotkey
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        RegisterCurrentHotkey();
    }

    private void RegisterCurrentHotkey()
    {
        _hotkeyManager.Unregister();
        bool success = _hotkeyManager.Register(_settings.Hotkey);
        if (!success)
        {
            _trayIcon.ShowBalloonTip(3000, "Hata", $"{_settings.HotkeyName} kısayolu kaydedilemedi. Başka bir uygulama kullanıyor olabilir.", ToolTipIcon.Warning);
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings);
            _settingsForm.FormClosed += (s, e) => 
            {
                if (_settingsForm.DialogResult == DialogResult.OK)
                {
                    // Ayarlar kaydedildi, kısayolu güncelle
                    RegisterCurrentHotkey();
                }
                _settingsForm = null;
            };
            _settingsForm.Show();
        }
        else
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
        }
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_isCapturing) return;

        // Settings açıksa gizle ve bekle
        if (_settingsForm != null && _settingsForm.Visible)
        {
            _settingsForm.Hide();
            await Task.Delay(100); 
        }

        CaptureScreenAction();
    }

    private void CaptureScreenAction()
    {
        _isCapturing = true;

        try
        {
            while (true)
            {
                var vs = SystemInformation.VirtualScreen;
                using var fullScreenBmp = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(fullScreenBmp))
                {
                    g.CopyFromScreen(vs.X, vs.Y, 0, 0, vs.Size, CopyPixelOperation.SourceCopy);
                }

                using var cropForm = new CropForm(fullScreenBmp);
                cropForm.ShowDialog();

                if (cropForm.WasCancelled)
                {
                    break;
                }

                var region = cropForm.SelectedRegion;

                // Minimum alan kontrolü (5x5)
                if (region.Width < 5 || region.Height < 5)
                {
                    var result = MessageBox.Show(
                        "Seçilen alan çok küçük. Yeniden denemek ister misiniz?", 
                        "Uyarı", 
                        MessageBoxButtons.RetryCancel, 
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1,
                        MessageBoxOptions.DefaultDesktopOnly);

                    if (result == DialogResult.Retry)
                    {
                        continue; // Döngü başına dön, yeniden al
                    }
                    else
                    {
                        break; // İptal et
                    }
                }

                // Görüntüyü kırp ve kaydet
                using var croppedBmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(croppedBmp))
                {
                    g.DrawImage(fullScreenBmp, new Rectangle(0, 0, region.Width, region.Height), region, GraphicsUnit.Pixel);
                }

                SaveAndNotify(croppedBmp);
                break; // Başarılı, döngüden çık
            }
        }
        finally
        {
            _isCapturing = false;
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.Show();
            }
        }
    }

    private void SaveAndNotify(Bitmap bmp)
    {
        string dateFolder = DateTime.Now.ToString("dd.MM.yyyy");
        string saveDir;

        // Dinamik Klasörleme
        if (_settings.SaveFolder.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), StringComparison.OrdinalIgnoreCase))
        {
            saveDir = Path.Combine(_settings.SaveFolder, "EkranAlıntısı", dateFolder);
        }
        else
        {
            saveDir = Path.Combine(_settings.SaveFolder, dateFolder);
        }

        Directory.CreateDirectory(saveDir);

        string fileName;
        if (_settings.NamingMode == NamingMode.Timestamp)
        {
            fileName = $"ekran_{DateTime.Now:HH-mm-ss}.png";
        }
        else
        {
            // Sıralı Artan (Klasör taraması, JSON yok)
            int maxCounter = 0;
            string prefix = _settings.FilePrefix;
            var existingFiles = Directory.GetFiles(saveDir, $"{prefix}*.png");

            foreach (var file in existingFiles)
            {
                string nameOnly = Path.GetFileNameWithoutExtension(file);
                if (nameOnly.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string numStr = nameOnly.Substring(prefix.Length);
                    if (int.TryParse(numStr, out int num))
                    {
                        if (num > maxCounter) maxCounter = num;
                    }
                }
            }

            int nextCounter = maxCounter + 1;
            fileName = $"{prefix}{nextCounter}.png";
        }

        string fullPath = Path.Combine(saveDir, fileName);
        bmp.Save(fullPath, ImageFormat.Png);

        // Bildirim göster (Tag olarak yolu veriyoruz)
        _trayIcon.Tag = fullPath;
        _trayIcon.ShowBalloonTip(3000, "Görüntü Kaydedildi", fileName, ToolTipIcon.Info);
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        if (_trayIcon.Tag is string path && File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch { /* Ignored */ }
        }
    }

    private void Exit()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyManager.Dispose();
        Application.Exit();
    }
}
