// Smoke test: is SolidWorks reachable, which version, which documents are open.
public static class Job {
  public static string Run(string arg) {
    var sw = Sw.App(); var sb = new StringBuilder();
    sb.AppendLine("SolidWorks " + sw.RevisionNumber() + ", visible " + sw.Visible);
    var docs = (object[])sw.GetDocuments();
    sb.AppendLine("open documents: " + (docs == null ? 0 : docs.Length));
    if (docs != null) foreach (ModelDoc2 d in docs) sb.AppendLine("  " + d.GetPathName());
    return sb.ToString();
  }
}
