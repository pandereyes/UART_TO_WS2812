# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个C# Windows Forms应用程序，用于通过串口驱动WS2812 LED灯带，支持多种显示模式包括音乐频谱可视化、图片显示、自定义绘图等。

## 构建和运行

### 开发环境
- .NET Framework 4.8
- Visual Studio项目
- 需要串口设备连接WS2812灯带

### 构建命令
```bash
# 使用Visual Studio构建
msbuild "source/串口驱动WS2812.csproj" /p:Configuration=Debug /p:Platform=x64

# 或者使用dotnet构建（如果配置了.NET Framework支持）
dotnet build "source/串口驱动WS2812.csproj" -f net48
```

### 运行
直接运行生成的EXE文件，或通过Visual Studio启动调试。

## 架构设计

### 核心模块

1. **主窗体 (Form1.cs)**
   - UI界面管理
   - 按钮事件处理
   - 串口连接控制

2. **显示系统 (DisplayFunc.cs)**
   - `display_refresh`: 显示刷新定时器和管理器
   - `IShowable`接口: 所有显示功能的统一接口
   - 支持多种显示模式: 绘图、音乐频谱、图片显示等

3. **WS2812驱动 (ws2812_driver.cs)**
   - 串口通信协议实现
   - LED颜色数据发送
   - 亮度控制

4. **音频分析 (AudioFFTAnalysis.cs)**
   - WASAPI音频捕获
   - FFT频谱分析
   - 实时音乐可视化

5. **图片处理 (DisplayFunc.cs中的display_func_picture)**
   - 图片加载和8x8转换
   - 极化处理和色彩增强
   - 参数设置界面集成

### 显示模式系统

项目使用基于接口的显示模式架构：

```csharp
public interface IShowable
{
    void show();
}
```

当前支持的显示模式：
- `display_func_draw`: 自定义绘图模式
- `display_music_spectrum`: 音乐频谱可视化  
- `display_func_picture`: 图片显示模式（支持参数调节）

通过`display_globle_define.g_display_func_index`切换显示模式。

### 配置系统

- **DisplayConfig.cs**: 支持多种灯板配置（8x8, 16x16, 1x30）
- **ImageSettingsForm.cs**: 图片处理参数设置界面
- 全局参数通过静态类管理

## 关键功能

### 音乐频谱可视化
- 实时音频FFT分析
- 8频段频谱显示
- 动态颜色渐变效果

### 图片显示
- 支持多种图片格式
- 8x8像素转换算法
- 极化处理和对比度增强
- 实时参数调节预览

### 串口通信
- 高速串口数据传输
- WS2812协议实现
- 亮度控制支持

## 依赖项

- NAudio 2.2.0+ (音频处理)
- System.Drawing (图像处理)
- System.IO.Ports (串口通信)

## 开发注意事项

1. **显示坐标系统**: 注意Y轴方向，UI显示和物理LED布局可能需要翻转
2. **实时性**: 显示刷新间隔5ms，音频处理需要高效实现
3. **内存管理**: 图片处理使用`using`语句确保资源释放
4. **线程安全**: UI更新使用`BeginInvoke`避免跨线程问题

## 测试功能

项目包含AudioTest.cs用于测试音频功能：
```csharp
AudioTest.RunTest();
```

使用button7测试图片加载功能，会自动弹出参数设置窗口。

## 扩展性

项目设计支持轻松添加新的显示模式，只需实现`IShowable`接口并在`display_refresh`中注册即可。