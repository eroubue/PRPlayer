using System;
using Dalamud.Game.Config;
using ECommons.DalamudServices;

namespace PRPlayer;

/// <summary>跟随游戏的哪个声道类别。</summary>
public enum GameVolumeCategory
{
    /// <summary>音效(Sound Effects)</summary>
    SoundEffects = 0,
    /// <summary>BGM</summary>
    Bgm = 1,
    /// <summary>语音(Voice)</summary>
    Voice = 2,
    /// <summary>系统音(System Sounds)</summary>
    System = 3,
    /// <summary>环境音(Ambient Sounds)</summary>
    Ambient = 4,
}

/// <summary>
/// 游戏原生音量源:读取游戏系统设置里的主音量与所选声道类别音量(含静音开关),
/// 合成 0-1 的系数,并监听游戏设置变更实时刷新。
/// 系数语义:主音量 × 声道音量,任一侧被静音则为 0。
/// </summary>
public sealed class GameVolumeSource : IDisposable
{
    private bool enabled = true;
    private GameVolumeCategory category = GameVolumeCategory.SoundEffects;

    public GameVolumeSource()
    {
        Svc.GameConfig.Changed += OnGameConfigChanged;
        Refresh();
    }

    public bool Enabled
    {
        get => enabled;
        set { enabled = value; Refresh(); }
    }

    public GameVolumeCategory Category
    {
        get => category;
        set { category = value; Refresh(); }
    }

    /// <summary>当前游戏音量系数(0-1)。未启用时恒为 1。</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>系数变化时触发。</summary>
    public event Action<float>? ScaleChanged;

    public void Refresh()
    {
        var scale = 1f;
        if (enabled)
        {
            try
            {
                scale = ReadMaster() * ReadCategory(category);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "[PRPlayer] 读取游戏音量设置失败,按 1.0 处理");
                scale = 1f;
            }
        }

        if (Math.Abs(scale - Scale) < 0.0001f)
            return;

        Scale = scale;
        try { ScaleChanged?.Invoke(scale); }
        catch (Exception ex) { Svc.Log.Error(ex, "[PRPlayer] ScaleChanged 回调异常"); }
    }

    public void Dispose() => Svc.GameConfig.Changed -= OnGameConfigChanged;

    private void OnGameConfigChanged(object? sender, ConfigChangeEvent e) => Refresh();

    private static float ReadMaster()
    {
        if (Svc.GameConfig.TryGet(SystemConfigOption.IsSndMaster, out bool muted) && muted)
            return 0f;
        return Svc.GameConfig.TryGet(SystemConfigOption.SoundMaster, out uint v)
            ? Math.Clamp(v / 100f, 0f, 1f)
            : 1f;
    }

    private static float ReadCategory(GameVolumeCategory cat)
    {
        var (volumeOption, muteOption) = cat switch
        {
            GameVolumeCategory.Bgm => (SystemConfigOption.SoundBgm, SystemConfigOption.IsSndBgm),
            GameVolumeCategory.Voice => (SystemConfigOption.SoundVoice, SystemConfigOption.IsSndVoice),
            GameVolumeCategory.System => (SystemConfigOption.SoundSystem, SystemConfigOption.IsSndSystem),
            GameVolumeCategory.Ambient => (SystemConfigOption.SoundEnv, SystemConfigOption.IsSndEnv),
            _ => (SystemConfigOption.SoundSe, SystemConfigOption.IsSndSe),
        };

        if (Svc.GameConfig.TryGet(muteOption, out bool muted) && muted)
            return 0f;
        return Svc.GameConfig.TryGet(volumeOption, out uint v)
            ? Math.Clamp(v / 100f, 0f, 1f)
            : 1f;
    }
}
