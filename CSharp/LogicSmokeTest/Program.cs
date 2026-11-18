using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FamilyNameAuditor;
using FamilyNameAuditor.Models;
using FamilyNameAuditor.Services;

namespace LogicSmokeTest
{
    // Runs the pure C# logic against the SAME rules JSON and 15 test family names
    // used by the Python engine. Expected output matches W3:
    //   15 families / 11 violations / 1 auto-rename (recheck passed) / 10 manual
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                NamingRules rules = NamingRules.Load(Config.RulesPath);

                List<string> names = new List<string>();
                foreach (string line in File.ReadAllLines(Config.TestDataPath, Encoding.UTF8))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0 && !trimmed.StartsWith("#")) names.Add(trimmed);
                }

                List<AuditResult> results = NamingAuditor.AuditBatch(names, rules, true);
                AuditReportWriter.Write(results, Config.LogicTestReportPath);

                int violations = 0;
                int auto = 0;
                int recheck = 0;
                int manual = 0;
                foreach (AuditResult r in results)
                {
                    if (!r.IsValid) violations++;
                    if (r.CanAutoRename)
                    {
                        auto++;
                        if (r.RecheckPassed) recheck++;
                    }
                    if (!r.IsValid && !r.CanAutoRename) manual++;
                }

                Console.WriteLine("============================================");
                Console.WriteLine(" C# Logic Smoke Test (no Revit required)");
                Console.WriteLine("============================================");
                Console.WriteLine(string.Format(
                    "\u626B\u63CF {0} \u4E2A\u65CF\uFF0C\u8FDD\u89C4 {1} \u4E2A\uFF0C\u53EF\u81EA\u52A8\u6539\u540D {2} \u4E2A\uFF0C\u590D\u68C0\u901A\u8FC7 {3} \u4E2A\uFF0C\u9700\u4EBA\u5DE5 {4} \u4E2A",
                    results.Count, violations, auto, recheck, manual));
                Console.WriteLine("Report: " + Config.LogicTestReportPath);
                Console.WriteLine("============================================");

                foreach (AuditResult r in results)
                {
                    string flag = r.IsValid ? "OK " : "BAD";
                    Console.WriteLine("[" + flag + "] " + r.OriginalName);
                    if (r.Reasons.Count > 0)
                    {
                        Console.WriteLine("     Reasons: " + string.Join("; ", r.Reasons.ToArray()));
                        Console.WriteLine("     Suggest: " + r.Suggestion
                            + (r.CanAutoRename ? "  (auto, recheck OK)" : "  (manual)"));
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED: " + ex.Message);
                return 1;
            }
        }
    }
}
