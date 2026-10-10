# 第三方组件与来源

本项目以 **GPL-3.0-only** 发布，见 `LICENSE`。下列组件随本项目分发或参与构建，各自保留其原始许可。

## 复用的源码

| 来源 | 许可 | 复用范围 |
|---|---|---|
| [BetterGI](https://github.com/babalae/better-genshin-impact) | GPL-3.0 | `Fischless.GameCapture`、`Fischless.HotkeyCapture` 两个工程整体复制 |
| [Fischless](https://github.com/Fischless) | MIT，见 `Fischless.WindowsInput/LICENSE.txt` | `Fischless.WindowsInput` 整体复制（另两个工程由 BetterGI 在其基础上改写） |

对复用文件所做的修改共 3 处：

- 三个 `.csproj` 的包引用版本改由 `Directory.Packages.props` 集中管理；
- `Fischless.GameCapture/Graphics/GraphicsCaptureV2.cs` 增加 `IsHdrActive`，暴露实际生效的 HDR 状态；
- `Fischless.GameCapture/Graphics/GraphicsCaptureV2.cs` 的构造函数增加可选参数 `hdrToSdrShader`，为空时用上游自带的 `HdrToSdrShader.Content`，非空时用调用方给的源码（本项目按显示器 SDR 白电平算出曝光系数后注入）。

上游里的 `BitBlt`、`DWM SharedSurface`、WGC v1 三个采集实现本项目不接入，但保留原样、不删除也不修改。

除 `Fischless.WindowsInput` 外，`Fischless.*` 在 BetterGI 仓库内以 GPL-3.0 分发，本项目的整体许可因此为 GPL-3.0。

`Fischless.GameCapture/Graphics/GraphicsCaptureV2.cs` 自身注明部分设计参考 FFmpeg 的 `libavfilter/vsrc_gfxcapture_winrt.cpp`（LGPL-2.1-or-later，允许升级为 GPL）。

## 运行期依赖

| 组件 | 许可 | 说明 |
|---|---|---|
| [OpenCvSharp4](https://github.com/shimat/opencvsharp) | Apache-2.0 | OpenCV 的 .NET 绑定 |
| [OpenCV](https://opencv.org/) | Apache-2.0 | 由 `OpenCvSharp4.Windows` 携带的原生库 |
| [SharpDX](https://github.com/sharpdx/SharpDX) | MIT | Direct3D11 / D3DCompiler 绑定 |
| [Vanara](https://github.com/dahall/Vanara) | MIT | Win32 P/Invoke |
| [ONNX Runtime](https://github.com/microsoft/onnxruntime) | MIT | 推理引擎 |
| [YoloSharp](https://github.com/dme-compunet/YoloSharp) | AGPL-3.0-or-later | YOLO 推理与后处理。GPLv3 第 13 条允许与本项目组合，AGPL 第 13 条的网络条款随组合体生效 |
| [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)、ImageSharp.Drawing、Fonts | Six Labors Split License（开源项目适用 Apache-2.0） | 由 YoloSharp 引入 |
| [Clipper2](https://github.com/AngusJohnson/Clipper2) | BSL-1.0 | 由 YoloSharp 引入 |
| [ModelContextProtocol](https://github.com/modelcontextprotocol/csharp-sdk) | MIT | MCP 官方 C# SDK |
| [Serilog](https://github.com/serilog/serilog) | Apache-2.0 | 日志 |
| [YamlDotNet](https://github.com/aaubry/YamlDotNet) | MIT | YAML 解析 |
| [Microsoft.Extensions.*](https://github.com/dotnet/runtime) | MIT | 宿主 / DI / 配置 |

## 模型与数据

| 资源 | 许可 | 来源 |
|---|---|---|
| `bgi_world` / `bgi_fish` / `bgi_mine` / `bgi_tree` / `avatar` / `item` / Q·E 技能分类 | GPL-3.0 | BetterGI 组织自训，见 `BetterGI.Assets.Model` 包内 `Assets/Model/LICENSE` |
| PP-OCRv4 / v5 / v6 det 与 rec | Apache-2.0 | [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR)，经 [Paddle2ONNX](https://github.com/PaddlePaddle/Paddle2ONNX) 转换 |
| Silero VAD | MIT | 见包内 `Assets/Model/Vad/LICENSE` |
| VC++ 2015–2022 x64 运行库 | Microsoft 可再发行组件 | `BetterGI.VCRuntime` |

## 系统组件

`d3dcompiler_47.dll`、`DirectML.dll`、`d3d11.dll`、`dxgi.dll` 随 Windows 分发，
不由本项目再分发。
