# 回归测试

测试程序是普通 .NET 控制台/WinForms 可执行程序，不依赖第三方测试框架。断言失败返回非零退出码；`dotnet test` 不会执行这些检查。

## 专项命令

先执行 `scripts/build.ps1`，再按改动范围选择：

| 命令 | 范围 | 桌面要求 |
| --- | --- | --- |
| `scripts/test.ps1 -Suite Surface -NoBuild` | PNG DPI 与透明图层原始像素复制 | 不采集真实屏幕；适合 Windows CI |
| `scripts/test.ps1 -Suite Rendering -NoBuild` | 图标尺寸、菜单绘制、关于页图像和按钮状态 | Windows 图形环境 |
| `scripts/test.ps1 -Suite Menus -NoBuild` | 菜单布局、禁用状态和快捷键设置 | 交互式桌面 |
| `scripts/test.ps1 -Suite Badges -NoBuild` | 徽标像素、原生/自绘标题栏、窗口事件、真实热键 | 已解锁桌面 |
| `scripts/test.ps1 -Suite UI -NoBuild` | 设置和关于页字体倍率、布局与预览 | Windows 图形环境 |
| `scripts/test.ps1 -Suite About -NoBuild` | 关于页与贴图同时操作 | 已解锁桌面 |
| `scripts/test.ps1 -Suite Full -NoBuild` | 完整桌面回归 | 已解锁桌面，测试期间暂停鼠标操作 |

输出图保存在 `tests/TopMostTool.Tests/artifacts`，不进入版本控制。测试使用单独配置，不覆盖正式用户设置。

所有涉及桌面的测试应顺序执行，避免窗口、鼠标、前台状态和全局快捷键互相干扰。锁屏、远程桌面、其他置顶窗口或同时操作鼠标可能导致屏幕/输入断言失败。

## 发布检查

`scripts/publish.ps1` 会自动执行：

```powershell
dotnet tests/TopMostTool.Tests/bin/Release/net8.0-windows/TopMostTool.Tests.dll --published artifacts/publish/staging-win-x64/TopMostTool.exe
```

检查文件版本、EXE 原生图标、自包含运行时、隐藏热键窗口和进程退出。仅清理测试启动的实例；若已有正式实例运行，会拒绝开始发布检查。

## v1.0.0 验证范围

公开发布整理前，图钉专项 83 项、渲染专项 125 项、staging 发布检查 7 项通过。图钉检查包含六种源 PNG DPI 的逐像素复制、明暗背景、窗口生命周期、普通/最大化原生标题栏、自绘拖动栏高度变化、部分移出屏幕、窗口位置事件与原生热键。

公开发布整理后，两项目离线构建为 0 警告、0 错误，Surface 6 项、Rendering 125 项、Menus 36 项和 staging 发布检查 7 项通过。此时桌面已锁屏，Badges 在首个真实屏幕像素断言处失败，未将本轮 Badges 记录为通过。应用 C# 源码和 172 个图标资源与整理前一致；调整集中在目录、项目引用和构建/发布配置。

完整回归中仍有一项物理鼠标拖动距离断言未稳定通过，没有扩大容差或将其改记为成功。当前实机验证是单显示器、120 DPI；多显示器混合 DPI、不同物理排列和系统深色主题仍待验证。

GitHub CI 只承诺两项目构建和 **Surface** 专项；绿色 CI 不代表完整桌面回归通过。工作流允许手动触发重新验证。
