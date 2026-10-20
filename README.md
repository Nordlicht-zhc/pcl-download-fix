# Plain Craft Launcher (PCL) 本地下载修复版

基于龙腾猫跃开发的 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 的第三方本地修改版，用于恢复 2.99.0.0 中被停用的 Minecraft 下载页面与整合包安装入口。本项目不是官方版本。

原作者：**龙腾猫跃**。原作者的关于页面、[赞助入口](https://afdian.com/a/LTCat)、游戏购买与赞助劝导均保留。原许可和使用指南见 [LICENCE](LICENCE)，有关分发权限请以其中引用的《PCL 分发有限许可》为准，本仓库没有重新授权上游代码。

## 使用

程序位于 [dist](dist/)，文件名为 `Plain Craft Launcher (PCL) 本地下载修复版.exe`。在 Windows 上将它放进自己的 PCL 文件夹再运行，即可使用该目录已有的 Minecraft 文件和设置。保留原启动器，便于切回原版本。

运行文件 SHA256：

```text
FB3E219C4B579661052E791496CF8D0D9C3BE93EE7EBCABFB3750E647AA2C988
```

## 修改内容

`source/Plain Craft Launcher 2/Modules/ModSecret.vb` 的 `RestrictMcDownload` 从 `True` 改为 `False`。为了在本地编译，VB 项目对 `MeloongCore`、`MeloongCore.Wpf`、`PCLCS` 的引用改为同版本原程序中提取的 DLL，放在 `source/PCLCS/bin`；其余相关依赖同样保留供编译使用。

原 EXE 中部分正常分支已经被编译器删除，因此最终程序采用“编译同版本官方源码，再将恢复的方法写回原程序集”的构建方式。`tools/Program.cs` 提供方法恢复与比较实现，实际变化包括：

- `PageDownloadInstall.LoaderInit()`：接入正常的 Minecraft 版本列表加载器。
- `ModModpack.ModpackInstall()`：恢复选择本地整合包的入口。
- `ModModpack.ModpackInstall(File, InstanceName, Logo)`：恢复按整合包类型调用安装函数。
- 整合包入口的后台回调 `_Lambda$__0()`：与上述方法配套恢复。

最终比较确认，4 个方法体发生变化，6335 个方法体及 2 个完整内嵌资源保持一致，限制常量为 `False`。这里的同版本表示上游源码和本地基准程序均标记为 2.99.0.0、Release 版本代码 413；没有把仅版本号相同当作代码完全一致的依据，最终使用方法和资源比较约束改动范围。

## 验证

已通过源码构建、修改程序集的 JIT 检查、下载页面初始化与整合包格式解析检查。联网测试中版本列表加载状态达到 `Finished`，获取到 919 个版本条目。记录见 [docs/assembly-check.txt](docs/assembly-check.txt) 和 [docs/runtime-check.txt](docs/runtime-check.txt)。

尚未执行完整游戏下载安装、真实整合包安装或微软登录验证。测试取得的版本数量是当时的结果，不代表后续固定数量。

## 重新构建

需要 Windows、Python 3、.NET 9 SDK、Visual Studio MSBuild，以及 .NET Framework 4.8/WPF 编译环境。首次构建工具会从 NuGet 获取 Mono.Cecil 0.11.6。

准备一份未修改的原版 2.99.0.0 EXE，然后执行：

```powershell
./build.ps1 -OriginalExe 'D:/path/to/Plain Craft Launcher 2.exe'
```

基准程序必须匹配以下 SHA256，脚本会在修改前检查：

```text
6B6E1260CB5A1D6A303855F1C60CAB3D0BC1AEBBA7E40A964180188B271E7386
```

脚本输出到 `dist`，不覆盖传入的原程序。仓库包含修改后的上游源码、所需编译依赖、构建工具和修改版程序；不包含本地游戏目录、账号配置、存档、原始反编译目录或个人运行日志。
