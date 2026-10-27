# 开发与构建

## 环境

- Windows 11 x64。
- .NET 8 SDK x64；`global.json` 接受已安装的 .NET 8 功能更新。
- PowerShell 5.1 或 7；Visual Studio 2022 可选。

程序与回归测试不依赖第三方 NuGet 包。自包含发行版需要 .NET 8 Windows x64 运行时包，可由 NuGet 还原或事先放入本地源。

## 构建

从仓库根目录执行：

```powershell
.\scripts\build.ps1
.\scripts\test.ps1 -Suite Surface -NoBuild
```

`build.ps1` 总是先还原两个项目，再构建整个解决方案。也可使用 `dotnet build TopMostTool.sln -c Release`；测试程序集位于 `tests/TopMostTool.Tests/bin/Release/net8.0-windows/`。

SDK 未加入 PATH 或需要离线构建时：

```powershell
.\scripts\build.ps1 -DotnetPath 'C:\SDK\dotnet.exe' -PackageSource 'C:\NuGet\offline-feed'
.\scripts\test.ps1 -DotnetPath 'C:\SDK\dotnet.exe' -Suite Surface -NoBuild
.\scripts\publish.ps1 -DotnetPath 'C:\SDK\dotnet.exe' -PackageSource 'C:\NuGet\offline-feed'
```

示例路径需替换为本机现有 SDK 和包源。脚本不会下载或安装 SDK。需要指定缓存目录时，可通过标准 `NUGET_PACKAGES` 环境变量设置。

## 发布

先通过托盘正常退出正在运行的实例，并保存需要的贴图，再运行：

```powershell
.\scripts\publish.ps1
```

流程为：还原两项目 → 构建测试程序 → 发布到 `artifacts/publish/staging-win-x64` → 运行 `--published` 验证 → 生成 `artifacts/release/v版本/`。

发行目录包含：

- `TopMostTool.exe`：Windows x64、自包含单文件程序。
- `TopMostTool-版本-win-x64.zip`：程序、使用说明和 MIT 许可证。
- `SHA256SUMS.txt`：EXE 与 ZIP 的校验值。
- `RELEASE_NOTES.md`：用于 GitHub Release 正文的发行说明。

脚本不覆盖当前运行目录。验证通过后可将 staging EXE 复制到本地 `publish/win-x64/TopMostTool.exe`，再启动正式版本。`publish/`、`artifacts/` 和所有 `bin/obj` 都在 Git 忽略列表中。

维护版本号时，同时更新项目中的 `Version`、测试版本断言、README 下载文件名和发行说明。打好 `v版本` 标签后，可在 GitHub Actions 的 **Release** 工作流手动选择该标签发布。已有同名 Release 时流程会停止，避免覆盖已发行文件。

## 图标资源

已确认的原图和衍生帧位于 `src/TopMostTool/Assets`，全部嵌入 EXE。普通编译和发布不需要 Python，也不重新生成资源。

```powershell
# 只在需要更新已确认图标时运行
.\scripts\build-icons.ps1 -TrayOnly
python -m pip install -r scripts/requirements-icons.txt
python scripts/build-menu-pins.py
python scripts/redraw-icons.py --preview artifacts/icon-preview.png
```

`build-icons.ps1` 处理程序/托盘资源；`build-menu-pins.py` 派生菜单图钉；`redraw-icons.py` 只处理六个操作图标。不要无意覆盖品牌原图，也不要把预览图作为运行资源。

## 主要模块

| 文件 | 职责 |
| --- | --- |
| `TrayApplicationContext` | 托盘、菜单、快捷键与窗口生命周期 |
| `WindowManager` / `WindowLocationMonitor` | 外部窗口置顶、恢复与位置事件 |
| `PinnedBadgeWindow` | 透明图层、标题栏定位和点击穿透 |
| `ScreenCapture` / `ScreenshotSelectionOverlay` | 屏幕捕获与区域选择 |
| `PinnedImageWindow` | 截图显示、拖动、缩放、复制和保存 |
| `UiTheme` / `RoundedButton` | 菜单渲染、圆角和按钮状态 |
| `TrayIconFactory` / `MenuIconFactory` | 按实际像素尺寸加载嵌入图标 |

`PinnedBadgeWindow.CreateLayeredSurface` 必须使用显式源/目标像素矩形复制；`DrawImageUnscaled(image, Point.Empty)` 仍可能按 PNG DPI 元数据缩放并裁切图钉。原始像素回归通过逐像素 alpha 合成比较，不能复用被测 GDI+ 绘制方法生成预期图。
