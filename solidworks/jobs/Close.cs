// Close documents without saving. Arg: a title ("UiTest.SLDPRT"), or empty for every document in the
// kit's work folder. Lists what is open first.
public static class Job {
  public static string Run(string title) {
    var sw = Sw.App(); var sb = new StringBuilder(); var docs = (object[])sw.GetDocuments();
    if (docs == null) return "nothing open";
    foreach (ModelDoc2 d in docs) {
      string p = d.GetPathName(); bool hit = title != "" ? d.GetTitle().StartsWith(title, StringComparison.OrdinalIgnoreCase)
        : p.StartsWith(Sw.WorkDir, StringComparison.OrdinalIgnoreCase);
      sb.AppendLine((hit ? "closed  " : "left    ") + p);
      if (hit) sw.CloseDoc(d.GetTitle());   // CloseDoc does not save
    }
    return sb.ToString();
  }
}
