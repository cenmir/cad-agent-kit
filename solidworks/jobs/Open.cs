// Open a document visibly and make it the active one (for screenshots and dialogs). Arg: the file.
public static class Job {
  public static string Run(string file) {
    var sw = Sw.App(); var doc = Sw.Open(sw, file);
    int e = 0; sw.ActivateDoc3(doc.GetTitle(), false, 0, ref e); doc.ShowNamedView2("*Isometric", 7); doc.ViewZoomtofit2();
    return "active: " + doc.GetTitle();
  }
}
