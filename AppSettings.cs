using System.Text.Json;
using System.Text.Json.Serialization;

namespace EkranAlintisiAraci;

// ─────────────────────────────────────────────────────────────────────────────
public enum NamingMode
{
    Timestamp  = 0,
    Sequential = 1
}

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Uygulama ayar modeli.
/// Load() → JSON varsa okur, yoksa varsayılanlar RAM'e alınır (diske yazmaz).
/// Save() → %LOCALAPPDATA%\EkranAlintisiAraci\ayarlar.json yoluna yazar.
/// </summary>
public sealed class AppSettings
{
    // ── Sabit Yollar ─────────────────────────────────────────────────────────
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EkranAlintisiAraci");

    private static readonly string JsonPath = Path.Combine(AppDataDir, "ayarlar.json");

    // ── Özellikler ───────────────────────────────────────────────────────────

    /// <summary>Görüntülerin kaydedileceği üst klasör yolu.</summary>
    public string SaveFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    /// <summary>Dosya adlandırma modu.</summary>
    public NamingMode NamingMode { get; set; } = NamingMode.Timestamp;

    /// <summary>
    /// Windows kullanıcı adından dosya sisteminde yasaklı karakterleri temizleyerek
    /// güvenli varsayılan dosya ön adı üretir (Örn: "omercoskun_").
    /// </summary>
    public static string GetDefaultPrefix()
    {
        string rawUser = Environment.UserName;
        return SanitizePrefix($"{rawUser}_", "kullanici_");
    }

    /// <summary>
    /// Verilen metinden Windows dosya adlarında hata verebilecek karakterleri (\ / : * ? " < > | vb.) temizler.
    /// </summary>
    public static string SanitizePrefix(string? input, string fallback = "kullanici_")
    {
        if (string.IsNullOrWhiteSpace(input))
            return fallback;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var cleanChars = input.Where(c => !invalidChars.Contains(c) && !char.IsControl(c)).ToArray();
        string clean = new string(cleanChars).Trim();

        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }

    private string _filePrefix = GetDefaultPrefix();

    /// <summary>Sıralı artan modda kullanılacak dosya ön adı.</summary>
    public string FilePrefix
    {
        get => _filePrefix;
        set => _filePrefix = SanitizePrefix(value, GetDefaultPrefix());
    }

    /// <summary>Çekilen görüntünün otomatik olarak panoya kopyalanıp kopyalanmayacağı.</summary>
    public bool CopyToClipboard { get; set; } = true;

    /// <summary>
    /// Kısayol tuşunun string adı (JSON'a yazılır).
    /// Örnek: "F9", "PrintScreen"
    /// </summary>
    public string HotkeyName { get; set; } = "F9";

    /// <summary>HotkeyName'den türetilen Keys değeri (JSON'a yazılmaz).</summary>
    [JsonIgnore]
    public Keys Hotkey
    {
        get => Enum.TryParse<Keys>(HotkeyName, ignoreCase: true, out var k) ? k : Keys.F9;
        set => HotkeyName = value.ToString();
    }

    // ── JSON Yükleme / Kayıt ─────────────────────────────────────────────────

    /// <summary>
    /// JSON dosyası varsa yükler; yoksa varsayılan ayarlarla döner
    /// (diske yazmaz – Sıfır Disk İzi prensibi).
    /// </summary>
    public static AppSettings Load()
    {
        if (!File.Exists(JsonPath))
            return new AppSettings();

        try
        {
            string json = File.ReadAllText(JsonPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // Bozuk JSON → varsayılan, diske yazma
            return new AppSettings();
        }
    }

    /// <summary>
    /// Kullanıcı "Kaydet" butonuna bastığında çağrılır.
    /// Klasörü gerekirse oluşturur.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(JsonPath, json);
    }

    /// <summary>Derin kopya döndürür.</summary>
    public AppSettings Clone() => new()
    {
        SaveFolder      = SaveFolder,
        NamingMode      = NamingMode,
        FilePrefix      = FilePrefix,
        HotkeyName      = HotkeyName,
        CopyToClipboard = CopyToClipboard
    };

    /// <summary>Kaynaktan bu nesneye kopyalar.</summary>
    public void CopyFrom(AppSettings src)
    {
        SaveFolder      = src.SaveFolder;
        NamingMode      = src.NamingMode;
        FilePrefix      = src.FilePrefix;
        HotkeyName      = src.HotkeyName;
        CopyToClipboard = src.CopyToClipboard;
    }
}
