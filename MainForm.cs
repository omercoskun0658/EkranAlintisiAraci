using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;

namespace EkranAlintisiAraci;

/// <summary>
/// Uygulamanın ana penceresi ve kontrol paneli.
/// Görev çubuğunda simgeyle görünür, hızlı ekran alıntısı alma,
/// ayarları yönetme ve kayıt klasörüne erişim imkanı sunar.
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly HotkeyManager _hotkeyManager;
    private readonly NotifyIcon _trayIcon;
    private bool _isCapturing;
    private bool _forceExit;
    private string? _lastSavedPath;

    private enum BalloonAction
    {
        OpenImage,
        RestoreApp
    }

    // UI Bileşenleri
    private Label _lblStatusBadge = null!;
    private Label _lblInfo = null!;
    private Button _btnCapture = null!;
    private Button _btnOpenFolder = null!;
    private Button _btnSettings = null!;
    private Button _btnExit = null!;

    public MainForm()
    {
        _settings = AppSettings.Load();

        // Form Ayarları
        Text = "Ekran Alıntısı Aracı";
        ClientSize = new Size(440, 330);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(248, 249, 250);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // İkonu ayarla (Görev çubuğu ve sol üst köşe için)
        try
        {
            Icon = TrayIconFactory.Create();
        }
        catch
        {
            // İkon oluşturulamazsa varsayılan devam etsin
        }

        // Sistem Tepsisi (Tray) İkonu
        _trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Visible = true,
            Text = "Ekran Alıntısı Aracı"
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Ana Pencereyi Aç", null, (s, e) => ShowAndRestore());
        trayMenu.Items.Add("Ekran Alıntısı Al", null, (s, e) => TriggerCapture());
        trayMenu.Items.Add("Kayıt Klasörünü Aç", null, (s, e) => OpenSaveFolder());
        trayMenu.Items.Add("Ayarlar", null, (s, e) => ShowSettings());
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Çıkış", null, (s, e) => FullExit());
        _trayIcon.ContextMenuStrip = trayMenu;

        _trayIcon.DoubleClick += (s, e) => ShowAndRestore();
        _trayIcon.BalloonTipClicked += OnBalloonTipClicked;

        // Kısayol Yöneticisi
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.HotkeyPressed += (s, e) => TriggerCapture();
        RegisterCurrentHotkey();

        // UI Tasarımı
        BuildUi();
        UpdateHotkeyBadge();
    }

    private void BuildUi()
    {
        // ── Üst Başlık ve Durum Kartı ─────────────────────────────────────────
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = Color.FromArgb(24, 32, 54),
            Padding = new Padding(20, 15, 20, 15)
        };

        var lblAppTitle = new Label
        {
            Text = "Ekran Alıntısı Aracı",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16)
        };

        _lblStatusBadge = new Label
        {
            Text = "Kısayol: [ F9 ]",
            ForeColor = Color.FromArgb(100, 210, 255),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 47)
        };

        topPanel.Controls.Add(lblAppTitle);
        topPanel.Controls.Add(_lblStatusBadge);
        Controls.Add(topPanel);

        // ── Ana Eylem Butonu (Ekran Alıntısı Al) ───────────────────────────────
        _btnCapture = new Button
        {
            Text = "📸  Yeni Ekran Alıntısı Al",
            Location = new Point(25, 102),
            Size = new Size(390, 48),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            BackColor = Color.FromArgb(41, 128, 243),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnCapture.FlatAppearance.BorderSize = 0;
        _btnCapture.Click += (s, e) => TriggerCapture();
        Controls.Add(_btnCapture);

        // ── İkincil Butonlar (Klasörü Aç & Ayarlar) ────────────────────────────
        _btnOpenFolder = new Button
        {
            Text = "📁  Kayıt Klasörü",
            Location = new Point(25, 160),
            Size = new Size(190, 40),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(40, 40, 40),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 220);
        _btnOpenFolder.Click += (s, e) => OpenSaveFolder();
        Controls.Add(_btnOpenFolder);

        _btnSettings = new Button
        {
            Text = "⚙️  Ayarlar",
            Location = new Point(225, 160),
            Size = new Size(190, 40),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(40, 40, 40),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnSettings.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 220);
        _btnSettings.Click += (s, e) => ShowSettings();
        Controls.Add(_btnSettings);

        // ── Alt Bilgi ve Çıkış ────────────────────────────────────────────────
        _lblInfo = new Label
        {
            Text = "Pencereyi kapattığınızda uygulama sistem tepsisinde çalışmaya devam eder.",
            ForeColor = Color.FromArgb(120, 120, 120),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(15, 206),
            Size = new Size(410, 36),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_lblInfo);

        _btnExit = new Button
        {
            Text = "Uygulamadan Tamamen Çık",
            Location = new Point(25, 246),
            Size = new Size(390, 30),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(180, 60, 60),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnExit.FlatAppearance.BorderSize = 0;
        _btnExit.Click += (s, e) => FullExit();
        Controls.Add(_btnExit);

        // ── Yapımcı Bilgisi ──────────────────────────────────────────────────
        var lblCopyright = new Label
        {
            Text = GetAuthorCopyrightText(),
            ForeColor = Color.FromArgb(150, 155, 165),
            Font = new Font("Segoe UI", 8f),
            Location = new Point(25, 288),
            Size = new Size(390, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(lblCopyright);
    }

    public static string GetAuthorCopyrightText()
    {
        int year = DateTime.Now.Year;
        return year <= 2026 ? "Ömer Coşkun 2026" : $"Ömer Coşkun 2026 - {year}";
    }

    private void UpdateHotkeyBadge()
    {
        _lblStatusBadge.Text = $"Kısayol Tuşu: [ {_settings.HotkeyName} ] • Sistem Hazır";
        _trayIcon.Text = $"Ekran Alıntısı Aracı ({_settings.HotkeyName})";
    }

    private void RegisterCurrentHotkey()
    {
        _hotkeyManager.Unregister();
        bool success = _hotkeyManager.Register(_settings.Hotkey);
        if (!success)
        {
            _trayIcon.Tag = null;
            _trayIcon.ShowBalloonTip(3000, "Kısayol Uyarısı", $"{_settings.HotkeyName} kısayolu rezerve edilemedi. Başka bir program kullanıyor olabilir.", ToolTipIcon.Warning);
        }
    }

    public void ShowAndRestore()
    {
        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
    }

    private async void TriggerCapture()
    {
        if (_isCapturing) return;

        bool wasVisible = Visible;
        if (wasVisible)
        {
            Hide();
            await Task.Delay(150); // Formun ekrandan tamamen silinmesi için kısa gecikme
        }

        CaptureScreenAction();

        if (wasVisible && !_forceExit)
        {
            ShowAndRestore();
        }
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
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                using var croppedBmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(croppedBmp))
                {
                    g.DrawImage(fullScreenBmp, new Rectangle(0, 0, region.Width, region.Height), region, GraphicsUnit.Pixel);
                }

                SaveAndNotify(croppedBmp);
                break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ekran alıntısı alınırken veya kaydedilirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isCapturing = false;
        }
    }

    private string GetDailySaveDirectory()
    {
        var trCulture = new CultureInfo("tr-TR");
        string yearFolder = DateTime.Now.ToString("yyyy");
        string monthFolder = DateTime.Now.ToString("MMMM", trCulture).ToUpper(trCulture);
        string dateFolder = DateTime.Now.ToString("dd.MM.yyyy");

        string baseDir;
        if (_settings.SaveFolder.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), StringComparison.OrdinalIgnoreCase))
        {
            baseDir = Path.Combine(_settings.SaveFolder, "EkranAlıntısı");
        }
        else
        {
            baseDir = _settings.SaveFolder;
        }

        return Path.Combine(baseDir, yearFolder, monthFolder, dateFolder);
    }

    private void SaveAndNotify(Bitmap bmp)
    {
        try
        {
            string saveDir = GetDailySaveDirectory();
            Directory.CreateDirectory(saveDir);

            string fileName;
            if (_settings.NamingMode == NamingMode.Timestamp)
            {
                string baseName = $"ekran_{DateTime.Now:HH-mm-ss}";
                fileName = $"{baseName}.png";
                int dupIndex = 1;
                while (File.Exists(Path.Combine(saveDir, fileName)))
                {
                    fileName = $"{baseName}_{dupIndex++}.png";
                }
            }
            else
            {
                int maxCounter = 0;
                string prefix = AppSettings.SanitizePrefix(_settings.FilePrefix, AppSettings.GetDefaultPrefix());
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

            // Panoya Kopyala
            if (_settings.CopyToClipboard)
            {
                try
                {
                    Clipboard.SetImage(bmp);
                }
                catch
                {
                    // Başka bir uygulamanın panoyu geçici kilitlemesini tolere et
                }
            }

            _lastSavedPath = fullPath;
            _trayIcon.Tag = BalloonAction.OpenImage;
            if (_settings.CopyToClipboard)
            {
                _trayIcon.ShowBalloonTip(3000, "Görüntü Kaydedildi & Kopyalandı", $"{fileName} (Ctrl+V ile yapıştırabilirsiniz)", ToolTipIcon.Info);
            }
            else
            {
                _trayIcon.ShowBalloonTip(3000, "Görüntü Kaydedildi", fileName, ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            _trayIcon.Tag = null;
            _trayIcon.ShowBalloonTip(4000, "Kayıt Hatası", $"Dosya kaydedilemedi: {ex.Message}", ToolTipIcon.Error);
            MessageBox.Show($"Ekran görüntüsü kaydedilirken hata oluştu:\n{ex.Message}\n\nKlasör: {_settings.SaveFolder}", "Kayıt Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSaveFolder()
    {
        try
        {
            string targetDir = GetDailySaveDirectory();

            if (!Directory.Exists(targetDir))
            {
                // Günlük klasör henüz oluşmadıysa sırasıyla üst klasörleri kontrol et
                string? monthDir = Path.GetDirectoryName(targetDir);
                string? yearDir = monthDir != null ? Path.GetDirectoryName(monthDir) : null;

                if (monthDir != null && Directory.Exists(monthDir))
                    targetDir = monthDir;
                else if (yearDir != null && Directory.Exists(yearDir))
                    targetDir = yearDir;
                else
                    targetDir = _settings.SaveFolder;
            }

            if (Directory.Exists(targetDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetDir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Klasör açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowSettings()
    {
        using var settingsForm = new SettingsForm(_settings);
        if (settingsForm.ShowDialog(this) == DialogResult.OK)
        {
            RegisterCurrentHotkey();
            UpdateHotkeyBadge();
        }
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        if (_trayIcon.Tag is BalloonAction action)
        {
            if (action == BalloonAction.RestoreApp)
            {
                // Bildirime tıklandığında ana pencereyi aç
                ShowAndRestore();
            }
            else if (action == BalloonAction.OpenImage && !string.IsNullOrEmpty(_lastSavedPath) && File.Exists(_lastSavedPath))
            {
                // Ekran görüntüsü bildirimine tıklandığında resmi aç
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _lastSavedPath,
                        UseShellExecute = true
                    });
                }
                catch { /* Ignored */ }
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_forceExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.Tag = BalloonAction.RestoreApp;
            _trayIcon.ShowBalloonTip(2000, "Ekran Alıntısı Aracı", "Uygulama arka planda çalışmaya devam ediyor. Açmak için tıklayın.", ToolTipIcon.Info);
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyManager.Dispose();

        base.OnFormClosing(e);
    }

    private void FullExit()
    {
        _forceExit = true;
        Close();
        Application.Exit();
    }
}
