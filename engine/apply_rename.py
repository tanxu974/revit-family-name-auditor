# -*- coding: utf-8 -*-
"""
W3 批量改名预演引擎（纯 Python 版，不依赖 Revit/Dynamo）

在 W2 审计引擎基础上，加入"批量改名决策 + 复检闸门"：
- 对每个族，判断是否可以自动改名（安全底线：缺字段/字段不足的保守跳过）
- 模拟改名后用建议名重新校验一次 —— 这就是"复检"（真实场景里再跑一遍审计）
- 导出改名前后对照报告

这是 Dynamo 手册第 4 段"批量改名 + 事务处理"的决策逻辑预演。
真实改 Revit 模型需要 Dynamo 版脚本（dynamo/rename_in_revit.py），
但"改哪些、不改哪些、改完对不对"的判断逻辑在这里先跑对。

用法：
    python apply_rename.py <规则JSON> <族名清单> <对照报告输出>
"""

import sys
import os
import json
import csv
import datetime

# 精简版 Python 默认不把脚本目录加入搜索路径，这里显式自举，保证能 import 同目录的 audit_core
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# 复用 W2 引擎的校验函数（同目录）
from audit_core import load_rules, split_type_name, validate, can_autofix


def preview_rename(names, rules):
    """
    预演批量改名。返回 (统计字符串, 对照报告二维表)。
    对照报告列：原名/初检合规/违规原因/是否可自动改/建议名/复检合规/复检原因/处理方式
    """
    rows = []
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    sep = rules["separator"]

    for raw in names:
        family_name = split_type_name(raw)
        is_valid, reasons, suggestion = validate(family_name, rules)

        # 是否可自动改名：违规 + 有建议名 + 建议名不同 + 不含"不可自动修复"类原因
        auto_ok = (not is_valid) and suggestion and (suggestion != family_name) \
                  and can_autofix(reasons)

        new_name = suggestion if auto_ok else family_name

        # 复检闸门：用改名后的名字再校验一次
        post_valid, post_reasons, _ = validate(new_name, rules)

        if is_valid:
            action = "无需改动"
        elif auto_ok and post_valid:
            action = "自动改名(复检通过)"
        elif auto_ok and not post_valid:
            action = "建议名仍不合规->转人工"
        else:
            action = "标记人工(不可自动推断)"

        rows.append([
            raw,
            "是" if is_valid else "否",
            "; ".join(reasons),
            "是" if auto_ok else "否",
            new_name if not is_valid else family_name,
            "是" if (is_valid or post_valid) else "否",
            "; ".join(post_reasons) if (not is_valid and auto_ok) else "",
            action,
            timestamp,
        ])

    total = len(rows)
    violations = sum(1 for r in rows if r[1] == "否")
    auto_count = sum(1 for r in rows if r[3] == "是")
    passed = sum(1 for r in rows if r[7] == "自动改名(复检通过)")
    need_manual = sum(1 for r in rows if "人工" in r[7])
    summary = ("扫描 {} 个族，初检违规 {} 个，可自动改名 {} 个，"
               "复检通过 {} 个，需人工 {} 个").format(
                   total, violations, auto_count, passed, need_manual)
    return summary, rows


def export_csv(rows, path):
    with open(path, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f)
        writer.writerow(["原名", "初检合规", "违规原因", "可自动改名",
                         "建议名", "复检合规", "复检原因", "处理方式", "时间戳"])
        writer.writerows(rows)


def main():
    rules_path = sys.argv[1] if len(sys.argv) > 1 else "D:/FamilyNameAuditor/rules/default_rules.json"
    names_path = sys.argv[2] if len(sys.argv) > 2 else "D:/FamilyNameAuditor/testdata/family_names.txt"
    out_path = sys.argv[3] if len(sys.argv) > 3 else "D:/FamilyNameAuditor/rename_preview.csv"

    rules = load_rules(rules_path)
    with open(names_path, "r", encoding="utf-8") as f:
        names = [line.strip() for line in f if line.strip() and not line.startswith("#")]

    summary, rows = preview_rename(names, rules)
    export_csv(rows, out_path)

    print("=" * 64)
    print(summary)
    print("=" * 64)
    for r in rows:
        print("[{}] {}".format(r[7], r[0]))
        if r[4] and r[4] != r[0]:
            print("     建议名: " + r[4])
    print("=" * 64)
    print("对照报告已导出: " + out_path)


if __name__ == "__main__":
    main()
