using System;
using System.Collections.Generic;

// 测试随机数修复
class TestRandom
{
    static void Main()
    {
        Console.WriteLine("测试RandomUtils随机性:");
        
        // 测试多次调用是否产生不同的结果
        var results1 = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            results1.Add(RandomUtils.RandRange(1, 100));
        }
        
        Console.WriteLine("第一次调用结果: " + string.Join(", ", results1));
        
        // 再次测试
        var results2 = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            results2.Add(RandomUtils.RandRange(1, 100));
        }
        
        Console.WriteLine("第二次调用结果: " + string.Join(", ", results2));
        
        // 测试random_for_tick
        Console.WriteLine("\n测试random_for_tick随机性:");
        
        var results3 = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            results3.Add(random_for_tick.get_random(100));
        }
        
        Console.WriteLine("random_for_tick结果: " + string.Join(", ", results3));
        
        // 检查是否有重复（理论上应该有很少的重复）
        Console.WriteLine("\n随机性测试完成！");
    }
}