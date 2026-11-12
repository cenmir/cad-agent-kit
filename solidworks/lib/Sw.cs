// Helpers shared by every job. job.ps1 supplies the using lines.
public static class Sw {
  // Work copies go here: <kit>\solidworks\work unless CAD_KIT_WORK says otherwise (set by job.ps1).
  public static string WorkDir { get {
    var d = System.Environment.GetEnvironmentVariable("CAD_KIT_WORK");
    return string.IsNullOrEmpty(d) ? Path.Combine(Path.GetTempPath(), "cad-agent-kit") : d; } }
  // The SolidWorks that runs in this desktop session.
  public static SldWorks App() {
    object o;
    try { o = Marshal.GetActiveObject("SldWorks.Application"); }
    catch (COMException ex) {
      throw new Exception("SolidWorks is not reachable (" + ex.Message.Trim() + "). Is it running, in this desktop " +
        "session, at the same elevation (both normal or both 'Run as administrator')?");
    }
    var sw = (SldWorks)o;
    // AddDimension2 otherwise opens the Modify box and waits for a click that never comes.
    sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);
    return sw;
  }
  // Copy a file into WorkDir under a new name (time stamp) and open the copy. Never touches the original.
  public static ModelDoc2 OpenCopy(SldWorks sw, string original) {
    string workDir = WorkDir;
    Directory.CreateDirectory(workDir);
    string copy = Path.Combine(workDir, Path.GetFileNameWithoutExtension(original) + "_" +
      DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.GetExtension(original));
    File.Copy(original, copy);
    return Open(sw, copy);
  }
  public static ModelDoc2 Open(SldWorks sw, string path) {
    int type = path.ToUpper().EndsWith(".SLDASM") ? (int)swDocumentTypes_e.swDocASSEMBLY
             : path.ToUpper().EndsWith(".SLDDRW") ? (int)swDocumentTypes_e.swDocDRAWING : (int)swDocumentTypes_e.swDocPART;
    int e = 0, w = 0;
    var doc = (ModelDoc2)sw.OpenDoc6(path, type, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
    if (doc == null) throw new Exception("OpenDoc6 failed, error " + e + " (65536: a file with this name is already open)");
    return doc;
  }
  public static void Close(SldWorks sw, ModelDoc2 doc) { if (doc != null) sw.CloseDoc(doc.GetTitle()); }

  // Features with an error after the last rebuild, or "ok".
  public static string Failures(ModelDoc2 doc) {
    var bad = new List<string>();
    for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) {
      bool warn; int code = f.GetErrorCode2(out warn);
      if (code != 0) bad.Add(f.Name + "(" + (swFeatureError_e)code + (warn ? ", warning" : "") + ")");
    }
    return bad.Count == 0 ? "ok" : string.Join(" ", bad);
  }
  // The feature tree: name, type, suppression, error.
  public static string Tree(ModelDoc2 doc) {
    var sb = new StringBuilder();
    for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) {
      bool warn; int code = f.GetErrorCode2(out warn);
      bool sup = ((bool[])f.IsSuppressed2((int)swInConfigurationOpts_e.swThisConfiguration, null))[0];
      sb.AppendLine("  " + f.Name + " [" + f.GetTypeName2() + "]" + (sup ? " suppressed" : "") +
        (code != 0 ? " ERROR " + (swFeatureError_e)code : ""));
    }
    return sb.ToString();
  }
  // Solid bodies with volume (mm^3) and exact extents along x, y, z (mm). Parts only.
  // Body2.GetBodyBox is a loose box (0.15 mm too big seen on a plain plate); extreme points are exact.
  public static string Bodies(ModelDoc2 doc) {
    var o = (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true);
    if (o == null) return "no solid bodies";
    var l = new List<string>();
    foreach (Body2 b in o) {
      var mp = (double[])b.GetMassProperties(1.0); var lo = new double[3]; var hi = new double[3];
      for (int k = 0; k < 3; k++) {
        double[] d = new double[3]; double x, y, z;
        d[k] = -1; b.GetExtremePoint(d[0], d[1], d[2], out x, out y, out z); lo[k] = new[] { x, y, z }[k];
        d[k] = 1;  b.GetExtremePoint(d[0], d[1], d[2], out x, out y, out z); hi[k] = new[] { x, y, z }[k];
      }
      l.Add(string.Format("{0} V={1:F3} mm3 x {2:F3}..{3:F3} y {4:F3}..{5:F3} z {6:F3}..{7:F3} mm", b.Name, mp[3] * 1e9,
        lo[0] * 1e3, hi[0] * 1e3, lo[1] * 1e3, hi[1] * 1e3, lo[2] * 1e3, hi[2] * 1e3));
    }
    return string.Join("\r\n", l);
  }
  // Global variables and equations with their values.
  public static string Equations(ModelDoc2 doc) {
    var eq = doc.GetEquationMgr(); var sb = new StringBuilder();
    for (int i = 0; i < eq.GetCount(); i++) sb.AppendLine("  " + eq.get_Equation(i) + "  => " + eq.get_Value(i));
    return sb.ToString();
  }
  // Set a global variable ("N" = 20), or add it if it does not exist.
  public static void SetGlobal(ModelDoc2 doc, string name, string value) {
    var eq = doc.GetEquationMgr(); string key = "\"" + name + "\"=";
    for (int i = 0; i < eq.GetCount(); i++)
      if (eq.get_Equation(i).Replace(" ", "").StartsWith(key)) { eq.set_Equation(i, "\"" + name + "\" = " + value); return; }
    if (eq.Add2(-1, "\"" + name + "\" = " + value, true) < 0) throw new Exception("cannot add global " + name);
  }
  // Set several globals, evaluate, rebuild, then read every one back. Returns "" when all took
  // effect, else "NOT APPLIED: ...". A value that drives some dimension out of range (a negative
  // fillet radius, say) can be refused without any rebuild error, and from then on further changes
  // may be ignored too, so a sweep must check the values it set, not only the feature errors.
  public static string SetGlobals(ModelDoc2 doc, string[] names, string[] values) {
    for (int k = 0; k < names.Length; k++) SetGlobal(doc, names[k], values[k]);
    var eq = doc.GetEquationMgr(); eq.EvaluateAll(); doc.ForceRebuild3(false);
    var bad = new List<string>();
    for (int k = 0; k < names.Length; k++) {
      double want; if (!double.TryParse(values[k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out want)) continue;
      double got = double.NaN; string key = "\"" + names[k] + "\"=";
      for (int i = 0; i < eq.GetCount(); i++) if (eq.get_Equation(i).Replace(" ", "").StartsWith(key)) got = eq.get_Value(i);
      if (double.IsNaN(got) || Math.Abs(got - want) > 1e-9 * Math.Max(1, Math.Abs(want))) bad.Add(names[k] + "=" + got + " (asked " + values[k] + ")");
    }
    return bad.Count == 0 ? "" : "NOT APPLIED: " + string.Join(", ", bad);
  }
}
