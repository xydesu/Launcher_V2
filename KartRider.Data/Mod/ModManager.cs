using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace KartRider;

public static class ModManager
{
    private static List<IMod> ModList { get; } = new();
    private static string ModPath { get; set; } = string.Empty;

    public static event Action OnAllModLoaded; // 所有Mod加载完成后事件

    // 初始化ModManager
    public static void Initialize(string RootDirectory)
    {
        // 定义mod路径
        ModPath = Path.Combine(RootDirectory, "Mods");
        Console.WriteLine($"Mod加载路径: {ModPath}");

        // 检查mod路径是否存在
        if (!Directory.Exists(ModPath))
        {
            Directory.CreateDirectory(ModPath);
            Console.WriteLine($"Mod文件夹已创建: {ModPath}");
        }
        // 加载所有Mod
        LoadMods(ModPath);
        Console.WriteLine($"Mod加载完成，共加载 {ModList.Count} 个 Mod。");

        OnAllModLoaded?.Invoke();
    }

    // 加载所有Mod
    public static void LoadMods(string modPath)
    {
        string[] ModDllFiles = Directory.GetFiles(modPath, "*.dll");
        // 遍历所有Mod DLL文件
        foreach (string file in ModDllFiles)
        {
            try
            {
                // 加载程序集
                Assembly assembly = Assembly.LoadFrom(file);

                // 寻找所有实现了 IMod 接口的类
                var modTypes = assembly
                    .GetTypes()
                    .Where(t => typeof(IMod).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

                foreach (var type in modTypes)
                {
                    // 实例化 Mod 对象
                    IMod mod = (IMod)Activator.CreateInstance(type);

                    // 执行初始化方法
                    mod.OnInitialize();
                    // 添加到 Mod列表
                    ModList.Add(mod);

                    if (mod is IPacketHandler packetHandler)
                    {
                        PacketDispatcher.RegisterHandler(packetHandler);
                        Console.WriteLine(
                            $">>> Mod [{mod.Name}] 已注册数据包拦截，监听 {packetHandler.InterestedPackets.Count} 种数据包"
                        );
                    }

                    Console.WriteLine(
                        $">>> 成功加载 Mod: [{mod.Name}] 来自 {Path.GetFileName(file)}"
                    );
                }
            }
            catch (Exception ex)
            {
                // 捕获加载过程中的异常
                Console.WriteLine($"[错误] 无法加载文件 {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    // 获取某个Mod的方法
    public static Func<object[], object> GetModAction(string modName, string methodName)
    {
        var mod = ModList.Find(m => m.Name == modName);
        if (mod == null)
            return null;

        var method = mod.GetType().GetMethod(methodName);
        if (method == null)
            return null;

        // 返回一个包装好的委托，内部已经闭包引用了 mod 实例
        return (parameters) =>
        {
            return method.Invoke(mod, parameters);
        };
    }

    // 获取某个Mod的实例
    public static IMod GetMod(string modName) => ModList.FirstOrDefault(mod => mod.Name == modName);
}
