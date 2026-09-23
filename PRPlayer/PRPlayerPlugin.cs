using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using PRPlayer.Media;
using PromeRotation.Plugins;

namespace PRPlayer;

[PromePlugin("PRPlayer", "PRPlayer", "WREN",
    "Media playback for other plugins and ACRs (audio now, video reserved).",
    "0.1.0.0")]
public sealed class PRPlayerPlugin : IPromePlugin, IPromeAsyncPlugin
{
    private readonly AudioEngine engine = new();
    private PRPlayerIpc? ipc;
    private Configuration config = new();
    private string testPath = string.Empty;

    /// <summary>供同进程直接引用场景使用的播放器实例。</summary>
    public IMediaPlayer Player => engine;

    public void Initialize()
    {
        config = Configuration.Load();
        engine.MasterVolume = config.MasterVolume;
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

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        engine.StopAll();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        ipc?.Dispose();
        engine.Dispose();
    }
}
