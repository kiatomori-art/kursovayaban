using WinFormsApp1.Services;

namespace WinFormsApp1;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowStartupError(e.Exception);

        try
        {
            var data = new DataManager();
            data.LoadAll();
            Application.Run(new Form1(data));
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private static void ShowStartupError(Exception exception)
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinFormsApp1");
        string logPath = Path.Combine(logDirectory, "startup-error.txt");

        try
        {
            Directory.CreateDirectory(logDirectory);
            File.WriteAllText(logPath,
                $"Ошибка: {DateTime.Now:G}{Environment.NewLine}{exception}");
        }
        catch
        {
            logPath = "Не удалось создать файл журнала.";
        }

        MessageBox.Show(
            $"Программа не смогла запуститься.{Environment.NewLine}{Environment.NewLine}" +
            $"Причина: {exception.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"Подробности записаны в: {logPath}",
            "Ошибка запуска — WinFormsApp1",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
