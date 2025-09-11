# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个C# Windows Forms应用程序，用于通过串口驱动WS2812 LED灯带，支持多种显示模式包括音乐频谱可视化、图片显示、自定义绘图等。

## 开发环境

- **框架**: .NET Framework 4.8
- **IDE**: Visual Studio
- **语言**: C#
- **依赖**: NAudio音频库

## 构建和运行命令

### 编译项目
```bash
# 使用Visual Studio构建
msbuild source/UART_TO_WS2812.csproj /p:Configuration=Debug

# 或者使用dotnet build (需要.NET Framework支持)
dotnet build source/UART_TO_WS2812.csproj
```

### 运行应用程序
```bash
# 直接运行编译后的可执行文件
source/bin/x64/Debug/串口驱动WS2812.exe
```

### 清理构建
```bash
msbuild source/UART_TO_WS2812.csproj /t:Clean
```

## 项目架构

### 核心模块
- **Form1.cs**: 主窗体，UI界面管理
- **ws2812_driver.cs**: WS2812串口驱动核心
- **DisplayConfig.cs**: 灯板配置管理（支持8x8, 16x16, 1x30）
- **AudioFFTAnalysis.cs**: 音频FFT频谱分析
- **DisplayFunc*.cs**: 各种显示功能实现

### 插件式显示系统
基于`IShowable`接口的架构：
```csharp
public interface IShowable
{
    void show();
}
```

当前支持的显示模式：
- `display_func_draw` - 自定义绘图模式
- `display_func_music_spectrum` - 音乐频谱可视化
- `display_func_picture` - 图片显示模式

### 灯板配置系统
支持多种LED灯板配置：
- 8x8 (64 LEDs)
- 16x16 (256 LEDs) 
- 1x30 (30 LEDs)

## 关键功能

### 音频频谱可视化
- WASAPI音频捕获
- 1024点FFT分析
- 实时灵敏度调节
- 彩虹色渐变显示

### 图片处理
- 双线性插值缩放
- 亮度/对比度/饱和度调节
- 二值化处理
- GIF动画支持

### 串口通信
- 3Mbps高速串口通信
- 硬件级LED控制
- 实时亮度调节

## 开发注意事项

1. **串口连接**: 需要物理连接WS2812灯带到计算机串口
2. **音频输入**: 需要音频输入设备用于频谱分析
3. **性能优化**: UI更新已优化，避免高频重绘
4. **线程安全**: 使用线程安全锁处理GIF动画帧

## 扩展开发

### 添加新显示模式
1. 实现`IShowable`接口
2. 在对应的`display_*_init.cs`中注册
3. 更新显示模式索引

### 自定义图像处理
修改`EnhanceColor()`方法实现自定义算法

### 支持新灯板类型
在`DisplayConfig.cs`中添加新的`LEDBoardType`和配置