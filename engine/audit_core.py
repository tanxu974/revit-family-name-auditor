# -*- coding: utf-8 -*-
"""
族命名审计核心引擎（纯 Python 版，不依赖 Revit/Dynamo）

这是 Dynamo 手册第 3 段"核心校验函数"的等价实现：
- 同一份规则 JSON（rules/default_rules.json）
- 同一套校验逻辑（清洗/分自动修复/逐段校验/建议名）
- 唯一区别：输入是一列族名字符串，而不是从 Revit 模型里收集的 FamilySymbol

意义：先在轻量 Python 环境把"审计逻辑"跑对，再原样搬进 Dynamo Python 节点，
     Dynamo 里只需额外加"收集族 + 改名事务"两段——核心零改动。

用法：
    python audit_core.py <规则JSON路径> <族名清单路径> <报告输出路径>
不传参数则使用默认路径（见 main）。
"""

import json
import re
import csv
import sys
import datetime


def load_rules(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def fullwidth_to_halfwidth(s):
    """全角转半角：解决中文输入法产生的全角横杠/乘号/括号。"""
    result = []
    for ch in s:
        code = ord(ch)
        if code == 0x3000:
            code = 0x20
        elif 0xFF01 <= code <= 0xFF5E:
            code -= 0xFEE0
        elif ch == "×":
            code = ord("x")
        result.append(chr(code))
    return "".join(result)


def clean_name(name, rules):
    """按配置清洗族名：全角转半角 + 去空格。"""
    norm = rules["normalize"]
    s = name
    if norm.get("fullwidth_to_halfwidth", True):
        s = fullwidth_to_halfwidth(s)
    if norm.get("trim_spaces", True):
        s = s.strip().replace(" ", "")
    return s


def split_type_name(raw):
    """Revit 里 '族名 : 类型名' 格式取冒号前的族名（v1 只审族名）。"""
    return raw.split(":")[0].strip()


def can_autofix(reasons):
    """安全底线：只有'可推断'类违规才自动改名，缺字段的保守不改。"""
    unfixable = ["字段不足", "缺字段"]
    return all(not any(u in r for r in reasons) for u in unfixable)


def validate(name, rules):
    """
    校验单个族名，返回 (是否合规, 违规原因列表, 建议名)。
    与 Dynamo 手册第 3 段 validate() 逻辑完全一致。
    """
    reasons = []
    sep = rules["separator"]
    segments = rules["segments"]
    norm = rules["normalize"]
    cleaned = clean_name(name, rules)

    # 非法词检查
    for bad in norm.get("illegal_chars", []):
        if bad in cleaned:
            reasons.append('含非法词:"{}"'.format(bad))
            cleaned = cleaned.replace(bad, "")

    # 统一分隔符 + 合并连续分隔符
    for alt in ["_", "－", "—", "–", " "]:
        cleaned = cleaned.replace(alt, sep)
    while sep + sep in cleaned:
        cleaned = cleaned.replace(sep + sep, sep)
    cleaned = cleaned.strip(sep)

    parts = cleaned.split(sep)

    # 必填段数量校验
    required_count = sum(1 for s in segments if s.get("required", False))
    if len(parts) < required_count:
        reasons.append("字段不足:{}段(需{})".format(len(parts), required_count))

    # 逐段校验
    for seg in segments:
        idx = seg["index"]
        val = parts[idx] if idx < len(parts) else None
        if seg.get("required", False) and (val is None or val == ""):
            reasons.append("缺字段:{}".format(seg["name"]))
            continue
        if val is None:
            continue
        if seg["type"] == "enum" and val not in seg["allowed"]:
            reasons.append('{}取值非法:"{}"'.format(seg["name"], val))
        elif seg["type"] == "regex" and not re.match(seg["pattern"], val):
            reasons.append('{}格式错误:"{}"'.format(seg["name"], val))

    is_valid = len(reasons) == 0
    suggestion = sep.join(parts) if not is_valid else name
    return is_valid, reasons, suggestion


def audit(names, rules, do_rename=True):
    """
    批量审计。返回 (统计信息字符串, 报告二维列表)。
    do_rename 在纯 Python 版里表示"是否给出可自动修复的建议名"（不真改文件）。
    """
    report = []
    rename_count = 0
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    for raw in names:
        family_name = split_type_name(raw)
        is_valid, reasons, suggestion = validate(family_name, rules)

        changed = False
        auto_ok = (not is_valid) and suggestion and ("<待填>" not in suggestion) \
                  and (suggestion != family_name) and can_autofix(reasons)
        if do_rename and auto_ok:
            changed = True
            rename_count += 1

        report.append([
            raw,
            "N/A",  # 纯 Python 版无 Revit 类别信息
            "是" if is_valid else "否",
            "; ".join(reasons),
            suggestion,
            "是" if changed else "否",
            timestamp,
        ])

    total = len(report)
    violations = sum(1 for r in report if r[2] == "否")
    auto_fixable = sum(1 for r in report if r[5] == "是")
    summary = "扫描 {} 个族，违规 {} 个，可自动修复 {} 个".format(
        total, violations, auto_fixable
    )
    return summary, report


def export_csv(report, path):
    with open(path, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f)
        writer.writerow(["原名", "类别", "是否合规", "违规原因", "建议名", "是否已改名", "时间戳"])
        writer.writerows(report)


def main():
    rules_path = sys.argv[1] if len(sys.argv) > 1 else "D:/FamilyNameAuditor/rules/default_rules.json"
    names_path = sys.argv[2] if len(sys.argv) > 2 else "D:/FamilyNameAuditor/testdata/family_names.txt"
    out_path = sys.argv[3] if len(sys.argv) > 3 else "D:/FamilyNameAuditor/audit_report.csv"

    rules = load_rules(rules_path)
    with open(names_path, "r", encoding="utf-8") as f:
        names = [line.strip() for line in f if line.strip() and not line.startswith("#")]

    summary, report = audit(names, rules)
    export_csv(report, out_path)

    print("=" * 60)
    print(summary)
    print("=" * 60)
    for row in report:
        flag = "OK " if row[2] == "是" else "BAD"
        print("[{}] {}".format(flag, row[0]))
        if row[3]:
            print("     原因: " + row[3])
            print("     建议: " + row[4] + ("  (可自动修复)" if row[5] == "是" else "  (需人工)"))
    print("=" * 60)
    print("报告已导出: " + out_path)


if __name__ == "__main__":
    main()
