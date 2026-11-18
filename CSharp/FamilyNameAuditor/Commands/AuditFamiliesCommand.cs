using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyNameAuditor.Models;
using FamilyNameAuditor.Services;

namespace FamilyNameAuditor.Commands
{
    // READ-ONLY command: collects FamilySymbol names, audits them, exports CSV.
    // Never opens a Transaction - the model cannot be modified by this command.
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AuditFamiliesCommand : IExternalCommand
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

                FilteredElementCollector collector =
                    new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));

                List<string> names = new List<string>();
                foreach (Element e in collector)
                {
                    FamilySymbol fs = e as FamilySymbol;
                    if (fs != null && !string.IsNullOrEmpty(fs.Name)) names.Add(fs.Name);
                }

                NamingRules rules = NamingRules.Load(Config.RulesPath);
                List<AuditResult> results = NamingAuditor.AuditBatch(names, rules, false);
                AuditReportWriter.Write(results, Config.AuditReportPath);

                int violations = 0;
                foreach (AuditResult r in results)
                {
                    if (!r.IsValid) violations++;
                }

                TaskDialog td = new TaskDialog("\u65CF\u547D\u540D\u5BA1\u8BA1\u5B8C\u6210");
                td.MainInstruction = "\u626B\u63CF " + results.Count + " \u4E2A\u65CF\uFF0C\u8FDD\u89C4 " + violations + " \u4E2A";
                td.MainContent = "Report: " + Config.AuditReportPath
                    + "\n\nRead-only audit. The model was NOT modified."
                    + "\nRun 'Apply Rename' to fix the safe cases.";
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
