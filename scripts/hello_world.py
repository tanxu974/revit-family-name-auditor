# -*- coding: utf-8 -*-
"""
W1 环境验证脚本（Dynamo 的 Python Script 节点）

用法：新建 Dynamo 图 → 放一个 Python Script 节点 → 双击节点，
把本文件全部内容粘贴进去 → 点保存关闭 → 点运行 → 看 OUT 输出。

不用连任何线，不用设 IN[0]，直接跑。

输出三段信息：Python 引擎 / Revit 文档 / 规则 JSON 读取结果
看到 "规则读取成功: version=1.0, 段数=5" 即 W1 通过。
"""

import sys
import os
import json

# 1) Python 引擎版本
engine_info = "Python " + ".".join(str(v) for v in sys.version_info[:3])

# 2) Revit 当前文档（Dynamo 会注入 doc 变量）
doc_info = "doc 未注入"
try:
    d = doc  # noqa: F821  Dynamo 注入
    if d is not None:
        doc_info = "当前文档: " + d.Title
except NameError:
    pass
except Exception as e:
    doc_info = "doc 读取异常: " + repr(e)

# 3) 读规则 JSON（默认按启动包约定路径；若 IN[0] 传了路径则优先用它）
#    注意：Dynamo 里没有 __file__ 变量，所以这里用固定的绝对路径
rule_path = "D:/FamilyNameAuditor/rules/default_rules.json"
try:
    if len(IN) > 0 and IN[0]:  # noqa: F821
        rule_path = str(IN[0])
except Exception:
    pass

if os.path.exists(rule_path):
    try:
        with open(rule_path, "r", encoding="utf-8") as f:
            rules = json.load(f)
        rules_info = "规则读取成功: version=" + str(rules.get("version")) \
                     + ", 段数=" + str(len(rules.get("segments", [])))
    except Exception as e:
        rules_info = "规则读取失败: " + repr(e)
else:
    rules_info = "规则文件未找到: " + rule_path

OUT = engine_info + " | " + doc_info + " | " + rules_info  # noqa: F821
