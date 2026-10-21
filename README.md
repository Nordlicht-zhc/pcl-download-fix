# Plain Craft Launcher (PCL) 本地下载修复版

基于 [PCL 2.99.0.0](https://github.com/Meloong-Git/PCL) 的第三方修改版，恢复 Minecraft 下载页面和整合包安装入口。本项目不是官方版本。

## 下载与使用

**修改后的 EXE 在 [Release](https://github.com/Nordlicht-zhc/pcl-download-fix/releases/tag/v2.99.0.0-download-fix.1) 中下载。** 打开页面，在 **Assets** 中下载 `Plain-Craft-Launcher-PCL-Download-Fix.exe`。

在 Windows 上，将 EXE 放入自己的 PCL 文件夹后运行，即可使用该目录已有的游戏文件和设置。建议保留原启动器。私有仓库需要登录有访问权限的 GitHub 账号才能下载。

## 修改与源码

将 `ModSecret.vb` 中的 `RestrictMcDownload` 改为 `False`，并恢复原 EXE 中已被编译器移除的下载和整合包安装分支。

修改后的源码位于 `source`，方法恢复工具位于 `tools`，构建入口为 `build.ps1`，本地构建结果输出到 `artifacts`。编译好的 EXE 通过 Release 提供。

## 原作者与许可

原作者为 **龙腾猫跃**，项目地址为 [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)，[赞助原作者](https://afdian.com/a/LTCat)。原程序中的作者信息、赞助入口和正版购买劝导保留，许可及使用指南见 [LICENCE](LICENCE)。
