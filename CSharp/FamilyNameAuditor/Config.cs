namespace FamilyNameAuditor
{
    // All paths are explicit and absolute on purpose: the smoke test and the
    // Revit commands must read the SAME rules file and produce COMPARABLE reports.
    public static class Config
    {
        public const string RulesPath = @"D:\FamilyNameAuditor\rules\default_rules.json";
        public const string TestDataPath = @"D:\FamilyNameAuditor\testdata\family_names.txt";
        public const string AuditReportPath = @"D:\FamilyNameAuditor\csharp_audit_report.csv";
        public const string RenameReportPath = @"D:\FamilyNameAuditor\csharp_rename_preview.csv";
        public const string LogicTestReportPath = @"D:\FamilyNameAuditor\csharp_logic_test_output.csv";
    }
}
