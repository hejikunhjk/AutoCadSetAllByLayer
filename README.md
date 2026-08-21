# SetBL - AutoCAD 图层颜色统一插件 / AutoCAD Layer Color Unification Plugin

[![Apache License 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)

## 简介 / Introduction

SetBL 是一款面向 AutoCAD 的 .NET C# 插件，旨在快速将图纸中所有非 ByLayer 颜色的对象统一设置为 ByLayer（随图层颜色）。这对于统一图块内部颜色、批量修正图层设置等问题非常有用。

SetBL is a .NET C# plugin for AutoCAD that quickly sets all non-ByLayer colored objects in a drawing to ByLayer. It's very useful for unifying block internal colors and batch-correcting layer settings.

## 功能特性 / Features

- **批量处理 / Batch Processing**：自动遍历整个图纸空间，包括模型空间、图纸空间（布局）和所有块定义
- **智能过滤 / Smart Filtering**：仅修改颜色非 ByLayer 的对象，已是 ByLayer 的对象自动跳过；锁定图层上的对象也会自动跳过 / Objects on locked layers are also skipped automatically
- **进度显示 / Progress Display**：实时显示处理进度和统计信息
- **详细日志 / Detailed Logging**：处理完成后输出修改统计（修改数、跳过数、耗时等）

## 系统要求 / System Requirements

- **AutoCAD**：2024 或更高版本 / 2024 or higher
- **.NET 运行时 / .NET Runtime**：.NET 8.0 Windows
- **操作系统 / Operating System**：Windows 10/11 (x64)

## 快速安装 / Quick Install

- 下载 [dist/SetBL.dll](dist/SetBL.dll) 和 [dist/load_setbl.lsp](dist/load_setbl.lsp) / Download [dist/SetBL.dll](dist/SetBL.dll) and [dist/load_setbl.lsp](dist/load_setbl.lsp)

### 方法一：自动加载（推荐）/ Auto Load (Recommended)

1. 将两个文件放入 AutoCAD 支持搜索路径，例如： / Place both files into an AutoCAD support search path, for example:
   ```
   C:\Users\YourUsername\AppData\Roaming\Autodesk\AutoCAD 2026\R26\chs\Support\
   ```
2. 在 AutoCAD 命令行运行： / Run in AutoCAD command line:
   ```
   ap
   ```
3. 然后加载： / Then load:
   ```
   C:\Users\YourUsername\AppData\Roaming\Autodesk\AutoCAD 2026\R26\chs\Support\load_setbl.lsp
   ```
4. 添加到启动组以便以后自动加载 / Add to Startup group for automatic loading on next launch

5. 输入 `SetBL` 启动插件 / Type `SetBL` to launch the plugin

### 方法二：直接使用 / Direct Use

1. 将两个文件放入 AutoCAD 支持搜索路径，例如： / Place both files into an AutoCAD support search path, for example:
   ```
   C:\Users\YourUsername\AppData\Roaming\Autodesk\AutoCAD 2026\R26\chs\Support\
   ```
2. 在 AutoCAD 命令行运行： / Run in AutoCAD command line:
   ```
   (load "load_setbl")
   ```
3. 输入 `SetBL` 启动插件 / Type `SetBL` to launch the plugin

### 方法三：NETLOAD / Method 3: NETLOAD

1. 在 AutoCAD 命令行输入 `NETLOAD` / Type `NETLOAD` in AutoCAD command line
2. 选择 `dist/SetBL.dll` / Select `dist/SetBL.dll`
3. 输入 `SetBL` 运行 / Type `SetBL` to run

## 编译构建 / Build from Source

```powershell
git clone https://github.com/hejikunhjk/AutoCadSetAllByLayer.git
cd AutoCadSetAllByLayer
dotnet build "SetBL\SetBL.csproj" -c Release -nologo
```

编译产物位置 / Output: `dist\SetBL.dll`

## 使用方法 / Usage

插件加载后，在 AutoCAD 命令行输入 / After loading, type in AutoCAD command line:
```
SetBL
```

插件将自动 / The plugin will automatically:
1. 处理模型空间和图纸空间中的独立对象 / Process independent objects in model space and layout
2. 遍历所有块定义，修改块内部内容的颜色 / Traverse all block definitions and modify internal colors
3. 重生成图形并显示处理结果 / Regenerate graphics and show results

### 输出示例 / Output Example

```
正在遍历图纸中所有对象（仅修改非ByLayer颜色的对象）...
Traversing all objects in drawing (only modifying non-ByLayer objects)...

>> 第1步：处理图纸空间中的独立对象...
>> Step 1: Processing independent objects in layout...

>> 第2步：遍历所有块定义，修改内部内容颜色...
>> Step 2: Traversing block definitions and modifying internal colors...

>> 第3步：重生成图形... 完成
>> Step 3: Regenerating graphics... Done

============ 处理结果 / Results ============
总计修改 / Total modified: 145 个对象 / objects
总计跳过 / Total skipped: 56 个对象（已是ByLayer）/ objects (already ByLayer)
总耗时 / Total time: 1234 ms
===============================
```

## 项目结构 / Project Structure

```
SetBL/
├── SetBL.csproj          # 项目文件 / Project file
├── SetBLPlugin.cs        # 插件入口类 / Plugin entry class
├── Commands.cs           # 所有命令实现 / Command implementations
├── SetBLTZCommands.cs    # 天正命令实现 / TArch command implementations
├── InspectCommands.cs    # 检查命令 / Inspection commands
├── Utils/
│   ├── ColorHelper.cs    # 颜色判断/设置工具 / Color check/set utilities
│   └── ProgressReporter.cs # 进度报告工具 / Progress reporter utilities
├── dist/                # 编译成品 / Compiled plugin
│   ├── SetBL.dll
│   ├── SetBL.deps.json
│   └── SetBL.pdb
└── SetBL.dll             # 编译成品（供 NETLOAD 使用）/ Compiled plugin (for NETLOAD)
```

## 核心技术 / Core Technologies

- **AutoCAD .NET API**：使用 `Autodesk.AutoCAD.Runtime` 和 `Autodesk.AutoCAD.DatabaseServices`
- **事务处理 / Transaction Processing**：通过 `Transaction` 安全地遍历和修改数据库对象
- **颜色系统 / Color System**：深度支持 AutoCAD ACI 颜色索引和 ByLayer/ByBlock 颜色方法

## 许可证 / License

本项目基于 Apache License 2.0 开源。/ This project is open source under Apache License 2.0.
