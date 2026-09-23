using System;
using System.Collections.Generic;
using ECommons.DalamudServices;
using PRPlayer.Media;

namespace PRPlayer;

// IPC 前缀:PRPlayer.
// Play(string path, float volume) -> int          播放音频,返回句柄(-1 失败)
// Stop(int handle)                                停止指定句柄
// StopAll()                                       停止全部
// Pause(int handle) / Resume(int handle)          暂停/恢复
// SetVolume(int handle, float volume)             单通道音量(0-1)
// SetMasterVolume(float volume)                   主音量(0-1)
// IsPlaying(int handle) -> bool
// GetActiveCount() -> int
//
// ACR 调用示例:
//   var play = Svc.PluginInterface.GetIpcSubscriber<string, float, int>("PRPlayer.Play");
//   var handle = play.InvokeFunc(@"D:\media\alert.mp3", 1.0f);
//   Svc.PluginInterface.GetIpcSubscriber<int, object>("PRPlayer.Stop").InvokeAction(handle);
internal sealed class PRPlayerIpc : IDisposable
{
    private const string Prefix = "PRPlayer.";

    private readonly IMediaPlayer player;
    private readonly List<Action> disposeActions = new();

    public PRPlayerIpc(IMediaPlayer player)
    {
        this.player = player;

        RegisterFunc<string, float, int>("Play", player.PlayAudio);
        RegisterAction<int>("Stop", player.Stop);
        RegisterAction("StopAll", player.StopAll);
        RegisterAction<int>("Pause", player.Pause);
        RegisterAction<int>("Resume", player.Resume);
        RegisterAction<int, float>("SetVolume", player.SetVolume);
        RegisterAction<float>("SetMasterVolume", v => player.MasterVolume = v);
        RegisterFunc<int, bool>("IsPlaying", player.IsPlaying);
        RegisterFunc<int>("GetActiveCount", () => player.ActiveCount);
    }

    public void Dispose()
    {
        foreach (var action in disposeActions)
            action();
        disposeActions.Clear();
    }

    private void RegisterAction(string name, Action action)
    {
        var provider = Svc.PluginInterface.GetIpcProvider<object>(Prefix + name);
        provider.RegisterAction(action);
        disposeActions.Add(provider.UnregisterAction);
    }

    private void RegisterAction<T1>(string name, Action<T1> action)
    {
        var provider = Svc.PluginInterface.GetIpcProvider<T1, object>(Prefix + name);
        provider.RegisterAction(action);
        disposeActions.Add(provider.UnregisterAction);
    }

    private void RegisterAction<T1, T2>(string name, Action<T1, T2> action)
    {
        var provider = Svc.PluginInterface.GetIpcProvider<T1, T2, object>(Prefix + name);
        provider.RegisterAction(action);
        disposeActions.Add(provider.UnregisterAction);
    }

    private void RegisterFunc<TRet>(string name, Func<TRet> func)
    {
        var provider = Svc.PluginInterface.GetIpcProvider<TRet>(Prefix + name);
        provider.RegisterFunc(func);
        disposeActions.Add(provider.UnregisterFunc);
    }

    private void RegisterFunc<T1, TRet>(string name, Func<T1, TRet> func)
    {
        var provider = Svc.PluginInterface.GetIpcProvider<T1, TRet>(Prefix + name);
        provider.RegisterFunc(func);
        disposeActions.Add(provider.UnregisterFunc);
    }

    private void RegisterFunc<T1, T2, TRet>(string name, Func<T1, T2, TRet> func)
    {
        var provider = Svc.PluginInterface.GetIpcProvider<T1, T2, TRet>(Prefix + name);
        provider.RegisterFunc(func);
        disposeActions.Add(provider.UnregisterFunc);
    }
}
