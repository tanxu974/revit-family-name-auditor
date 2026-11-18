using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyNameAuditor.Models;
using FamilyNameAuditor.Services;

namespace FamilyNameAuditor.Commands
{
    // WRITE command: only renames families that pass BOTH gates:
    //   1) no "missing required field" reason (inferable, not guesswork)
    //   2) re-validating the proposed name yields zero violations
    // All renames happen inside ONE transaction; any exception rolls everything back.
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RenameFamiliesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    message = "\u8BF7\u5148\u6253\u5F00\u4E00\u4E2A Revit \u9879\u76EE\u3002";
                    return Result.Failed;
                }
                Document doc = uidoc.Document;
                NamingRules rules = NamingRules.Load(Config.RulesPath);

                FilteredElementCollector collector =
                    new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));

                Dictionary<FamilySymbol, AuditResult> pending =
                    new Dictionary<FamilySymbol, AuditResult>();
                List<AuditResult> report = new List<AuditResult>();

                foreach (Element e in collector)
                {
                    FamilySymbol fs = e as FamilySymbol;
                    if (fs == null || string.IsNullOrEmpty(fs.Name)) continue;

                    string familyName = NamingAuditor.SplitTypeName(fs.Name);
                    ValidationOutcome outcome = NamingAuditor.Validate(familyName, rules);

                    AuditResult item = new AuditResult();
                    item.OriginalName = fs.Name;
                    if (fs.Category != null) item.Category = fs.Category.Name;
                    item.IsValid = outcome.IsValid;
                    item.Reasons.AddRange(outcome.Reasons);
                    item.Suggestion = outcome.Suggestion;
                    item.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    if (!outcome.IsValid
                        && NamingAuditor.CanAutoFix(outcome.Reasons)
                        && !string.IsNullOrEmpty(outcome.Suggestion)
                        && !outcome.Suggestion.Contains("\u003C\u5F85\u586B\u003E")
                        && outcome.Suggestion != familyName)
                    {
                        ValidationOutcome recheck = NamingAuditor.Validate(outcome.Suggestion, rules);
                        if (recheck.IsValid)
                        {
                            item.RecheckPassed = true;
                            item.CanAutoRename = true;
                            pending.Add(fs, item);
                        }
                    }

                    report.Add(item);
                }

                int changed = 0;
                if (pending.Count > 0)
                {
                    using (Transaction tx = new Transaction(doc, "FamilyNameAuditor - Batch Rename"))
                    {
                        tx.Start();
                        try
                        {
                            foreach (KeyValuePair<FamilySymbol, AuditResult> kv in pending)
                            {
                                kv.Key.Name = kv.Value.Suggestion;
                                changed++;
                            }
                            tx.Commit();
                        }
                        catch
                        {
                            try { tx.RollBack(); } catch { }
                            throw;
                        }
                    }
                }

                AuditReportWriter.Write(report, Config.RenameReportPath);

                TaskDialog td = new TaskDialog("\u65CF\u540D\u6279\u91CF\u89C4\u8303\u5316\u5B8C\u6210");
                td.MainInstruction = "\u5B9E\u9645\u6539\u540D " + changed + " \u4E2A\uFF0C\u4FDD\u6301\u4E0D\u52A8 "
                    + (report.Count - changed) + " \u4E2A";
                td.MainContent = "Rules: " + Config.RulesPath
                    + "\nReport: " + Config.RenameReportPath
                    + "\n\nSafety gates: inferable-only + recheck-passed."
                    + "\nMissing-field cases are left for manual review by design.";
                td.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
