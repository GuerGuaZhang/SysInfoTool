# dsh-sysinfo-report

DSH 技能插件：生成电脑硬件/系统/软件/网络信息报告。

## 安装

在 DSH 中安装此插件：

```bash
dsh plugin --profile <你的profile名> add dsh-sysinfo-report
```

或者手动将此目录复制到 DSH 的插件目录。

## 使用

安装后，在对话中说以下内容即可触发：

- "生成电脑信息报告"
- "看看这台电脑的配置"
- "排查一下系统问题"
- "导出硬件信息"

## 功能

- 采集 26 个模块的系统信息
- 生成自包含 HTML 报告（可视化）
- 生成结构化 JSON 数据（程序/AI 可解析）
- 支持脱敏模式（隐藏敏感信息）
- 支持按电脑名分目录存储

## 依赖

- SysInfoTool.exe（需要在系统 PATH 中或当前工作目录）
- Windows 10 1809+ / Windows 11
- .NET Framework 4.8（系统自带）

## License

MIT
