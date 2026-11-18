# -*- coding: utf-8 -*-
"""
W3 Dynamo 版：真正改 Revit 模型的批量改名脚本

用法：Dynamo 里放 Python Script 节点，把本文件全部内容贴进去。
输入：
    IN[0] = 类别名（如 "Ducts"，对应 OST_Ducts）
    IN[1] = 规则 JSON 路径
    IN[2] = 是否执行改名 True/False（False=只审计预览）
输出：
    OUT = [统计信息, 改名前后对照表]

安全机制：
    1. 默认 false 只审计，确认无误再改 true
    2. 只改"可自动推断"的族，缺字段的保守跳过
    3. 改名后复检，复检不过的转人工标记
    4. 所有改名包在一个事务里，失败可整体回滚
"""

import clr
import json
import re
import csv
import datetime

clr.AddReference("RevitAPI")
clr.AddReference("RevitAPIUI")
from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

clr.AddReference("RevitServices")
from RevitServices.Persistence import DocumentManager
from RevitServices.Transactions import TransactionManager

doc = DocumentManager.Instance.CurrentDBDocument

category_name = IN[0]
rules_path = IN[1]
do_rename = IN[2]


# ---------- 工具函数（与 W2 引擎一致）----------
with open(rules_path, "r", encoding="utf-8") as f:
    RULES = json.load(f)
SEP = RULES["separator"]
SEGMENTS = RULES["segments"]
NORM = RULES["normalize"]


def fullwidth_to_halfwidth(s):
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


def clean_name(name):
    s = name
    if NORM.get("fullwidth_to_halfwidth", True):
        s = fullwidth_to_halfwidth(s)
    if NORM.get("trim_spaces", True):
        s = s.strip().replace(" ", "")
    return s


def split_type_name(raw):
    return raw.split(":")[0].strip()


def can_autofix(reasons):
    unfixable = ["字段不足", "缺字段"]
    return all(not any(u in r for r in reasons) for u in unfixable)


def validate(name):
    """校验单个族名，返回 (是否合规, 原因列表, 建议名)。"""
    reasons = []
    cleaned = clean_name(name)
    for bad in NORM.get("illegal_chars", []):
        if bad in cleaned:
            reasons.append('含非法词:"{}"'.format(bad))
            cleaned = cleaned.replace(bad, "")
    for alt in ["_", "－", "—", "–", " "]:
        cleaned = cleaned.replace(alt, SEP)
    while SEP + SEP in cleaned:
        cleaned = cleaned.replace(SEP + SEP, SEP)
    cleaned = cleaned.strip(SEP)
    parts = cleaned.split(SEP)

    required_count = sum(1 for s in SEGMENTS if s.get("required", False))
    if len(parts) < required_count:
        reasons.append("字段不足:{}段(需{})".format(len(parts), required_count))
    for seg in SEGMENTS:
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
    suggestion = SEP.join(parts) if not is_valid else name
    return is_valid, reasons, suggestion


# ---------- 收集族 ----------
bic = getattr(BuiltInCategory, "OST_" + category_name, None)
if bic is None:
    raise ValueError("未知类别:" + category_name)

symbols = FilteredElementCollector(doc).OfCategory(bic) \
    .WhereElementIsElementType().ToElements()

preview = []       # 改名前后对照表
rename_count = 0
timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

# ---------- 事务：所有改名在同一个事务里 ----------
if do_rename:
    TransactionManager.Instance.EnsureInTransaction(doc)

for sym in symbols:
    raw_name = sym.Name
    family_name = split_type_name(raw_name)
    is_valid, reasons, suggestion = validate(family_name)

    auto_ok = (not is_valid) and suggestion and (suggestion != family_name) \
              and can_autofix(reasons)
    changed = False
    post_valid = is_valid

    if do_rename and auto_ok:
        try:
            sym.Name = suggestion          # 关键：赋值即改名
            changed = True
            rename_count += 1
            post_valid, _, _ = validate(split_type_name(sym.Name))   # 复检
        except Exception as e:
            reasons.append("改名失败:{}".format(str(e)))

    if is_valid:
        action = "无需改动"
    elif auto_ok and post_valid:
        action = "自动改名(复检通过)"
    elif auto_ok and not post_valid:
        action = "建议名仍不合规->转人工"
    else:
        action = "标记人工(不可自动推断)"

    preview.append([
        raw_name,
        "是" if is_valid else "否",
        "; ".join(reasons),
        suggestion,
        "是" if changed else "否",
        action,
        timestamp,
    ])

if do_rename:
    TransactionManager.Instance.TransactionTaskDone()

# ---------- 导出对照报告 ----------
csv_path = r"D:\FamilyNameAuditor\rename_report.csv"
with open(csv_path, "w", newline="", encoding="utf-8-sig") as f:
    writer = csv.writer(f)
    writer.writerow(["原名", "初检合规", "违规原因", "建议名", "是否已改名", "处理方式", "时间戳"])
    writer.writerows(preview)

summary = "扫描 {} 个族，改名 {} 个，导出对照报告".format(len(preview), rename_count)
OUT = [summary, preview]
