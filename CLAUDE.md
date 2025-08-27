# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个C# Windows Forms应用程序，用于通过串口驱动WS2812 LED灯板。支持多种显示模式：手动绘图、音乐频谱可视化、图片显示、GIF动画播放等。

## 核心架构

### 主要组件
- **Form1.cs**: 主界面，包含UI控件和事件处理
- **ws2812_driver.cs**: WS2812串口驱动和编码逻辑
- **DisplayFunc.cs**: 显示功能实现（绘图、频谱、图片、GIF等）
- **AudioFFTAnalysis.cs**: 音频FFT分析和音乐频谱功能
- **ImageSettingsForm.cs**: 图片处理参数设置界面

### 架构层次
1. **硬件驱动层 (ws2812_driver.cs)**: WS2812协议编码、串口通信、亮度控制
2. **显示功能层 (DisplayFunc.cs)**: 多种显示模式实现，基于IShowable接口
3. **音频处理层 (AudioFFTAnalysis.cs)**: 实时音频捕获和FFT频谱分析
4. **UI控制层 (Form1.cs)**: 用户界面和模式切换控制
5. **图片处理层**: 图片加载、缩放、色彩增强和极化处理

### 显示模式索引
- **0**: 手动绘图模式 (display_func_draw)
- **1**: 音乐频谱模式 (display_music_spectrum)
- **2**: 图片显示模式 (display_func_picture)
- **3**: GIF动画播放模式

## 关键功能特性

### 串口通信
- 通过CP2102N USB转串口芯片驱动WS2812
- 3,000,000 bps波特率
- 特殊bit-banging编码模拟WS2812时序
- 50μs低电平复位信号

### 亮度控制
- HSL色彩空间亮度调节（默认60%）
- 保持颜色准确性，避免RGB乘法导致的色偏
- 实时亮度调整支持

### 音频分析
- 使用NAudio进行1024点FFT实时分析
- 44.1kHz采样率，50%重叠处理
- WASAPI回环捕获，支持系统音频分析
- 8频段频谱可视化

### 图片处理
- 支持多种图片格式（BMP、JPG、JPEG、PNG、GIF）
- 高质量双线性插值缩放至8x8像素
- 极化处理算法增强黑白对比度
- HSL色彩空间饱和度增强
- 灰度图像二值化处理
- 实时参数调整界面

### GIF动画支持
- 多帧GIF动画播放
- 帧率控制和时间同步
- 内存优化处理

## 开发命令

### MSBuild 构建命令
```bash
# Debug 构建
msbuild "source/串口驱动WS2812.csproj" /p:Configuration=Debug /p:Platform=x64

# Release 构建  
msbuild "source/串口驱动WS2812.csproj" /p:Configuration=Release /p:Platform=x64

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
- NAudio 2.2.0 + 相关组件（音频处理）
- System.Numerics（数学计算）
- Microsoft.Win32.Registry 5.0.0

恢复NuGet包：
```bash
nuget restore "串口驱动WS2812.sln"
```

## 核心数据结构

### 全局数据缓存
```csharp
public static uint[] g_display_data = new uint[64];     // 8x8显示数据缓存
public static uint[] g_fft_amp = new uint[512];         // FFT幅度数据缓存  
public static int g_display_func_index = 0;             // 当前显示模式索引
```

### 显示刷新系统
- 刷新间隔：5ms
- UI更新降频：每4次硬件更新更新一次UI
- 基于IShowable接口的插件架构

### 图片处理参数
- 亮度增强 (Brightness: 1.0f)
- 对比度系数 (Contrast: 2.5f) 
- 饱和度增强 (Saturation: 1.3f)
- 亮度阈值 (Threshold: 0.5f)
- 强制二值化选项

## 硬件规格

- **LED灯板**: WS2812 8x8矩阵 (64个LED)
- **电平转换**: MOS管反向电路（TX高电平→低电平输出）
- **供电**: 建议使用外部5V电源（USB供电可能不足）

## 扩展接口

### IShowable接口
所有显示模式必须实现IShowable接口：
```csharp
public interface IShowable
{
    void show();
}
```

### 添加新显示模式
1. 实现IShowable接口
2. 在display_refresh的display_func_init()中添加实例
3. 分配唯一的显示模式索引

### 图片处理扩展点
- `display_func_picture.EnhanceColor()`: 色彩增强算法
- `display_func_picture.LoadAndProcessImage()`: 图片加载处理流程
- ImageSettingsForm: 参数调整界面

## 调试和优化

### 性能优化
- 显示刷新：5ms间隔确保实时性
- 音频处理：50%重叠提高频率响应
- 内存管理：使用using语句自动释放资源

### 常见问题排查
- **串口连接**: 检查端口权限和驱动安装
- **音频分析**: 确保系统有音频输出
- **亮度范围**: 建议20-100确保LED可见性
- **颜色准确性**: 使用TestHslConversion()测试HSL转换

### 测试功能
- HSL色彩转换测试
- 串口通信测试
- 音频捕获测试
- 图片处理质量验证

## 实时性能指标

- 显示刷新率: 200Hz (5ms间隔)
- 音频采样率: 44.1kHz  
- FFT长度: 1024点
- 重叠处理: 50%
- UI更新频率: 50Hz (每4次硬件更新)

## 文件结构说明

- `source/DisplayFunc.cs`: 所有显示模式实现
- `source/ws2812_driver.cs`: 硬件驱动和协议编码
- `source/AudioFFTAnalysis.cs`: 音频处理核心逻辑
- `source/ImageSettingsForm.cs`: 图片参数设置界面
- `source/Form1.cs`: 主界面和事件处理
- `source/Form1.Designer.cs`: 界面设计器代码

## 开发注意事项

1. **线程安全**: UI更新使用BeginInvoke避免阻塞定时器
2. **资源释放**: 图片、音频等资源使用using语句确保释放
3. **异常处理**: 图片加载等IO操作需要try-catch处理
4. **实时性**: 保持5ms刷新间隔的实时性能
5. **兼容性**: 支持.NET Framework 4.8