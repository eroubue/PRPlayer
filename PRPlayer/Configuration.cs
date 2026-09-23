using System;
using System.IO;
using System.Text.Json;
using ECommons.DalamudServices;

namespace PRPlayer;

public sealed class Configuration
{
    public float MasterVolume { get; set; } = 1.0f;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string ConfigPath =>
        Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "PRPlayer.json");

    public static Configuration Load()
    {
        try
        {
            var path = ConfigPath;
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path)) ?? new Configuration();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[PRPlayer] 配置读取失败,使用默认值");
        }
        return new Configuration();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[PRPlayer] 配置保存失败");
        }
    }
}
