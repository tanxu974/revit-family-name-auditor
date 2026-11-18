# FamilyNameAuditor · C# Revit 插件版

> 把同名的 Dynamo/Python 原型工程化成的 Revit 二次开发插件。C# / .NET Framework 4.8 / Revit API 2024。

这是作品的**第二阶段**：从"能在 Dynamo 里跑的脚本"升级为"JD 里真正认的 Revit 插件"。原型版见仓库根目录的 `engine/` 与 `dynamo/` 目录。

---

## 一句话说明做了什么

在 Revit 中批量审计族命名是否符合企业规范，对**能 100% 安全推断**的违规项自动改名，其余保守标记人工处理；改后立刻复检，失败则整体回滚。

---

## 架构：逻辑层与适配层分离

```
FamilyNameAuditor/
├── Models/
│   └── NamingRules.cs          # 规则 JSON 解析（系统自带库，零 NuGet）
├── Services/
│   ├── NamingAuditor.cs        # 纯决策逻辑：零 Revit 引用，可独立单测
│   └── AuditReportWriter.cs    # CSV 报告输出（UTF-8 BOM，Excel 中文不乱码）
├── Commands/
│   ├── AuditFamiliesCommand.cs # IExternalCommand：只读审计，不开事务
│   └── RenameFamiliesCommand.cs# IExternalCommand：单事务批量改名 + 复检 + 回滚
├── Config.cs                   # 规则文件路径配置
└── Properties/AssemblyInfo.cs

LogicSmokeTest/                 # 链接同一批源码的控制台项目
└── Program.cs                  # 无 Revit 环境下验证决策逻辑
```

**为什么这样分**：`NamingAuditor.cs` 不引用任何 Revit API，因此可以在没有模型、没有 Revit 的环境里跑单元测试，用可对照的数字证明"逻辑没写歪"——这是原型版已经验证过的证据链在 C# 版的延续。

---

## 三道安全闸门

1. **只改可 100% 推断的**：15 组测试族里 11 个违规，只有 1 个（去掉"副本"二字）敢自动改，其余 10 个保守标人工。
2. **改后立即复检**：自动改名后用同一套规则重新校验，不通过就不写回。
3. **单事务整体回滚**：所有改名包在一个 `Transaction` 里，任一环节抛异常全部撤销。

> 工程工具的价值不在于"敢改多少"，而在于"改错了能不能兜住"。

---

## 验证状态（诚实清单）

| 项目 | 状态 | 证据 |
|---|---|---|
| 源码完整、字节级纯 ASCII | ✅ | 11 个文件校验通过，中文全部 `\u` 转义 |
| 逻辑冒烟测试 | ✅ | `15 族 / 11 违规 / 1 自动改 / 复检通过 1 / 人工 10`，与 Python 原型逐条一致 |
| 报告输出 | ✅ | CSV 2.2 KB，中文正常 |
| DLL 编译 | ✅ | `FamilyNameAuditor.dll` 19.5 KB，系统 `csc.exe` 编译 |
| Revit 插件注册 | ✅ | 「外部工具」下拉出现 `AuditFamiliesCommand` 与 `RenameFamiliesCommand` |
| Revit 进程内加载 | ⚠️ | 被 Windows Smart App Control 拦截（`0x800711C7`），非代码问题 |

**关于最后一条**：本机 Smart App Control 处于 `On` 状态，它默认静默拒绝未签名 DLL 且不提供"仍要运行"入口。作者权衡后**不关闭该单向开关**——为一个开发环境降低系统级安全防线不划算。功能验证由已全链路跑通的 Dynamo/Python 原型承担。正式部署的正规做法是申请 EV 代码签名证书对 DLL 签名。

---

## 如何构建与部署（无需 Visual Studio）

环境要求：Windows + .NET Framework 4.8（含 `csc.exe`）+ Revit 2024。

1. **逻辑验证**：双击 `run_csharp_smoke.bat` → 编译并运行 `LogicSmokeTest`，检查统计行是否与上表一致。
2. **编译插件**：双击 `build_plugin.bat` → 生成 `bin\Debug\FamilyNameAuditor.dll`。
3. **部署插件**：双击 `install_addin.bat`（必要时以管理员身份运行）→ 复制 `.addin` 到 `C:\ProgramData\Autodesk\Revit\Addins\2024\`。
4. **在 Revit 中使用**：完全退出并重开 Revit 2024 → 「外接程序」→「外部工具」→ 先点 `AuditFamiliesCommand`（只读）→ 新建测试项目后再点 `RenameFamiliesCommand`（会真改模型）。

> 所有 bat 均为纯 ASCII + CRLF 换行，规避 Windows 命令行对 UTF-8 中文的解析坑。

---

## 技术要点

- `IExternalCommand`、`Transaction`、`TransactionMode.Manual`
- `FilteredElementCollector` 遍历族元素
- JSON 规则与代码分离，换企业规范无需改代码
- UTF-8 BOM 输出，Excel 直接打开不乱码
- 零 NuGet 依赖，规避国内包源下载问题

---

## 目录结构说明

- `engine/`、`dynamo/`、`rules/`、`testdata/`：原型版（Python / Dynamo）
- `CSharp/`：本插件版（C# / Revit API）
- `audit_report.csv`、`rename_preview.csv`：原型版实测报告
- `csharp_logic_test_output.csv`：C# 逻辑冒烟测试报告

---

## License

用于个人作品集展示与学习交流。
