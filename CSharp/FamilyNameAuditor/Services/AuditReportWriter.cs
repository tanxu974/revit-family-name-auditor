using System.Collections.Generic;
using System.IO;
using System.Text;
using FamilyNameAuditor.Services;

namespace FamilyNameAuditor.Services
{
    // Writes the same 7-column CSV as the Python version, with UTF-8 BOM (utf-8-sig)
    // so Chinese opens cleanly in Excel / WPS.
    public static class AuditReportWriter
    {
        public static void Write(List<AuditResult> results, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\u539F\u540D,\u7C7B\u522B,\u662F\u5426\u5408\u89C4,\u8FDD\u89C4\u539F\u56E0,\u5EFA\u8BAE\u540D,\u662F\u5426\u5DF2\u6539\u540D,\u65F6\u95F4\u6233");

            foreach (AuditResult r in results)
            {
                sb.Append(Escape(r.OriginalName)).Append(",");
                sb.Append(Escape(r.Category)).Append(",");
                sb.Append(r.IsValid ? "\u662F" : "\u5426").Append(",");
                sb.Append(Escape(string.Join("; ", r.Reasons.ToArray()))).Append(",");
                sb.Append(Escape(r.Suggestion)).Append(",");
                sb.Append(r.CanAutoRename ? "\u662F" : "\u5426").Append(",");
                sb.Append(Escape(r.Timestamp)).AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needQuote = value.Contains(",") || value.Contains("\"")
                || value.Contains("\r") || value.Contains("\n");
            string s = value.Replace("\"", "\"\"");
            if (needQuote) return "\"" + s + "\"";
            return s;
        }
    }
}
