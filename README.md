# Maitetsu(爱上火车)音乐播放补丁

《爱上火车 Last Run!!》BGM 鉴赏界面的音乐播放器补丁。当前版本 **1.2.3**。

## 功能

- 五种播放模式：单曲循环、顺序循环、随机播放、顺序播放、单曲播放。
- 上一首／下一首、暂停／继续、停止，以及可拖动的播放进度条。
- 收藏歌曲、收藏列表，列表曲名前用 ★ 显示收藏状态。
- 切换歌曲／BGM 分类时保留正在播放曲目的名称、介绍和进度。
- 新增按钮复用原版尺寸、圆角、半透明边框及状态素材。

## 安装

1. 下载 [BGM_App_MOD.zip](bgm_music_mod/BGM_App_MOD.zip) 并解压。
2. 关闭游戏，运行其中的 `BGM_Mod_Manager.exe`。
3. 选择包含 `MaitetsuLastRun.exe` 的游戏目录，点击“安装／更新”。
4. 重新启动游戏，进入 BGM 鉴赏。

卸载时运行同一安装器并选择“卸载 MOD”。收藏保存在游戏系统设置中，卸载补丁不会删除收藏。

仓库也提供 [独立安装器](BGM_Mod_Manager.exe)，已内置补丁文件，可直接下载使用。

## 操作

| 功能 | 操作 |
| --- | --- |
| 上一首／下一首 | 原有按钮，或 ←／→ |
| 暂停／继续 | 原有按钮，或空格 |
| 切换播放模式 | 新增模式按钮、原有循环按钮，或 M |
| 停止 | 原有停止按钮，或 S |
| 调整播放位置 | 拖动进度条后松开；暂停时保持暂停，继续时应用目标位置 |
| 收藏／取消收藏 | 收藏按钮，或 F |
| 收藏列表／返回全部 | 收藏列表按钮，或 C |

完整说明见 [播放器说明](bgm_music_mod/README.md)。

## 源码和构建

- `bgm_mod_work/bgm_app_mod.tjs`：播放器扩展源码。
- `bgm_mod_work/build_mod.py`：生成 `patch.xp3` 与 UTF-16 播放器脚本；仅使用 Python 标准库。
- `bgm_mod_work/ModManager.cs`：Windows 安装器源码。
- `bgm_mod_work/build_installer.ps1`：使用 Windows 自带 .NET Framework C# 编译器构建安装器。
- `bgm_mod_work/installer-legacy.txt`：已知旧版本校验值，用于升级及卸载识别。

在仓库根目录执行：

```powershell
python bgm_mod_work/build_mod.py
powershell -ExecutionPolicy Bypass -File bgm_mod_work/build_installer.ps1
```

构建只更新补丁和安装器。测试引擎、游戏原始资源、歌曲文件及存档不包含在仓库内；运行游戏与真实游戏回归需要自行安装游戏。

## 验证记录

1.2.3 已通过 52 项独立引擎逻辑检查、15 项分步拖动专项检查、58 项真实游戏回归，以及 27 项安装器自检。

检查脚本位于 `bgm_mod_work/`，报告位于 `bgm_music_mod/`。其中独立逻辑检查可把 `check.tjs` 作为官方吉里吉里 Z 引擎的启动脚本执行；脚本中的 `../../bgm_app_mod.tjs` 对应测试引擎安装在 `bgm_mod_work/test-engine/krkrz_20171225/` 的布局。真实游戏检查脚本使用隔离存档测试，文件输出位置沿用开发目录 `爱火火车歌曲插件mod/bgm_mod_work/`。

## 1.2.3 修复

播放定时器同时识别原生滑块拖动状态与播放器拖动状态。按住滑块但尚未移动时，计时刷新不再推动滑块；松开后准确提交所选进度，暂停拖动保持暂停。
