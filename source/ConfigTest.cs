using System;
using System.IO;

namespace UART_TO_WS2812
{
    /// <summary>
    /// 配置管理测试类，用于验证配置的完整性
    /// </summary>
    public static class ConfigTest
    {
        /// <summary>
        /// 测试配置的完整性
        /// </summary>
        public static void TestConfigIntegrity()
        {
            try
            {
                Console.WriteLine("开始测试配置完整性...");

                // 1. 测试默认配置创建
                TestDefaultConfigCreation();

                // 2. 测试配置保存
                TestConfigSave();

                // 3. 测试配置加载
                TestConfigLoad();

                // 4. 测试环境光配置
                TestAmbiLightConfig();

                Console.WriteLine("配置完整性测试完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"配置测试失败: {ex.Message}");
            }
        }

        private static void TestDefaultConfigCreation()
        {
            Console.WriteLine("测试默认配置创建...");
            
            // 创建临时配置文件路径
            string tempConfigPath = Path.Combine(Path.GetTempPath(), "test_config.txt");
            
            // 删除可能存在的临时配置文件
            if (File.Exists(tempConfigPath))
                File.Delete(tempConfigPath);

            // 初始化配置管理器（会创建默认配置）
            ConfigManager.Initialize();
            
            Console.WriteLine("✓ 默认配置创建成功");
        }

        private static void TestConfigSave()
        {
            Console.WriteLine("测试配置保存...");
            
            // 设置一些测试值
            ConfigManager.SetString(ConfigManager.Keys.BoardType, "ambilight");
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightTopLEDs, 35);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightBottomLEDs, 35);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightLeftLEDs, 25);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightRightLEDs, 25);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightStartPosition, 1);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightDirection, 1);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightSamplePercent, 3);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightRefreshRate, 25);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightSampleInterval, 40);
            ConfigManager.SetInt(ConfigManager.Keys.AmbiLightSampleWidth, 25);

            // 保存配置
            ConfigManager.SaveConfig();
            
            Console.WriteLine("✓ 配置保存成功");
        }

        private static void TestConfigLoad()
        {
            Console.WriteLine("测试配置加载...");
            
            // 重新加载配置
            ConfigManager.LoadConfig();
            
            // 验证加载的值
            string boardType = ConfigManager.GetString(ConfigManager.Keys.BoardType);
            int topLEDs = ConfigManager.GetInt(ConfigManager.Keys.AmbiLightTopLEDs);
            int startPos = ConfigManager.GetInt(ConfigManager.Keys.AmbiLightStartPosition);
            int samplePercent = ConfigManager.GetInt(ConfigManager.Keys.AmbiLightSamplePercent);
            
            if (boardType == "ambilight" && topLEDs == 35 && startPos == 1 && samplePercent == 3)
            {
                Console.WriteLine("✓ 配置加载验证成功");
            }
            else
            {
                Console.WriteLine($"✗ 配置加载验证失败: {boardType}, {topLEDs}, {startPos}, {samplePercent}");
            }
        }

        private static void TestAmbiLightConfig()
        {
            Console.WriteLine("测试环境光配置...");
            
            // 应用配置
            ConfigManager.ApplyConfig();
            
            // 验证环境光配置
            var ambiConfig = DisplayConfig.AmbiLightSettings;
            if (ambiConfig.TopLEDs == 35 && 
                ambiConfig.BottomLEDs == 35 && 
                ambiConfig.LeftLEDs == 25 && 
                ambiConfig.RightLEDs == 25 &&
                ambiConfig.StartPosition == AmbiLightStartPosition.LeftBottom &&
                ambiConfig.Direction == AmbiLightDirection.CounterClockwise &&
                ambiConfig.SamplePercent == 3 &&
                ambiConfig.RefreshRate == 25)
            {
                Console.WriteLine("✓ 环境光配置验证成功");
            }
            else
            {
                Console.WriteLine($"✗ 环境光配置验证失败");
                Console.WriteLine($"TopLEDs: {ambiConfig.TopLEDs} (期望: 35)");
                Console.WriteLine($"StartPosition: {ambiConfig.StartPosition} (期望: LeftBottom)");
                Console.WriteLine($"SamplePercent: {ambiConfig.SamplePercent} (期望: 3)");
            }
            
            // 测试配置收集
            ConfigManager.CollectCurrentConfig();
            
            Console.WriteLine("✓ 配置收集测试完成");
        }

        /// <summary>
        /// 列出所有配置项，用于检查是否有遗漏
        /// </summary>
        public static void ListAllConfigKeys()
        {
            Console.WriteLine("所有配置项:");
            Console.WriteLine("基本配置:");
            Console.WriteLine($"  {ConfigManager.Keys.BoardType}");
            Console.WriteLine($"  {ConfigManager.Keys.SerialPort}");
            Console.WriteLine($"  {ConfigManager.Keys.LEDBrightness}");
            
            Console.WriteLine("音乐频谱:");
            Console.WriteLine($"  {ConfigManager.Keys.MusicSensitivity}");
            
            Console.WriteLine("图片处理:");
            Console.WriteLine($"  {ConfigManager.Keys.PictureBrightness}");
            Console.WriteLine($"  {ConfigManager.Keys.PictureContrast}");
            Console.WriteLine($"  {ConfigManager.Keys.PictureSaturation}");
            Console.WriteLine($"  {ConfigManager.Keys.PictureThreshold}");
            Console.WriteLine($"  {ConfigManager.Keys.PictureForceBinarization}");
            
            Console.WriteLine("B站功能:");
            Console.WriteLine($"  {ConfigManager.Keys.BilibiliUID}");
            
            Console.WriteLine("环境光配置:");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightTopLEDs}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightBottomLEDs}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightLeftLEDs}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightRightLEDs}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightSampleInterval}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightSampleWidth}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightStartPosition}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightDirection}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightSamplePercent}");
            Console.WriteLine($"  {ConfigManager.Keys.AmbiLightRefreshRate}");
        }
    }
}