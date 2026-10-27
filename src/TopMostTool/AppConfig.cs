using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopMostTool;

internal sealed class AppConfig
{
public HotKeyBinding Shortcut { get; set; } = HotKeyBinding.Default;
    public HotKeyBinding ScreenshotShortcut { get; set; } = HotKeyBinding.ScreenshotDefault;
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public string LastAction { get; set; } = "None";
}

internal sealed class ConfigStore
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal string ConfigPath { get; }

    internal ConfigStore(string? configPath = null)
    {
        ConfigPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TopMostTool", "config.json");
    }

    internal AppConfig Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            string json = File.ReadAllText(ConfigPath);
            AppConfig config = JsonSerializer.Deserialize<AppConfig>(json, _options) ?? new AppConfig();
            if (config.Shortcut is null || !config.Shortcut.IsValid || config.Shortcut.ConflictsWithCancel)
            {
                config.Shortcut = HotKeyBinding.Default;
                warning = "窗口置顶快捷键无效，已恢复默认组合 Ctrl + Space。";
            }
            if (config.ScreenshotShortcut is null || !config.ScreenshotShortcut.IsValid ||
                config.ScreenshotShortcut.ConflictsWithCancel ||
                config.ScreenshotShortcut == config.Shortcut)
            {
                config.ScreenshotShortcut = config.Shortcut == HotKeyBinding.ScreenshotDefault
                    ? HotKeyBinding.ScreenshotDefault with
                    {
                        Modifiers = NativeMethods.HotKeyModifiers.Control |
                            NativeMethods.HotKeyModifiers.Alt | NativeMethods.HotKeyModifiers.Shift
                    }
                    : HotKeyBinding.ScreenshotDefault;
            }

            return config;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = $"设置读取失败，已使用默认设置。\n{exception.Message}";
            return new AppConfig();
        }
    }

    internal bool TrySave(AppConfig config, out string? error)
    {
        try
        {
            string? directory = Path.GetDirectoryName(ConfigPath);
            if (directory is null)
            {
                throw new IOException("无法找到用于保存设置的目录。");
            }

            Directory.CreateDirectory(directory);
            string tempPath = ConfigPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(config, _options));
            File.Move(tempPath, ConfigPath, true);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }
}
