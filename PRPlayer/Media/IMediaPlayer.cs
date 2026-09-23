using System;

namespace PRPlayer.Media;

/// <summary>
/// PRPlayer 的公共播放接口。其他 Prome 插件 / ACR 一般应通过 Dalamud IPC
/// (前缀 "PRPlayer.") 调用,而不是直接引用本程序集;本接口用于直接引用场景
/// 以及未来视频实现的 API 面稳定。
/// </summary>
public interface IMediaPlayer : IDisposable
{
    /// <summary>播放音频文件(mp3/wav/aac/m4a/wma 等 Media Foundation 支持的格式)。返回播放句柄,失败返回 -1。</summary>
    int PlayAudio(string path, float volume = 1.0f);

    /// <summary>
    /// 播放视频文件(预留,当前版本未实现,调用抛出 <see cref="NotSupportedException"/>)。
    /// 签名已固定,后续版本实现渲染后 ACR 无需改动。
    /// </summary>
    int PlayVideo(string path, float volume = 1.0f);

    /// <summary>停止指定句柄的播放。</summary>
    void Stop(int handle);

    /// <summary>停止所有播放。</summary>
    void StopAll();

    /// <summary>暂停指定句柄。</summary>
    void Pause(int handle);

    /// <summary>恢复指定句柄。</summary>
    void Resume(int handle);

    /// <summary>设置指定句柄的音量(0.0 - 1.0,与主音量叠乘)。</summary>
    void SetVolume(int handle, float volume);

    /// <summary>主音量(0.0 - 1.0),作用于所有通道。</summary>
    float MasterVolume { get; set; }

    /// <summary>指定句柄是否正在播放。</summary>
    bool IsPlaying(int handle);

    /// <summary>当前活跃通道数。</summary>
    int ActiveCount { get; }

    /// <summary>播放自然结束或被停止时触发,参数为句柄。在 NAudio 播放线程上触发,勿做重活。</summary>
    event Action<int>? PlaybackFinished;
}
