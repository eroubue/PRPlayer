# PRPlayer

[PromeRotation](https://github.com/) 的媒体播放插件(Prome plugin),向其他 Prome 插件和 ACR(战斗循环)提供音频播放能力;视频播放 API 已预留,后续版本实现。

## 构建

```bash
dotnet build -c Debug
```

- 目标框架 `net10.0-windows7.0`,SDK 由 `global.json` 锁定(10.0.100,rollForward latestMajor)。
- 默认引用 `..\..\PromeRotation\PromeRotation\bin\Debug\` 下的 `PromeRotation.dll` 等宿主程序集;路径不同可用 `-p:PromeRotationDir="..."` 覆盖。
- 产物在 `PRPlayer/bin/x64/Debug/`:`PRPlayer.dll` + NAudio 依赖 + `PRPlayer.deps.json`。

## 安装

把构建产物整个拷到 PromeRotation 插件目录:

```
<PromeRotation 配置目录>/Plugins/PRPlayer/PRPlayer.dll
<PromeRotation 配置目录>/Plugins/PRPlayer/NAudio*.dll
<PromeRotation 配置目录>/Plugins/PRPlayer/PRPlayer.deps.json
```

然后在 PromeRotation 插件管理窗口中启用。注意:Release 模式下插件加载需通过宿主的网络验证,开发期请用 Debug 构建的宿主。

## IPC API(供其他插件 / ACR 调用)

前缀 `PRPlayer.`,通过 Dalamud IPC 调用,无需引用本程序集:

| 名称 | 签名 | 说明 |
|---|---|---|
| `PRPlayer.Play` | `(string path, float volume) -> int` | 播放音频,返回句柄,-1 失败 |
| `PRPlayer.Stop` | `(int handle)` | 停止指定句柄 |
| `PRPlayer.StopAll` | `()` | 停止全部 |
| `PRPlayer.Pause` | `(int handle)` | 暂停 |
| `PRPlayer.Resume` | `(int handle)` | 恢复 |
| `PRPlayer.SetVolume` | `(int handle, float volume)` | 单通道音量 0-1 |
| `PRPlayer.SetMasterVolume` | `(float volume)` | 主音量 0-1 |
| `PRPlayer.IsPlaying` | `(int handle) -> bool` | 是否正在播放 |
| `PRPlayer.GetActiveCount` | `() -> int` | 活跃通道数 |

ACR 调用示例:

```csharp
var play = Svc.PluginInterface.GetIpcSubscriber<string, float, int>("PRPlayer.Play");
var handle = play.InvokeFunc(@"D:\media\alert.mp3", 1.0f);
Svc.PluginInterface.GetIpcSubscriber<int, object>("PRPlayer.Stop").InvokeAction(handle);
```

支持格式:mp3 / wav / aac / m4a / wma 等 Windows Media Foundation 支持的格式(经 NAudio `MediaFoundationReader`)。

## 后续:视频播放

`IMediaPlayer.PlayVideo(string path, float volume)` 签名已固定,本期抛 `NotSupportedException`;实现后 ACR 侧无需改动。
