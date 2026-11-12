// Inspect a copy of a part: equations, feature tree with errors, solid bodies. Arg: the original file.
public static class Job {
  public static string Run(string file) {
    var sw = Sw.App(); ModelDoc2 doc = null; var sb = new StringBuilder();
    try {
      doc = Sw.OpenCopy(sw, file);
      doc.ForceRebuild3(false);
      sb.AppendLine("copy: " + doc.GetPathName());
      sb.AppendLine("equations:"); sb.Append(Sw.Equations(doc));
      sb.AppendLine("features:"); sb.Append(Sw.Tree(doc));
      sb.AppendLine("rebuild: " + Sw.Failures(doc));
      if (doc.GetType() == (int)swDocumentTypes_e.swDocPART) sb.AppendLine(Sw.Bodies(doc));
    } catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex.Message); }
    finally { Sw.Close(sw, doc); }
    return sb.ToString();
  }
}
