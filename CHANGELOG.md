# 更新记录

## v1.2.1

- 胶囊管理窗口右上角加入绿色“+”新建胶囊；底部链接可清除当前副本的开机自启，程序继续运行。
- 托盘右键按管理/外观/整理、自启、自助与退出分为三区；移除托盘“新建胶囊”“打开胶囊”和“清理自启并退出”。
- 终极整理新建的 5 个胶囊明确使用 100% 图案不透明度；收敛矢量图案的浅色半片与高亮，避免视觉上显得发透。
- 桌面胶囊右键新增四个独立操作：更名、改色、改透明度、删除；各自直接弹出对应窗口，无需进入胶囊管理。
- 改色只修改默认胶囊图的颜色；透明度只修改当前图案的不透明度，也适用于自定义图片，名称保持可见。删除仍须确认。
- 胶囊图案按桌面图标的小、中、大尺寸调整，默认图使用程序内矢量路径，无 2K 位图素材。保留 1.2.0 未发布候选版的数据修复。
- 通过 348 项隔离检查；实机菜单操作与显示效果待用户试用确认。

## v1.2.0

- 全面更名为 M-Capsule，包括程序、EXE、发布包和仓库；旧版升级需完整 ZIP，并保留 `Data`。
- 胶囊管理加入复选与批量删除、统一图片、统一颜色/透明度；增加中文、英语、日语切换。
- 新拖入的文件不限大小，默认复制到胶囊；支持从胶囊拖出并在目标接收后移除条目。
- 同一路径重复导入会原子更新副本；失败保留旧副本，并改进复制中断后的恢复和深目录临时文件路径。
- 终极整理模式每次新建五个胶囊，复用已有整理文件夹，同名文件加序号且不覆盖。
- 完整便携 ZIP 不到 0.7 MB，源码 ZIP 保持独立。

## v1.1.0

- 胶囊与桌面图标重叠时保持可见；新胶囊根据桌面图标分布选择中央、右侧中间或下方中间位置。
- 托盘新增手动触发的“终极整理模式”：确认后将当前用户桌面的可见普通文件及快捷方式移动到“胶囊整理”，打开文件夹并尝试左半屏排列，在右半边生成五个随机色胶囊。
- 收纳中途失败时回退已移动文件；同名整理文件夹存在则停止。增加 14 项隔离测试及 6 项位置测试。

## v1.0.2

- 单个文件不超过 5 MiB（含 `.lnk/.url` 快捷方式）时复制进胶囊，删除原件后副本仍可打开；超过 5 MiB 的文件和文件夹保留路径引用。
- 旧版仍可访问的小文件引用在首帧显示后逐项迁移；已失效引用保留，避免丢失条目。
- 补充副本恢复、路径校验和隔离测试；保持现有 x64 便携构建方式。

## v1.0.1

- 按桌面图标格子起点吸附，允许首行和已有图标格子，不再额外居中和避让图标。
- 桌面活动时刷新图标格距；鼠标滚轮改变图标大小后，胶囊跟随新网格重新吸附。
- 胶囊与桌面图标重叠时，让原图标优先显示和接收点击，移走图标后胶囊重新出现。
- 修正前 10 种默认颜色被当作透明 ARGB 的错误；默认图标完全不透明。

## v1.0.0

- 每个默认胶囊可在 130 色面板中独立换色，并调整图标透明度 0–100%；配置随便携目录保存。
- 自定义图片与默认胶囊换色互斥：上传图片后禁用换色，点还原后重新开放。
- 托盘新增“清理自启并退出”；删除便携目录前可清理属于当前副本的自启快捷方式，失败时保留程序运行并提示。
- 补充停用、迁移、误删程序和配置保留的说明及回归检查。

## v0.6.6

- 启动时先绘制胶囊，随后处理托盘、自启登记检查和图标加载；日志记录程序内部首帧耗时。
- 新增文件、照片、文件夹等原路径引用；拖入时不复制文件，删除引用也不删除原文件。
- 通过只读 Windows 桌面视图取得图标格距与占用位置，胶囊尽量共用同一网格并避开原图标。
- 管理列表及“胶囊更名”窗口支持确认后替换图标、还原默认图；图标保存在便携数据目录。
- 默认胶囊图颜色扩展为 130 种；保留 v0.6.5 配置和旧快捷方式副本的兼容读取。

## v0.6.5

- 展开面板改用逐像素半透明窗口与壁纸取样模糊，取消旧版近乎不透明的黑底回退。
- 支持壁纸显示位置、多屏坐标与裁切对齐；背景处理放到后台，文字和图标保持清晰。
- 补充壁纸解码、模糊、透明通道和多屏位置测试。

## v0.6.4

- 登录自启改用启动文件夹快捷方式，创建后读回核验。
- 增加“检查/修复开机自启…”及启动日志。
- 增加登录早期桌面尚未就绪时的定位重试。

## v0.6.3

- 支持鼠标悬停 0.5 秒展开，并核验实际命中的窗口。
- 改善“显示桌面”后的胶囊恢复逻辑。

## 更早版本

逐步加入便携运行、本地配置恢复、网格对齐、列表更名和删除、无滚动条的自适应面板及基础任务栏外观。

---

# Changelog — English

## v1.2.1

- Capsule Manager now has a green **+** for creation and a footer link that removes sign-in startup without closing the app.
- Grouped the tray menu into capsule tools, startup settings, and help/exit; removed the tray's create, open, and cleanup-and-exit entries.
- Ultimate Organizing capsules explicitly use full artwork opacity. Reduced the vector capsule's pale highlight so opaque artwork looks solid.
- Added four independent desktop-capsule right-click actions: rename, change color, change transparency, and delete. Each opens its own focused dialog without entering Capsule Manager.
- Color changes only the default artwork; transparency changes the current image, including custom images, while leaving its name visible. Deletion still requires confirmation.
- Capsule artwork now follows small, medium, and large desktop icon sizes; default artwork is rendered from in-app vector paths with no 2K bitmap asset. The data fixes in the unpublished 1.2.0 candidate remain included.
- Passed 348 isolated checks; live menu and sizing behavior await user testing.

## v1.2.0

- Renamed the product, EXE, release files, and repository to M-Capsule. Upgrades from the old name require the full ZIP while preserving `Data`.
- Added multiselection in Capsule Manager for batch deletion, one image, or one color/opacity, plus Chinese, English, and Japanese UI switching.
- New files of any size are copied into capsules; dragging entries out removes them after an accepted drop.
- Reimporting the same path atomically refreshes its copy. Failure preserves the old copy; recovery ignores interrupted staging files, and staging names remain short in deep folders.
- Ultimate Organizing Mode creates five new capsules on each run, reuses the existing organizing folder, and numbers filename collisions without overwriting.
- The complete portable ZIP stays under 0.7 MB; the source ZIP remains separate.

## v1.1.0

- Kept capsules visible over desktop icons and placed new capsules according to desktop icon distribution.
- Added an explicitly confirmed Ultimate Organizing Mode that moves visible ordinary Desktop files to an organizing folder, opens it on the left, and creates five capsules on the right.
- Added rollback on move failure and isolated tests. This release originally stopped when the organizing folder already existed; v1.2.0 changes that behavior.

## v1.0.2

- Copied files up to 5 MiB into capsules while larger files and folders remained path references. v1.2.0 removes the size limit for new file imports.
- Migrated reachable small-file references gradually and retained missing references.

## v1.0.1

- Aligned capsules to the desktop icon grid, including its first row and occupied cells, and tracked icon-size changes.
- Corrected default capsule artwork opacity.

## v1.0.0

- Added 130 capsule colors and 0–100% icon opacity, with custom images taking precedence until restored.
- Added “Clean startup and exit” and configuration-preserving migration guidance.

## v0.6.6 and earlier

- Gradually added portable local configuration, file references, desktop-grid placement, image replacement, list-based management, wallpaper-sampled translucent glass, sign-in startup verification, hover expansion, Show Desktop recovery, adaptive panels, and taskbar appearance options. Earlier versions had different copy/reference behavior; the current instructions govern new imports.
