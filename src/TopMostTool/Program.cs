namespace TopMostTool;

internal static class Program
{
    private const string MutexName = @"Local\TopMostTool.38A338CC-B6A4-44F2-AF8B-CB67EF6A30B4";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "工具已在运行，可从系统托盘打开菜单。",
                "窗口置顶",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatalError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatalError(e.ExceptionObject as Exception ?? new Exception("发生未知运行错误。"));

        try
        {
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static void ShowFatalError(Exception exception)
    {
        MessageBox.Show(
            $"运行出错：{exception.Message}",
            "窗口置顶",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
