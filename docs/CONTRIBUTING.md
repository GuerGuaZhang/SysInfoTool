# 贡献指南

感谢你对 SysInfoTool 的关注！以下是参与贡献的基本流程。

## 开发环境

- Windows 10 1809+ / Windows 11
- .NET Framework 4.8（系统自带）
- 无需安装 .NET SDK（使用系统自带 csc.exe 编译）

## 项目结构

```
Source/
├── Collectors/          # 26 个信息采集器（每个实现 ICollector 接口）
├── Core/                # 核心架构（采集器接口、调度器、报告模型、总控）
├── Gui/                 # WinForms GUI（主界面、自定义控件、主题）
├── Helpers/             # 工具类（WMI、注册表、进程、脱敏、JSON 等）
├── Output/              # 输出层（HTML 报告、JSON 报告）
├── Program.cs           # 入口
└── SysInfoTool.csproj   # 项目文件
```

## 添加新的采集器

1. 在 `Source/Collectors/` 下创建新文件，如 `MyCollector.cs`
2. 实现 `ICollector` 接口：

```csharp
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class MyCollector : ICollector
    {
        public string Name { get { return "我的模块"; } }
        public int Order { get { return 105; } }  // 决定在报告中的位置

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("my-module", "我的模块", Order, "🔧");
            try
            {
                // 使用 WmiHelper、RegistryHelper 等采集信息
                s.Fact("属性名", "属性值");
                s.Status = "ok";
            }
            catch (System.Exception ex)
            {
                ctx.Fail(Name, "采集失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error";
            }
        }
    }
}
```

3. 在 `Source/Core/ReportService.cs` 的 `RegisterAll()` 方法中注册：

```csharp
runner.Register(new Collectors.MyCollector());
```

4. Order 值决定章节在报告中的位置：

| Order 范围 | 分组 |
|---|---|
| 10-100 | 硬件 |
| 110-130 | 系统 |
| 140-200 | 软件 |
| 210-220 | 账户与痕迹 |
| 230-240 | 性能与日志 |
| 250+ | 网络 |

## 构建

```powershell
.\build.ps1              # 输出到仓库根目录
.\build.ps1 -OutDir bin  # 输出到指定目录
```

## 提交规范

- 使用中文提交信息
- 简要描述改动内容
- 如涉及新采集器，说明采集的 WMI 类或注册表路径

## 许可证

MIT
