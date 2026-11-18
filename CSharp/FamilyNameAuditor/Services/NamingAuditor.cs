using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FamilyNameAuditor.Models;

namespace FamilyNameAuditor.Services
{
    // Plain C# audit engine. NO Revit API reference here.
    // Logic is line-by-line equivalent to W2/W3 Python engine (engine/audit_core.py).
    // This separation is deliberate: the decision logic can be unit-tested without Revit.
    public class AuditResult
    {
        public string OriginalName { get; set; }
        public string Category { get; set; }
        public bool IsValid { get; set; }
        public List<string> Reasons { get; set; }
        public string Suggestion { get; set; }
        public bool CanAutoRename { get; set; }
        public bool RecheckPassed { get; set; }
        public string Timestamp { get; set; }

        public AuditResult()
        {
            Reasons = new List<string>();
            Category = "N/A";
            Suggestion = "";
            Timestamp = "";
        }
    }

    public class ValidationOutcome
    {
        public bool IsValid { get; set; }
        public List<string> Reasons { get; set; }
        public string Suggestion { get; set; }

        public ValidationOutcome()
        {
            Reasons = new List<string>();
            Suggestion = "";
        }
    }

    public static class NamingAuditor
    {
        // Mirrors fullwidth_to_halfwidth() in Python: handles U+3000, FF01-FF5E, and the multiplication sign.
        public static string FullwidthToHalfwidth(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s)
            {
                int code = (int)ch;
                if (code == 0x3000) code = 0x20;
                else if (code >= 0xFF01 && code <= 0xFF5E) code -= 0xFEE0;
                if (ch == '\u00D7') code = (int)'x';
                sb.Append((char)code);
            }
            return sb.ToString();
        }

        public static string CleanName(string name, NamingRules rules)
        {
            string s = name;
            if (rules.Normalize.FullwidthToHalfwidth) s = FullwidthToHalfwidth(s);
            if (rules.Normalize.TrimSpaces)
            {
                s = s.Trim();
                s = s.Replace(" ", "");
            }
            return s;
        }

        // Revit sometimes stores "Family : Type". v1 audits the family name only.
        public static string SplitTypeName(string raw)
        {
            int idx = raw.IndexOf(':');
            if (idx < 0) return raw.Trim();
            return raw.Substring(0, idx).Trim();
        }

        // Safety gate #1: never auto-rename when a required field is missing.
        public static bool CanAutoFix(List<string> reasons)
        {
            foreach (string reason in reasons)
            {
                if (reason.Contains("\u5B57\u6BB5\u4E0D\u8DB3") || reason.Contains("\u7F3A\u5B57\u6BB5"))
                    return false;
            }
            return true;
        }

        public static ValidationOutcome Validate(string name, NamingRules rules)
        {
            ValidationOutcome outcome = new ValidationOutcome();
            List<string> reasons = outcome.Reasons;
            string sep = rules.Separator;
            string cleaned = CleanName(name, rules);

            // Remove illegal tokens, recording each removal as a reason.
            foreach (string bad in rules.Normalize.IllegalChars)
            {
                if (cleaned.Contains(bad))
                {
                    reasons.Add("\u542B\u975E\u6CD5\u8BCD:\"" + bad + "\"");
                    cleaned = cleaned.Replace(bad, "");
                }
            }

            // Unify alternative separators, then collapse repeats (mirrors Python).
            string[] alternatives = new string[] { "_", "\uFF0D", "\u2014", "\u2013", " " };
            foreach (string alt in alternatives)
            {
                cleaned = cleaned.Replace(alt, sep);
            }
            string doubleSep = sep + sep;
            while (cleaned.Contains(doubleSep))
            {
                cleaned = cleaned.Replace(doubleSep, sep);
            }
            cleaned = cleaned.Trim(sep.ToCharArray());

            string[] parts = cleaned.Split(new string[] { sep }, StringSplitOptions.None);

            int requiredCount = 0;
            foreach (RuleSegment seg in rules.Segments)
            {
                if (seg.Required) requiredCount++;
            }
            if (parts.Length < requiredCount)
            {
                reasons.Add("\u5B57\u6BB5\u4E0D\u8DB3:" + parts.Length + "\u6BB5(\u9700" + requiredCount + ")");
            }

            foreach (RuleSegment seg in rules.Segments)
            {
                int idx = seg.Index;
                string val = null;
                if (idx >= 0 && idx < parts.Length) val = parts[idx];

                if (seg.Required && (val == null || val.Length == 0))
                {
                    reasons.Add("\u7F3A\u5B57\u6BB5:" + seg.Name);
                    continue;
                }
                if (val == null) continue;

                if (seg.Type == "enum")
                {
                    bool found = false;
                    foreach (string allowed in seg.Allowed)
                    {
                        if (allowed == val) { found = true; break; }
                    }
                    if (!found)
                        reasons.Add(seg.Name + "\u53D6\u503C\u975E\u6CD5:\"" + val + "\"");
                }
                else if (seg.Type == "regex")
                {
                    if (!string.IsNullOrEmpty(seg.Pattern) && !Regex.IsMatch(val, seg.Pattern))
                        reasons.Add(seg.Name + "\u683C\u5F0F\u9519\u8BEF:\"" + val + "\"");
                }
            }

            outcome.IsValid = (reasons.Count == 0);
            outcome.Suggestion = outcome.IsValid ? name : string.Join(sep, parts);
            return outcome;
        }

        // Safety gate #2: re-validate the proposed name. Only a passing recheck
        // allows auto-rename. This is the exact rule proven in W3 (11 violations -> 1 auto).
        public static List<AuditResult> AuditBatch(List<string> names, NamingRules rules, bool allowRename)
        {
            List<AuditResult> results = new List<AuditResult>();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (string raw in names)
            {
                string familyName = SplitTypeName(raw);
                ValidationOutcome outcome = Validate(familyName, rules);

                AuditResult item = new AuditResult();
                item.OriginalName = raw;
                item.IsValid = outcome.IsValid;
                item.Reasons.AddRange(outcome.Reasons);
                item.Suggestion = outcome.Suggestion;
                item.Timestamp = timestamp;

                if (outcome.IsValid)
                {
                    item.RecheckPassed = true;
                }
                else if (allowRename && CanAutoFix(outcome.Reasons)
                    && !string.IsNullOrEmpty(outcome.Suggestion)
                    && !outcome.Suggestion.Contains("\u003C\u5F85\u586B\u003E")
                    && outcome.Suggestion != familyName)
                {
                    ValidationOutcome recheck = Validate(outcome.Suggestion, rules);
                    item.RecheckPassed = recheck.IsValid;
                    item.CanAutoRename = recheck.IsValid;
                }

                results.Add(item);
            }
            return results;
        }
    }
}
