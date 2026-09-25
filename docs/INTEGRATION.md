# PRPlayer 开发接入文档

面向 PromeRotation 插件与 ACR(战斗循环)开发者:如何在自己的代码里调用 PRPlayer 播放音频。

## 1. 概述

PRPlayer 是 PromeRotation 的二级插件(Prome plugin),运行在 PromeRotation 进程内,基于 NAudio + Windows Media Foundation 提供多路并发音频播放。它对外暴露 **Dalamud IPC** 接口,任何 Prome 插件或 ACR 都可以调用,**无需引用 PRPlayer 程序集**。

- IPC 前缀:`PRPlayer.`
- 支持格式:mp3 / wav / aac / m4a / wma 等 Media Foundation 支持的格式
- 并发:每次播放分配独立通道与句柄,路数不限
- 视频:`PlayVideo` 签名已预留,当前版本抛 `NotSupportedException`,实现后调用方无需改动

## 2. 前置条件

1. 用户已在 `<PromeRotation 配置目录>/Plugins/PRPlayer/` 安装并启用 PRPlayer。
2. 音频文件使用**绝对路径**,且游戏进程可读。
3. 调用前建议检测 IPC 是否可用(PRPlayer 未安装/未启用时不应报错刷屏),见 §5 的封装示例。

## 3. IPC API 参考

所有接口经 `Svc.PluginInterface.GetIpcSubscriber<...>("PRPlayer.<名称>")` 获取。

### `PRPlayer.Play` — 播放音频

```csharp
GetIpcSubscriber<string, float, int>("PRPlayer.Play")
```

| 参数 | 类型 | 说明 |
|---|---|---|
| `path` | `string` | 音频文件绝对路径 |
| `volume` | `float` | 通道音量,0.0–1.0,与主音量叠乘 |
| 返回 | `int` | 播放句柄;**-1 表示失败**(文件不存在/解码失败/插件已卸载) |

### `PRPlayer.Stop` — 停止指定句柄

```csharp
GetIpcSubscriber<int, object>("PRPlayer.Stop")
```

句柄无效或已结束时为无操作,不抛异常。

### `PRPlayer.StopAll` — 停止全部

```csharp
GetIpcSubscriber<object>("PRPlayer.StopAll")
```

### `PRPlayer.Pause` / `PRPlayer.Resume` — 暂停 / 恢复

```csharp
GetIpcSubscriber<int, object>("PRPlayer.Pause")
GetIpcSubscriber<int, object>("PRPlayer.Resume")
```

### `PRPlayer.SetVolume` — 单通道音量

```csharp
GetIpcSubscriber<int, float, object>("PRPlayer.SetVolume")
```

实时生效,范围 0.0–1.0。

### `PRPlayer.SetMasterVolume` — 主音量

```csharp
GetIpcSubscriber<float, object>("PRPlayer.SetMasterVolume")
```

作用于所有通道,范围 0.0–1.0。注意:用户在 PRPlayer 配置界面里的音量滑条写的也是这个值。

### `PRPlayer.IsPlaying` — 查询状态

```csharp
GetIpcSubscriber<int, bool>("PRPlayer.IsPlaying")
```

句柄不存在、已暂停或已结束均返回 `false`。

### `PRPlayer.GetActiveCount` — 活跃通道数

```csharp
GetIpcSubscriber<int>("PRPlayer.GetActiveCount")
```

## 4. 句柄语义与生命周期

- 句柄是单调递增的 `int`,一次播放一个句柄。
- 播放**自然结束**或被 `Stop` 后,句柄立即失效;之后对它的任何操作都是安全无操作。
- `Stop` 是异步生效(底层在播放线程上停止),通常毫秒内完成;`IsPlaying` 在停止完成后返回 `false`。
- 调用方**不需要**显式释放句柄,PRPlayer 自行回收。
- PRPlayer 被卸载/禁用时会停止所有播放并注销全部 IPC,此后 `InvokeFunc` 会抛 `IpcNotReadyError` —— 务必用 §5 的封装或自行 try/catch。

## 5. 推荐:复制即用的调用封装

在你的 ACR / 插件里放这样一个静态类,处理 IPC 不可用、异常兜底:

```csharp
using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

public static class PRPlayerApi
{
    private static ICallGateSubscriber<string, float, int>? play;
    private static ICallGateSubscriber<int, object>? stop;
    private static ICallGateSubscriber<object>? stopAll;

    // 在插件/ACR 初始化时调用一次,传入你的 IDalamudPluginInterface
    public static void Init(IDalamudPluginInterface pi)
    {
        play    = pi.GetIpcSubscriber<string, float, int>("PRPlayer.Play");
        stop    = pi.GetIpcSubscriber<int, object>("PRPlayer.Stop");
        stopAll = pi.GetIpcSubscriber<object>("PRPlayer.StopAll");
    }

    /// <summary>播放音频,返回句柄;PRPlayer 不可用或失败时返回 -1。</summary>
    public static int Play(string path, float volume = 1.0f)
    {
        try { return play?.InvokeFunc(path, volume) ?? -1; }
        catch { return -1; }   // IPC 未就绪 / PRPlayer 已卸载
    }

    public static void Stop(int handle)
    {
        if (handle < 0) return;
        try { stop?.InvokeAction(handle); } catch { }
    }

    public static void StopAll()
    {
        try { stopAll?.InvokeAction(); } catch { }
    }
}
```

> ACR 场景:`IDalamudPluginInterface` 可通过 `ECommons.DalamudServices.Svc.PluginInterface` 直接拿到(ACR 运行在 PromeRotation 进程内,`Svc` 已由宿主初始化)。

## 6. ACR 接入示例

战斗开始时播放提示音,脱战/团灭时停掉(事件名为 `IRotationEventHandler` 的真实成员):

```csharp
public class MyRotation : IRotation
{
    internal int BgmHandle = -1;

    public IRotationEventHandler GetEventHandler() => new MyEventHandler(this);

    private sealed class MyEventHandler(MyRotation rot) : IRotationEventHandler
    {
        public void OnBattleStarted()
        {
            rot.BgmHandle = PRPlayerApi.Play(@"D:\media\battle.mp3", 0.8f);
        }

        public void OnBattleEnded()
        {
            PRPlayerApi.Stop(rot.BgmHandle);
            rot.BgmHandle = -1;
        }

        public void OnUpdate() { }
        public void OnOutOfBattleUpdate() { }
        public void OnBattleUpdate() { }
        public void OnNoTarget() { }
        public void OnTerritoryChanged(ushort territoryId) { }
    }

    // ... NextGcd / NextOffGcd 等略
}
```

一次性播报(如机制提醒)更简单,句柄都不用存:

```csharp
PRPlayerApi.Play(@"D:\media\spread.mp3");   // 播完自动回收
```

## 7. 音量模型

最终音量 = `主音量(MasterVolume)` × `通道音量(Play 的 volume / SetVolume)` × `游戏音量系数`。

- 主音量是用户在 PRPlayer 配置界面控制的全局开关,**不要**用 `SetMasterVolume` 覆盖用户设置,除非你做的就是音量管理插件。
- 单个提示音想小声一点,用 `Play(path, 0.5f)`。
- **游戏音量系数**(默认开启):跟随游戏"系统设置 → 音量"中的主音量与所选声道(默认"音效")音量,静音开关同样生效;用户在游戏里调音量,PRPlayer 的播放实时跟随,调用方**不需要**为此做任何事,也不要自己再去读游戏音量做补偿。
- 用户可在 PRPlayer 配置界面关闭跟随,或选择跟随的声道类别(音效 / BGM / 语音 / 系统音 / 环境音)。

## 8. 线程与回调注意事项

- 所有 IPC 都是**线程安全**的,可以从游戏主线程、Framework.Update 或后台线程调用。
- `Play` 只负责启动播放,立即返回,不阻塞。
- `IMediaPlayer.PlaybackFinished` 事件只对直接引用场景开放(见 §9),在 NAudio 播放线程触发,回调里不要做重活;IPC 场景无此事件,需要"播完做某事"请轮询 `IsPlaying` 或按已知时长延时。

## 9. 备选:直接程序集引用(不推荐)

如果与 PRPlayer 同属一个插件包,也可以直接引用 `PRPlayer.dll`,经
`PromePluginRegistry.FindById("PRPlayer")?.Instance` 拿到插件实例再访问 `Player`(`IMediaPlayer`)。
此方式要求调用方与 PRPlayer 解析到**同一份程序集**(宿主 ALC 共享列表之外各自带一份 DLL 会造成类型身份分裂),跨插件/ACR 场景请一律用 IPC。

## 10. 故障排查

| 现象 | 排查 |
|---|---|
| `Play` 返回 -1 | 路径是否绝对路径、文件是否存在、格式是否被 MF 支持;查 Dalamud 日志(`/xllog` 过滤 `PRPlayer`) |
| 调用抛 `IpcNotReadyError` | PRPlayer 未安装/未启用/已卸载;用 §5 封装兜底 |
| 没有声音 | 主音量是否为 0(用户可能在 PRPlayer 界面调了);游戏音频设备是否被独占;用 PRPlayer 配置界面的"测试播放"验证 |
| 插件不加载 | Release 模式下宿主有网络验证,开发期用 Debug 构建的宿主 |

## 11. 视频播放(路线图)

`IMediaPlayer.PlayVideo(string path, float volume)` 签名已固定,本期抛 `NotSupportedException`。
视频实现落地时会同步增加 `PRPlayer.PlayVideo` IPC 接口,签名保持 `(string, float) -> int`,届时接入方式与音频完全一致。
