# 💊 M-Capsule

**给桌面吃点药，缓解你的桌面焦虑。**

把常用文件装进胶囊，让桌面重新留白。完整便携包**不到 0.7 MB**，比许多照片还小。免安装、无浏览器内核、无额外后台服务；专注整理，不堆砌功能。

**Windows 11 x64 · .NET Framework 4.8 · 本地保存 · 登录自启**

[下载最新版](https://github.com/SHUAIMAMAX/M-Capsule/releases/latest) · [反馈问题](https://github.com/SHUAIMAMAX/M-Capsule/issues) · [验证与已知限制](docs/验证与已知限制.md)

![桌面胶囊与半透明展开面板](docs/images/desktop-overview.png)

## 功能

- **拖入即复制**：照片、文档、视频、压缩包、程序和快捷方式等文件，不限大小，复制到 `Data/Capsules`。同一路径的文件再次拖入会更新副本；复制失败时保留旧副本。删除原文件，胶囊内副本仍在。文件夹只保存原路径引用。
- **拖出即移走**：从展开面板拖到资源管理器或桌面，目标接收成功后胶囊移除条目；其文件副本进入回收站。旧版路径引用只从胶囊移除，不删除原文件。大文件复制需要磁盘空间，可能短暂占用界面。
- **悬停即展开**：鼠标停留约 0.5 秒打开；桌面被其他应用盖住时不会误触发。面板自适应大小，内容过多时翻页。静态壁纸取样形成半透明毛玻璃。
- **贴合桌面**：胶囊对齐 Windows 图标网格；启动和每次新建胶囊时读取桌面图标尺寸，同步调整所有胶囊的小、中、大图案。默认图案由内置矢量路径绘制，无需大位图。新胶囊参考图标分布，默认在中央、右侧或下侧出现；可以与图标格子重叠。
- **右键即改**：直接右击桌面胶囊，分别更名、改色、改透明度或删除；每项只弹出对应操作的小窗口。透明度 0% 为不透明、100% 为完全透明，自定义图片也能单独调透明度；自定义图片换色前须先还原默认图。
- **集中管理**：托盘右键选择 **◆ 胶囊管理 · Capsule Manager**。右上角绿色 **+** 新建胶囊。勾选多个胶囊，可批量删除、统一换图、统一换色/透明度；使用自定义图的胶囊会跳过换色。点击名称更名。**⚙ 文/A** 切换中文、英语、日语。默认胶囊图有 130 色、0–100% 透明度，默认完全不透明。
- **可选的终极整理**：主动点击并确认后，将当前用户桌面顶层可见的普通文件和快捷方式移入 `胶囊整理` 文件夹，打开并尝试排在左半屏，右半屏**每次新建 5 个**随机色胶囊。再次使用复用已有文件夹，同名文件加序号，不覆盖。不会开机自动运行；不移动已有文件夹、隐藏/系统项目或公共桌面项目。
- **开机可用**：默认在当前用户登录后自启；胶囊、位置、文件副本及配置都随同目录 `Data` 保存。托盘可开关、检查/修复自启；管理窗口底部链接可关闭自启而不退出程序。任务栏另有正常、毛玻璃、全透明三种可选外观，效果取决于 Windows 环境。

胶囊只放桌面，不提供任务栏胶囊固定入口。图片图标支持 PNG/JPEG/BMP/ICO，原图不超过 2 MiB、最长边不超过 1024 像素，建议 256×256 正方形。

![托盘图标](docs/images/tray-icon.png) ![托盘菜单](docs/images/tray-menu.png) ![胶囊管理](docs/images/capsule-manager.png)

截图由项目使用者提供；其中第三方软件只作分类示例，不随项目分发。旧截图可能仍显示更名前的标题。

## 下载与升级

v1.2.1 已提供完整便携包。首次使用须完整解压 `M-Capsule-Portable-1.2.1.zip` 到当前用户可写的固定文件夹，运行 `M-Capsule.exe`。保留旁边的 `.exe.config`、`vendor` 以及运行后生成的整个 `Data`。普通使用**无需下载单独的源码 ZIP**。

**从 v1.1.0 或更早版本升级不能只替换 EXE，因为 EXE 已改名。**

1. 退出旧程序，建议先备份旧版整个 `Data`。
2. 将新版完整 ZIP 解压到**原便携目录**，保留旧 `Data`，不要覆盖或删除它。
3. 运行 `M-Capsule.exe`，核对旧胶囊及配置。程序核验新自启快捷方式后，清理仅指向同目录旧 EXE 的旧启动项。
4. 确认正常后，可手动删除旧 `MSBeatutyCapsule.exe` 和相应 `.config`。此后的更新是否仅替换 EXE，以相应版本说明为准。

搬家要带上整个 `Data` 并在新位置运行一次，以更新自启目标。停用前点击管理窗口底部的关闭自启链接，再从托盘退出；若已经删除程序，可按 `Win+R` → `shell:startup`，仅删除本程序的 `M-Capsule.lnk` 或旧版 `MS Beatuty Capsule Portable.lnk`。Windows 设置或组织策略仍可能阻止自启。

适用 **Windows 11 x64**，建议 22H2 或更新，需 .NET Framework 4.8，普通用户权限即可。Windows 10、ARM64 未系统验证；未提供 macOS、Linux 版本。

## 项目与验证

这是公开的 **Vibe Coding 项目**：人提出需求、决定取舍并反馈真实使用问题，AI 辅助实现和排查。AI 参与并不是质量保证。项目追求轻量、简单、可维护，不引入过多复杂功能。

本版完成 **348 项隔离检查**，涵盖四个右键独立入口、矢量图案内部不透明度和此前的数据、布局、自启逻辑。此前候选版另通过 42 项便携集成检查。当前更新没有操作真实桌面图标、Explorer、任务栏或真实启动文件夹；实际右键交互、Shell 尺寸读取和登录时序仍需用户反馈。详见 [验证与已知限制](docs/验证与已知限制.md)。

主体是 C# / WPF，目标框架 `net48`，输出 x64 便携 EXE。构建说明见 [BUILD.txt](BUILD.txt)。按 [GNU GPL v3](LICENSE.txt) 发布；第三方声明见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。

---

# 💊 M-Capsule — English

**Give your desktop a little medicine and ease the anxiety of desktop clutter.**

Put everyday files into capsules and make room on your desktop again. The complete portable ZIP is **under 0.7 MB**, smaller than many photos. No installer, bundled browser engine, or extra background service. Organization without needless features.

**Windows 11 x64 · .NET Framework 4.8 · Local data · Launch at sign-in**

[Download](https://github.com/SHUAIMAMAX/M-Capsule/releases/latest) · [Report an issue](https://github.com/SHUAIMAMAX/M-Capsule/issues) · [Validation and limits](docs/验证与已知限制.md)

![Desktop capsules and translucent panel](docs/images/desktop-overview.png)

## Features

- **Copy files in**: Drag in photos, documents, videos, archives, apps, and shortcuts of any size. A copy is stored in `Data/Capsules` and survives removal of the original. Reimporting a changed file from the same path refreshes the copy; a failed import leaves the old copy intact. Folders remain path references.
- **Move entries out**: Drag an entry from the expanded panel to Explorer or the desktop. Once accepted by the destination, its capsule entry is removed and the stored copy goes to the Recycle Bin. Legacy path references are only removed from the capsule. Large copies need disk space and may briefly occupy the UI.
- **Hover to open**: Hover for about 0.5 seconds. Covered desktop capsules do not open accidentally. The panel fits its contents and uses pages when needed. Static wallpaper sampling creates a translucent glass background.
- **Fit the desktop**: At launch and whenever a capsule is added, the app reads the desktop icon size and resizes all capsule artwork. Default artwork uses in-app vector paths with no large bitmap asset. New capsules choose a central, right, or lower position based on icon distribution; overlap with icon cells is allowed.
- **Right-click to edit**: Rename, change color, change transparency, or delete one desktop capsule through separate focused dialogs. Transparency ranges from 0% opaque to 100% invisible and also works for custom images. Restore the default artwork before changing a custom image's capsule color.
- **Manage together**: Right-click the tray icon and choose **◆ Capsule Manager**. Use the green **+** at the top right to create a capsule. Select multiple capsules for batch deletion, one shared image, or one color/opacity. Capsules with custom images are skipped for color changes. Click a name to rename it. **⚙ 文/A** cycles Chinese, English, and Japanese. Default artwork has 130 colors and 0–100% opacity, initially fully opaque.
- **Optional Ultimate Organizing Mode**: Only after you choose it and confirm, ordinary visible files and shortcuts on the current user's Desktop move into `胶囊整理`. The folder opens and the app attempts to position it on the left; **five new** random-color capsules appear on the right every time. Later runs reuse the folder and number colliding filenames without overwriting. It never runs at startup and leaves existing folders, hidden/system items, and Public Desktop items alone.
- **Ready at sign-in**: Enabled by default, with capsules, positions, copied files, and settings in the adjacent `Data` directory. The tray can toggle and check/repair startup; the link at the bottom of Capsule Manager turns it off without exiting. Normal, blurred, and clear taskbar modes depend on the Windows environment.

Capsules live on the desktop, not as pinned taskbar capsules. Custom images accept PNG/JPEG/BMP/ICO, up to 2 MiB and 1024 pixels on the longest side; a 256×256 square is recommended.

![Tray icon](docs/images/tray-icon.png) ![Tray menu](docs/images/tray-menu.png) ![Capsule manager](docs/images/capsule-manager.png)

Screenshots were supplied by a project user. Third-party apps shown are examples and are not included. Older screenshots may show the previous name.

## Download and upgrade

Version 1.2.1 is available as a complete portable package. For a first install, fully extract `M-Capsule-Portable-1.2.1.zip` into a fixed user-writable folder and run `M-Capsule.exe`. Keep its `.exe.config`, `vendor`, and the complete `Data` directory created at runtime. **The separate source ZIP is not needed to run the app.**

**Upgrading from v1.1.0 or earlier requires the full ZIP because the EXE has been renamed.** Exit the old app, back up all of `Data`, and extract the new ZIP into the same portable folder without deleting `Data`. Run `M-Capsule.exe` and verify your capsules and preferences. After verifying the new startup shortcut, the app retires old startup entries pointing to the old EXE in the same folder. You may then manually remove the old `MSBeatutyCapsule.exe` and matching `.config`. Follow each later release's upgrade instructions.

When relocating, move all of `Data` and run once from the new location to refresh startup. Before removal, turn off launch at sign-in using the link at the bottom of Capsule Manager, then exit from the tray. If the app is already gone, use `Win+R` → `shell:startup` and remove only its `M-Capsule.lnk` or old `MS Beatuty Capsule Portable.lnk`. System settings or organization policy can still disable startup.

Target system: **Windows 11 x64**, preferably 22H2 or later, with .NET Framework 4.8 and ordinary user permissions. Windows 10 and ARM64 are not systematically verified; macOS and Linux builds are unavailable.

## Project and validation

This is openly a **vibe-coding project**: a person supplies requirements, tradeoffs, and real-use feedback; AI assists implementation and debugging. AI involvement is not a quality guarantee. The project aims to remain small, simple, and maintainable.

This version passed **348 isolated checks** covering four distinct context-menu commands, focused dialogs, transparency mapping, and earlier data, layout, and startup behavior. A prior candidate also passed 42 portable integration checks. This update did not operate real desktop icons, Explorer, the taskbar, or the Startup folder. Live right-click interaction, Shell icon-size reading, and sign-in timing still need user feedback. See [validation and limits](docs/验证与已知限制.md).

The app uses C# / WPF, targets `net48`, and produces an x64 portable EXE. See [BUILD.txt](BUILD.txt). Licensed under [GNU GPL v3](LICENSE.txt), with third-party notices in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
