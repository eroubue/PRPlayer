using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ECommons.DalamudServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace PRPlayer.Media;

/// <summary>
/// 基于 NAudio 的音频引擎。每个播放请求一个独立 WaveOutEvent + MediaFoundationReader
/// 通道,支持多路并发。WaveOutEvent 自带播放线程,无需额外线程管理;
/// 卸载时由 Dispose/StopAll 全部释放。
/// </summary>
public sealed class AudioEngine : IMediaPlayer
{
    private sealed class Channel
    {
        public required WaveOutEvent Output;
        public required MediaFoundationReader Reader;
        public required VolumeSampleProvider VolumeProvider;
        public float Volume = 1.0f;
    }

    private readonly object gate = new();
    private readonly Dictionary<int, Channel> channels = new();
    private int nextHandle;
    private float masterVolume = 1.0f;
    private bool disposed;

    public event Action<int>? PlaybackFinished;

    public float MasterVolume
    {
        get { lock (gate) return masterVolume; }
        set
        {
            value = Math.Clamp(value, 0f, 1f);
            lock (gate)
            {
                masterVolume = value;
                foreach (var ch in channels.Values)
                    ApplyVolume(ch);
            }
        }
    }

    public int ActiveCount
    {
        get { lock (gate) return channels.Count; }
    }

    public int PlayAudio(string path, float volume = 1.0f)
    {
        if (disposed) return -1;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Svc.Log.Warning($"[PRPlayer] 音频文件不存在: {path}");
            return -1;
        }

        try
        {
            var reader = new MediaFoundationReader(path);
            var volumeProvider = new VolumeSampleProvider(reader.ToSampleProvider());
            var output = new WaveOutEvent();
            output.Init(volumeProvider.ToWaveProvider());

            var handle = Interlocked.Increment(ref nextHandle);
            var channel = new Channel
            {
                Output = output,
                Reader = reader,
                VolumeProvider = volumeProvider,
                Volume = Math.Clamp(volume, 0f, 1f),
            };
            ApplyVolume(channel);

            output.PlaybackStopped += (_, _) => OnPlaybackStopped(handle);

            lock (gate)
                channels[handle] = channel;

            output.Play();
            return handle;
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[PRPlayer] 播放失败: {path}");
            return -1;
        }
    }

    public int PlayVideo(string path, float volume = 1.0f)
        => throw new NotSupportedException("视频播放将在后续版本提供,本期仅音频。");

    public void Stop(int handle)
    {
        Channel? ch;
        lock (gate)
            channels.TryGetValue(handle, out ch);
        // PlaybackStopped 会负责移除与释放
        ch?.Output.Stop();
    }

    public void StopAll()
    {
        List<Channel> snapshot;
        lock (gate)
            snapshot = new List<Channel>(channels.Values);
        foreach (var ch in snapshot)
            ch.Output.Stop();
    }

    public void Pause(int handle)
    {
        lock (gate)
            if (channels.TryGetValue(handle, out var ch))
                ch.Output.Pause();
    }

    public void Resume(int handle)
    {
        lock (gate)
            if (channels.TryGetValue(handle, out var ch))
                ch.Output.Play();
    }

    public void SetVolume(int handle, float volume)
    {
        lock (gate)
        {
            if (channels.TryGetValue(handle, out var ch))
            {
                ch.Volume = Math.Clamp(volume, 0f, 1f);
                ApplyVolume(ch);
            }
        }
    }

    public bool IsPlaying(int handle)
    {
        lock (gate)
            return channels.TryGetValue(handle, out var ch)
                && ch.Output.PlaybackState == PlaybackState.Playing;
    }

    private void ApplyVolume(Channel ch)
        => ch.VolumeProvider.Volume = Math.Clamp(ch.Volume * masterVolume, 0f, 1f);

    private void OnPlaybackStopped(int handle)
    {
        Channel? ch;
        lock (gate)
        {
            if (!channels.Remove(handle, out ch))
                return;
        }

        try { ch.Output.Dispose(); } catch { /* 卸载竞态下忽略 */ }
        try { ch.Reader.Dispose(); } catch { }

        try { PlaybackFinished?.Invoke(handle); }
        catch (Exception ex) { Svc.Log.Error(ex, "[PRPlayer] PlaybackFinished 回调异常"); }
    }

    public void Dispose()
    {
        disposed = true;
        StopAll();

        // Stop 是异步的(PlaybackStopped 在线程池回调),给释放一点宽限再兜底清理
        var deadline = Environment.TickCount64 + 1000;
        while (ActiveCount > 0 && Environment.TickCount64 < deadline)
            Thread.Sleep(10);

        lock (gate)
        {
            foreach (var ch in channels.Values)
            {
                try { ch.Output.Dispose(); } catch { }
                try { ch.Reader.Dispose(); } catch { }
            }
            channels.Clear();
        }
    }
}
