# 音频FFT功能使用说明

## 修改内容

根据您提供的示例，我已经成功修改了代码，使用NAudio.Dsp命名空间下的FastFourierTransform和Complex类来实现音频可视化功能。

### 主要修改：

1. **AudioCapture.cs**
   - 添加了`NAudio.Dsp`命名空间引用
   - 修改了`OnDataAvailable`方法，直接在音频数据可用时进行FFT计算
   - 使用您提供的示例代码逻辑：
     ```csharp
     // 获取采样数据
     float[] samples = Enumerable
                           .Range(0, e.BytesRecorded / 4)
                           .Select(i => BitConverter.ToSingle(e.Buffer, i * 4))
                           .ToArray();
     
     // 计算需要的2的幂次长度
     int log = (int)Math.Ceiling(Math.Log(samples.Length, 2));
     float[] filledSamples = new float[(int)Math.Pow(2, log)];
     Array.Copy(samples, filledSamples, samples.Length);
     
     // 将采样转换为复数（缠绕到圆）
     Complex[] complexSrc = filledSamples.Select((v, i) =>
     {
         double deg = i / (double)currentSampleRate * Math.PI * 2;
         return new Complex()
         {
             X = (float)(Math.Cos(deg) * v),
             Y = (float)(Math.Sin(deg) * v)
         };
     }).ToArray();
     
     // 进行傅里叶变换
     FastFourierTransform.FFT(false, log, complexSrc);
     
     // 计算幅度谱
     double[] result = complexSrc.Select(v => Math.Sqrt(v.X * v.X + v.Y * v.Y)).ToArray();
     ```
   - 添加了新的事件`FFTResultAvailable`，直接提供FFT计算结果

2. **FFTAnalyzer.cs**
   - 更新为使用`NAudio.Dsp.Complex`类型
   - 修改了相关方法以适配新的Complex类型
   - 保留了原有的分析功能，如峰值检测、分贝转换等

3. **Form1.cs**
   - 更新了事件处理程序，使用新的`FFTResultAvailable`事件
   - 简化了音频数据处理流程

## 使用方法

### 在Form1中启动音频分析：
```csharp
// 点击"开始音频分析"按钮
private void button5_Click(object sender, EventArgs e)
{
    if (!isAudioAnalysisRunning)
    {
        StartAudioAnalysis();
        // 按钮状态更新...
    }
    else
    {
        StopAudioAnalysis();
        // 按钮状态更新...
    }
}
```

### 测试功能：
可以使用创建的`AudioTest.cs`类来测试音频捕获和FFT功能：
```csharp
AudioTest.RunTest();
```

## 功能特点

1. **实时音频捕获**：使用WASAPI回环捕获系统音频输出
2. **高效FFT计算**：使用NAudio.Dsp.FastFourierTransform进行快速傅里叶变换
3. **音乐可视化**：将频谱数据映射到8x8 LED矩阵显示
4. **颜色映射**：根据频率范围显示不同颜色（低频红色，中频绿色，高频蓝色）

## 注意事项

- 确保系统有音频输出才能看到可视化效果
- FFT计算在音频数据可用时自动进行，无需手动调用
- 可视化效果会根据音频强度和频率实时更新LED矩阵颜色

## 依赖包

项目已包含所需的NAudio相关包：
- NAudio 2.2.0
- NAudio.Core 2.2.1
- NAudio.Wasapi 22.0.0
- 其他NAudio相关包

所有修改已完成，代码可以直接运行测试！
