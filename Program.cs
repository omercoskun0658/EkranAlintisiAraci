namespace EkranAlintisiAraci;

static class Program
{
    private const string AppMutexName = "EkranAlintisiAraci_SingleInstance_Mutex";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, AppMutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "Ekran Alıntısı Aracı zaten çalışıyor.\nGörev çubuğunda veya saat yanındaki sistem tepsisinde bulabilirsiniz.",
                "Uygulama Zaten Açık",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
