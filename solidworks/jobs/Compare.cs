// Rebuild two parts over the same parameter values and compare them: rebuild errors, number of
// bodies, volume. For checking a rebuilt or ported model against a reference part.
// Arg: "partA|partB|N=8,20,50|m=1,2|suppress=Chamfer1" (all combinations; suppress applies to both
// parts, features missing in one part are ignored). Works on copies, saves nothing.
public static class Job {
  public static string Run(string arg) {
    var parts = arg.Split('|'); var sw = Sw.App(); var sb = new StringBuilder();
    var names = new List<string>(); var values = new List<string[]>(); var sup = new List<string>();
    for (int i = 2; i < parts.Length; i++) {
      var kv = parts[i].Split('='); if (kv[0].Trim() == "suppress") { sup.AddRange(kv[1].Split(',')); continue; }
      names.Add(kv[0].Trim()); values.Add(kv[1].Split(','));
    }
    ModelDoc2 a = null, b = null;
    try {
      a = Sw.OpenCopy(sw, parts[0]); b = Sw.OpenCopy(sw, parts[1]); string pa = a.GetPathName(), pb = b.GetPathName();
      Suppress(a, b, sup);
      sb.AppendLine("A = " + Path.GetFileName(parts[0]) + ", B = " + Path.GetFileName(parts[1]));
      var idx = new int[names.Count]; double worst = 0;
      while (true) {
        var label = new List<string>();
        for (int k = 0; k < names.Count; k++) label.Add(names[k] + "=" + values[k][idx[k]].Trim());
        // Each document in turn is made the active one: equations of a document that is open but not
        // active were not re-evaluated by ForceRebuild3, and that part silently kept its old size.
        var vals = new string[names.Count]; for (int k = 0; k < names.Count; k++) vals[k] = values[k][idx[k]].Trim();
        string applied = "";
        foreach (var d in new[] { a, b }) {
          int err = 0; sw.ActivateDoc3(d.GetTitle(), false, 0, ref err);
          string r = Sw.SetGlobals(d, names.ToArray(), vals); if (r != "") applied += (d == a ? " A " : " B ") + r;
        }
        double va = Vol(a), vb = Vol(b), rel = Math.Abs(va - vb) / Math.Max(Math.Abs(vb), 1e-9);
        worst = Math.Max(worst, rel);
        sb.AppendLine(string.Format("{0} | A {1} bodies {2} V={3:F3} | B {4} bodies {5} V={6:F3} | dV {7:E2}{8}", string.Join(" ", label),
          Sw.Failures(a), Count(a), va, Sw.Failures(b), Count(b), vb, rel, rel > 1e-4 || Count(a) != Count(b) ? "  <-- DIFFERENT" : "") + applied);
        // after a failed rebuild SolidWorks may ignore the next changes: reopen the unsaved copies
        if (applied != "" || Sw.Failures(a) != "ok" || Sw.Failures(b) != "ok") {
          Sw.Close(sw, a); Sw.Close(sw, b); a = Sw.Open(sw, pa); b = Sw.Open(sw, pb); Suppress(a, b, sup);
        }
        int j = 0; while (j < idx.Length && ++idx[j] == values[j].Length) { idx[j] = 0; j++; }
        if (j == idx.Length) break;
      }
      sb.AppendLine("largest relative volume difference " + worst.ToString("E2"));
    } catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex.Message); }
    finally { Sw.Close(sw, a); Sw.Close(sw, b); }
    return sb.ToString();
  }
  static void Suppress(ModelDoc2 a, ModelDoc2 b, List<string> sup) {
    foreach (var d in new[] { a, b }) foreach (var s in sup) {
      var f = (Feature)((PartDoc)d).FeatureByName(s.Trim());
      if (f != null) f.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
    }
  }
  static object[] Bodies(ModelDoc2 d) { return (object[])((PartDoc)d).GetBodies2((int)swBodyType_e.swSolidBody, true); }
  static int Count(ModelDoc2 d) { var o = Bodies(d); return o == null ? 0 : o.Length; }
  static double Vol(ModelDoc2 d) { var o = Bodies(d); double v = 0; if (o != null) foreach (Body2 x in o) v += ((double[])x.GetMassProperties(1.0))[3] * 1e9; return v; }
}
