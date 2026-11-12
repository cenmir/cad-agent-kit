// Parameter sweep on a copy: Arg = "file|Name=v1,v2,...|Name2=w1,w2,..." (all combinations).
// Sets the globals, rebuilds, and reports the failing features and the solid bodies of each case.
public static class Job {
  public static string Run(string arg) {
    var parts = arg.Split('|'); var sw = Sw.App(); ModelDoc2 doc = null; var sb = new StringBuilder();
    var names = new List<string>(); var values = new List<string[]>();
    for (int i = 1; i < parts.Length; i++) { var kv = parts[i].Split('='); names.Add(kv[0].Trim()); values.Add(kv[1].Split(',')); }
    try {
      doc = Sw.OpenCopy(sw, parts[0]);
      doc.ForceRebuild3(false); sb.AppendLine("as delivered: " + Sw.Failures(doc));
      var idx = new int[names.Count];
      while (true) {
        var label = new List<string>();
        for (int k = 0; k < names.Count; k++) { Sw.SetGlobal(doc, names[k], values[k][idx[k]].Trim()); label.Add(names[k] + "=" + values[k][idx[k]].Trim()); }
        doc.ForceRebuild3(false);
        sb.AppendLine(string.Join(" ", label) + " | " + Sw.Failures(doc) + " | " + Sw.Bodies(doc).Replace("\r\n", " ; "));
        int j = 0; while (j < idx.Length && ++idx[j] == values[j].Length) { idx[j] = 0; j++; }
        if (j == idx.Length) break;
      }
    } catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex.Message); }
    finally { Sw.Close(sw, doc); }   // the copy is not saved
    return sb.ToString();
  }
}
