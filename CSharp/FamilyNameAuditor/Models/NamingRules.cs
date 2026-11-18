using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FamilyNameAuditor.Models
{
    // Rule model: maps rules/default_rules.json 1:1.
    // Parsed with JavaScriptSerializer from System.Web.Extensions.dll
    // (ships with .NET Framework 4.8 - no third-party NuGet needed).
    public class RuleSegment
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public bool Required { get; set; }
        public string Type { get; set; }
        public List<string> Allowed { get; set; }
        public string Pattern { get; set; }

        public RuleSegment()
        {
            Allowed = new List<string>();
            Name = "";
            Type = "";
            Pattern = "";
        }
    }

    public class NormalizeSettings
    {
        public bool FullwidthToHalfwidth { get; set; }
        public bool TrimSpaces { get; set; }
        public List<string> IllegalChars { get; set; }

        public NormalizeSettings()
        {
            IllegalChars = new List<string>();
            FullwidthToHalfwidth = true;
            TrimSpaces = true;
        }
    }

    public class NamingRules
    {
        public string Version { get; set; }
        public string Separator { get; set; }
        public List<RuleSegment> Segments { get; set; }
        public NormalizeSettings Normalize { get; set; }

        public NamingRules()
        {
            Version = "";
            Separator = "-";
            Segments = new List<RuleSegment>();
            Normalize = new NormalizeSettings();
        }

        public static NamingRules Load(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            object raw = serializer.DeserializeObject(json);
            Dictionary<string, object> dict = raw as Dictionary<string, object>;
            if (dict == null)
            {
                throw new InvalidDataException("Rule JSON root is not an object: " + path);
            }

            NamingRules rules = new NamingRules();
            rules.Version = GetString(dict, "version");
            rules.Separator = GetString(dict, "separator");

            object segsRaw;
            if (dict.TryGetValue("segments", out segsRaw))
            {
                object[] segArray = segsRaw as object[];
                if (segArray != null)
                {
                    foreach (object item in segArray)
                    {
                        Dictionary<string, object> sd = item as Dictionary<string, object>;
                        if (sd == null) continue;

                        RuleSegment seg = new RuleSegment();
                        object tmp;
                        if (sd.TryGetValue("index", out tmp) && tmp != null)
                            seg.Index = Convert.ToInt32(tmp);
                        seg.Name = GetString(sd, "name");
                        if (sd.TryGetValue("required", out tmp) && tmp != null)
                            seg.Required = Convert.ToBoolean(tmp);
                        seg.Type = GetString(sd, "type");
                        seg.Pattern = GetString(sd, "pattern");
                        seg.Allowed = GetStringList(sd, "allowed");
                        rules.Segments.Add(seg);
                    }
                }
            }

            object normRaw;
            if (dict.TryGetValue("normalize", out normRaw))
            {
                Dictionary<string, object> nd = normRaw as Dictionary<string, object>;
                if (nd != null)
                {
                    object tmp;
                    if (nd.TryGetValue("fullwidth_to_halfwidth", out tmp) && tmp != null)
                        rules.Normalize.FullwidthToHalfwidth = Convert.ToBoolean(tmp);
                    if (nd.TryGetValue("trim_spaces", out tmp) && tmp != null)
                        rules.Normalize.TrimSpaces = Convert.ToBoolean(tmp);
                    rules.Normalize.IllegalChars = GetStringList(nd, "illegal_chars");
                }
            }

            return rules;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            object value;
            if (dict.TryGetValue(key, out value) && value != null) return value.ToString();
            return "";
        }

        private static List<string> GetStringList(Dictionary<string, object> dict, string key)
        {
            List<string> result = new List<string>();
            object value;
            if (dict.TryGetValue(key, out value))
            {
                object[] array = value as object[];
                if (array != null)
                {
                    foreach (object item in array)
                    {
                        if (item != null) result.Add(item.ToString());
                    }
                }
            }
            return result;
        }
    }
}
