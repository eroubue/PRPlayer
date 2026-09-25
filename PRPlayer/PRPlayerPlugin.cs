using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using PRPlayer.Media;
using PromeRotation.Plugins;

namespace PRPlayer;

[PromePlugin("PRPlayer", "PRPlayer", "Nag0mi",
    "Media playback for other plugins and ACRs (audio now, video reserved).",
    "0.1.0.0")]
public sealed class PRPlayerPlugin : IPromePlugin, IPromeAsyncPlugin
{
    private static readonly string[] CategoryNames = ["音效", "BGM", "语音", "系统音", "环境音"];

    private readonly AudioEngine engine = new();
    private PRPlayerIpc? ipc;
    private GameVolumeSource? gameVolume;
    private Configuration config = new();
    private string testPath = string.Empty;

    /// <summary>供同进程直接引用场景使用的播放器实例。</summary>
    public IMediaPlayer Player => engine;

    public void Initialize()
    {
        config = Configuration.Load();
        engine.MasterVolume = config.MasterVolume;
        ApplyFollowGameVolume(config.FollowGameVolume);
        if (gameVolume != null)
            gameVolume.Category = config.GameVolumeCategory;
        ipc = new PRPlayerIpc(engine);
    }

    public void DrawConfigUI()
    {
        var volume = engine.MasterVolume;
        if (ImGui.SliderFloat("主音量", ref volume, 0f, 1f))
        {
            engine.MasterVolume = volume;
            config.MasterVolume = volume;
            config.Save();
        }

        var follow = gameVolume?.Enabled ?? false;
        if (ImGui.Checkbox("跟随游戏音量设置", ref follow))
        {
            config.FollowGameVolume = follow;
            config.Save();
            ApplyFollowGameVolume(follow);
        }

        if (gameVolume != null)
        {
            var categoryIndex = (int)gameVolume.Category;
            if (ImGui.Combo("跟随声道", ref categoryIndex, CategoryNames, CategoryNames.Length))
            {
                gameVolume.Category = (GameVolumeCategory)categoryIndex;
                config.GameVolumeCategory = gameVolume.Category;
                config.Save();
            }
            ImGui.Text($"当前游戏音量系数: {gameVolume.Scale:0.00}");
        }

        ImGui.Separator();
        ImGui.Text("测试播放");
        ImGui.InputText("音频文件路径", ref testPath, 512);
        if (ImGui.Button("播放"))
            engine.PlayAudio(testPath);
        ImGui.SameLine();
        if (ImGui.Button("全部停止"))
            engine.StopAll();

        ImGui.Text($"活跃通道: {engine.ActiveCount}");
    }

    private void ApplyFollowGameVolume(bool follow)
    {
        if (follow && gameVolume == null)
        {
            gameVolume = new GameVolumeSource();
            engine.ExternalScale = gameVolume.Scale;
            gameVolume.ScaleChanged += scale => engine.ExternalScale = scale;
        }
        else if (!follow && gameVolume != null)
        {
            gameVolume.Dispose();
            gameVolume = null;
            engine.ExternalScale = 1f;
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        engine.StopAll();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        ipc?.Dispose();
        gameVolume?.Dispose();
        engine.Dispose();
    }
}
