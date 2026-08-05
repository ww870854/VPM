## 加入我们的 Discord 社区
[English](README.md) | [简体中文](README_CN.md)
[![Discord](https://img.shields.io/badge/Chat-Discord-blue?logo=discord)](https://discord.gg/TekWBVsa73)

# VPM - Virt-A-Mate 包管理器

<img width="700" height="677" alt="1" src="https://github.com/user-attachments/assets/eb70fbc3-a9b7-4a21-a915-e18e6179b11d" />
<img width="1360" height="860" alt="VPM exe_20260727_124255" src="https://github.com/user-attachments/assets/84a45035-2853-4285-a5ec-00a8c5f3345d" />

一款专为 Virt-A-Mate 设计的快速、现代且开源的软件包管理器。助您轻松浏览、整理和优化内容库，告别杂乱无章。

---

## 功能介绍

VPM 助您轻松管理数千个 VAR 软件包，无需为此烦恼。它会扫描您的内容库，展示现有资源，并提供工具帮助您保持整洁有序。

<img width="1641" height="1377" alt="image" src="https://github.com/user-attachments/assets/9e1eed01-7ebb-4d49-9187-9ad31dc3af3a" />

### 功能亮点

- **快速扫描** - 借助并行处理和智能缓存，数秒内即可加载数千个包
- **可视化浏览** - 一目了然地查看包、场景和预设的预览图片
- **依赖关系追踪** - 准确掌握每个包所需的资源及其依赖关系
- **纹理优化** - 压缩过大的纹理以节省磁盘空间和显存
- **头发与光照调整** - 降低头发密度和阴影分辨率，以提升性能
- **重复项清理** - 查找并删除重复的包版本
- **收藏与自动安装** - 标记您喜爱的包，并与 sfishere 的 var_browser 自动安装列表同步

---

## 功能

### 优化包

**此功能已被作者移除**

<img width="2378" height="1389" alt="image" src="https://github.com/user-attachments/assets/19b2a98d-cc6a-4b5d-9c63-b489c66d5fe9" />

使用更小的纹理或调整后的设置重新打包资源包：

- **纹理缩放** - 将 8K 纹理缩放为 4K、2K 或 1K
- **头发密度** - 降低发丝数量以获得更流畅的帧率
- **阴影分辨率** - 降低光照阴影贴图的分辨率
- **禁用镜面** - 关闭场景中的镜面
- **JSON 压缩** - 从场景文件中去除空白字符

所有更改都会生成一个新的优化包——您的原始文件保持不变。

---

## 入门指南

1. 下载最新版本
2. 运行 `VPM.exe`
3. 指定您的 VAM 文件夹路径
4. 等待初始扫描（首次运行后会缓存）

就这样。无需安装程序，无需注册表条目，也无需管理员权限。

---

## 系统要求

- Windows 10/11（64 位）
- .NET 10 运行时
- 已安装的 VAM 系统，且包含若干待管理的包

---

## 构建所用组件

- **WPF (.NET 10)** —— 属于 .NET 生态系统的一部分，采用 [MIT 许可证](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) 授权
- [**NetVips**](https://github.com/kleisauke/net-vips) — 高性能图像处理库（MIT 许可）
- [**SharpCompress**](https://github.com/adamhathcock/sharpcompress) — 归档处理和压缩工具 （MIT 许可）
- [**ImageListView**](https://github.com/oozcitak/imagelistview) — 可自定义的图像预览网格（[Apache 2.0 许可](https://www.apache.org/licenses/LICENSE-2.0)）

## 许可协议
本项目采用知识共享 署名-非商业性使用-相同方式共享 4.0 国际许可协议。
详情请参阅 LICENSE 文件。
