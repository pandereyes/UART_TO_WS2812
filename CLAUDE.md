# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个C# Windows Forms应用程序，用于通过串口驱动WS2812 LED灯板。支持多种显示模式：手动绘图、音乐频谱可视化、图片显示等。

## 核心架构

### 主要组件
- **Form1.cs**: 主界面，包含UI控件和事件处理
- **ws2812_driver.cs**: WS2812串口驱动和编码逻辑
- **DisplayFunc.cs**: 显示功能实现（绘图、频谱、图片等）
- **AudioFFTAnalysis.cs**: 音频FFT分析和音乐频谱功能

### 架构层次
1. **硬件驱动层 (ws2812_driver.cs)**: WS2812协议编码、串口通信、亮度控制
2. **显示功能层 (DisplayFunc.cs)**: 多种显示模式实现，基于IShowable接口
3. **音频处理层 (AudioFFTAnalysis.cs)**: 实时音频捕获和FFT频谱分析
4. **UI控制层 (Form1.cs)**: 用户界面和模式切换控制

### 关键功能
1. **串口通信**: 通过CP2102N USB转串口芯片驱动WS2812，3Mbps波特率
2. **亮度控制**: HSL色彩空间亮度调节（默认60%），保持颜色准确性
3. **多种显示模式**: 
   - 0: 手动绘图模式 (display_func_draw)
   - 1: 音乐频谱模式 (display_music_spectrum)
   - 2: 音乐柱状频谱模式 (display_music_bar_spectrum - 待实现)
   - 3: 图片显示模式 (display_picture)
4. **音频分析**: 使用NAudio进行1024点FFT实时分析，44.1kHz采样率

## 开发命令

### 构建和运行
```bash
# 使用MSBuild构建 (推荐使用Visual Studio Developer Command Prompt)
msbuild "source/串口驱动WS2812.csproj" /p:Configuration=Debug /p:Platform=x64
msbuild "source/串口驱动WS2812.csproj" /p:Configuration=Release /p:Platform=x64

# 运行应用程序
"source/bin/x64/Debug/串口驱动WS2812.exe"
"source/bin/x64/Release/串口驱动WS2812.exe"

# 清理构建
msbuild "source/串口驱动WS2812.csproj" /t:Clean
```

### Visual Studio 开发
```bash
# 打开解决方案
devenv "串口驱动WS2812.sln"

# 构建解决方案
devenv "串口驱动WS2812.sln" /Build "Debug|x64"
devenv "串口驱动WS2812.sln" /Build "Release|x64"
```

### 依赖管理
项目使用NuGet包管理，主要依赖：
- NAudio 2.2.0 (音频处理)
- NAudio.Core 2.2.1
- NAudio.Wasapi 22.0.0
- System.Numerics (数学计算)
- Microsoft.Win32.Registry 5.0.0

恢复NuGet包：
```bash
nuget restore "串口驱动WS2812.sln"
```

## 硬件连接
- **串口**: CP2102N USB转串口
- **电平转换**: MOS管反向电路（TX高电平→低电平输出）
- **LED灯板**: WS2812 8x8矩阵 (64个LED)
- **波特率**: 3,000,000 bps
- **复位信号**: 50μs低电平复位信号

## 重要注意事项

1. **亮度控制**: 使用HSL色彩空间保持颜色准确性，避免简单RGB乘法导致色偏
2. **编码方案**: 特殊bit-banging编码，通过串口8N1协议模拟WS2812时序
3. **供电问题**: USB供电可能不足，建议使用外部5V电源
4. **音频分析**: 需要系统有音频输出，使用WASAPI回环捕获
5. **实时性能**: 显示刷新间隔5ms，音频FFT使用50%重叠处理

## 调试技巧

- **亮度范围**: 0-100（建议不低于20以确保可见性）
- **串口调试**: 检查端口权限、驱动安装和硬件连接
- **音频调试**: 确保系统有音频输出，检查NAudio配置
- **性能优化**: UI更新降频处理（每4次硬件更新更新一次UI）
- **颜色测试**: 使用TestHslConversion()方法测试HSL转换准确性

## 扩展接口

### IShowable接口
所有显示模式实现IShowable接口：
```csharp
public interface IShowable
{
    void show();
}
```

### 全局数据结构
- `g_display_data[64]`: 8x8显示数据缓存
- `g_fft_amp[512]`: FFT幅度数据缓存
- `g_display_func_index`: 当前显示模式索引

## 开发模式

1. **添加新显示模式**: 实现IShowable接口并添加到display_refresh的showList
2. **修改编码方案**: 在ws2812_driver.cs中的WS2812PackBits方法
3. **调整音频参数**: 在AudioFFTAnalysis.cs中修改FFT长度和采样率
4. **自定义颜色映射**: 在music_spectrum_get_color类中修改颜色渐变逻辑