using System.Diagnostics;

namespace EkranAlintisiAraci;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    
    // UI Elements
    private TextBox txtFolder;
    private Button btnBrowse;
    private RadioButton radTimestamp;
    private RadioButton radSequential;
    private TextBox txtPrefix;
    private ComboBox cmbHotkey;
    private Button btnSave;
    private Button btnCancel;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        
        Text = "Ekran Alıntısı Aracı - Ayarlar";
        Size = new Size(400, 360);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);

        InitializeComponents();
        LoadSettingsToUI();
    }

    private void InitializeComponents()
    {
        int y = 20;

        // 1. Klasör
        var lblFolder = new Label { Text = "Kayıt Klasörü:", Location = new Point(20, y), AutoSize = true };
        Controls.Add(lblFolder);
        y += 25;

        txtFolder = new TextBox { Location = new Point(20, y), Width = 280, ReadOnly = true };
        btnBrowse = new Button { Text = "Gözat...", Location = new Point(310, y - 1), Width = 55 };
        btnBrowse.Click += BtnBrowse_Click;
        Controls.Add(txtFolder);
        Controls.Add(btnBrowse);
        y += 40;

        // 2. İsimlendirme Modu
        var lblNaming = new Label { Text = "İsimlendirme Modu:", Location = new Point(20, y), AutoSize = true };
        Controls.Add(lblNaming);
        y += 25;

        radTimestamp = new RadioButton { Text = "Zaman Damgası (Örn: ekran_14-30-22.png)", Location = new Point(20, y), AutoSize = true };
        y += 25;
        radSequential = new RadioButton { Text = "Sıralı Artan", Location = new Point(20, y), AutoSize = true };
        
        radTimestamp.CheckedChanged += (s, e) => txtPrefix.Enabled = radSequential.Checked;
        
        Controls.Add(radTimestamp);
        Controls.Add(radSequential);
        y += 30;

        // 3. Ön Ad (Sıralı için)
        var lblPrefix = new Label { Text = "Dosya Ön Adı:", Location = new Point(40, y + 2), AutoSize = true };
        txtPrefix = new TextBox { Location = new Point(130, y), Width = 150 };
        Controls.Add(lblPrefix);
        Controls.Add(txtPrefix);
        y += 40;

        // 4. Kısayol Tuşu
        var lblHotkey = new Label { Text = "Kısayol Tuşu:", Location = new Point(20, y), AutoSize = true };
        Controls.Add(lblHotkey);
        y += 25;

        cmbHotkey = new ComboBox { Location = new Point(20, y), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        PopulateHotkeys();
        Controls.Add(cmbHotkey);
        y += 50;

        // Çizgi
        var sep = new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(20, y), Size = new Size(345, 2) };
        Controls.Add(sep);
        y += 20;

        // Butonlar
        btnSave = new Button { Text = "Kaydet", Location = new Point(205, y), Width = 80, BackColor = SystemColors.Control };
        btnSave.Click += BtnSave_Click;
        
        btnCancel = new Button { Text = "İptal", Location = new Point(290, y), Width = 80 };
        btnCancel.Click += (s, e) => Close();

        Controls.Add(btnSave);
        Controls.Add(btnCancel);
    }

    private void PopulateHotkeys()
    {
        // Sadece izin verilenleri listele
        cmbHotkey.Items.Add("PrintScreen");
        for (int i = 1; i <= 12; i++)
        {
            cmbHotkey.Items.Add($"F{i}");
        }
    }

    private void LoadSettingsToUI()
    {
        txtFolder.Text = _settings.SaveFolder;
        
        if (_settings.NamingMode == NamingMode.Timestamp)
            radTimestamp.Checked = true;
        else
            radSequential.Checked = true;

        txtPrefix.Text = _settings.FilePrefix;
        
        if (cmbHotkey.Items.Contains(_settings.HotkeyName))
            cmbHotkey.SelectedItem = _settings.HotkeyName;
        else
            cmbHotkey.SelectedItem = "F9"; // Fallback
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Ekran görüntüleri nereye kaydedilsin?",
            SelectedPath = txtFolder.Text
        };

        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            txtFolder.Text = fbd.SelectedPath;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // UI'dan değerleri al
        _settings.SaveFolder = txtFolder.Text;
        _settings.NamingMode = radSequential.Checked ? NamingMode.Sequential : NamingMode.Timestamp;
        _settings.FilePrefix = txtPrefix.Text.Trim() == "" ? "ad_" : txtPrefix.Text.Trim();
        _settings.HotkeyName = cmbHotkey.SelectedItem?.ToString() ?? "F9";

        // JSON Kaydet
        _settings.Save();
        
        // Uygulamaya kapatıldığını bildir, böylece kısayol güncellenir
        DialogResult = DialogResult.OK;
        Close();
    }
}
