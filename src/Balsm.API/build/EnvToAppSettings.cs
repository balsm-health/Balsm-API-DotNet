using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

// MSBuild task loaded by RoslynCodeTaskFactory (see Balsm.API.csproj). This file
// is NOT compiled into the API assembly — it is excluded via <Compile Remove>.
//
// Transforms a repo-root .env (KEY=VALUE, `__` = nesting) into a nested JSON
// appsettings file the host loads on top of appsettings.json.
public sealed class EnvToAppSettings : Task
{
    [Required] public string EnvFile { get; set; } = "";
    [Required] public string OutFile { get; set; } = "";

    public override bool Execute()
    {
        if (!File.Exists(EnvFile))
        {
            Log.LogMessage(MessageImportance.Normal, "[EnvToAppSettings] No .env at " + EnvFile + " - skipping.");
            return true;
        }

        var root = new Dictionary<string, object>();
        foreach (var raw in File.ReadAllLines(EnvFile))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line.Substring(0, eq).Trim();
            var val = line.Substring(eq + 1).Trim();
            if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"')
                val = val.Substring(1, val.Length - 2);

            var parts = key.Replace(":", "__").Split(new[] { "__" }, StringSplitOptions.None);
            var node = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!node.ContainsKey(parts[i]) || node[parts[i]] is not Dictionary<string, object>)
                    node[parts[i]] = new Dictionary<string, object>();
                node = (Dictionary<string, object>)node[parts[i]];
            }
            node[parts[parts.Length - 1]] = val;
        }

        File.WriteAllText(OutFile, Serialize(root));
        Log.LogMessage(MessageImportance.High, "[EnvToAppSettings] Wrote " + OutFile);
        return true;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Serialize(object o)
    {
        if (o is Dictionary<string, object> d)
        {
            var sb = new StringBuilder("{");
            var first = true;
            foreach (var kv in d)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append("\":").Append(Serialize(kv.Value));
            }
            return sb.Append('}').ToString();
        }

        var v = Convert.ToString(o) ?? "";
        if (v == "true" || v == "false") return v;

        var num = v.Length > 0;
        foreach (var c in v)
            if (!(char.IsDigit(c) || c == '.' || c == '-')) { num = false; break; }

        return num ? v : "\"" + Escape(v) + "\"";
    }
}
