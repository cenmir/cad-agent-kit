// Open Tools > Equations of the active document. The dialog is modal: this job returns only when
// the dialog closes, so run it through remote.ps1 (which waits) and close it with ui.ps1 keys "{ESC}".
public static class Job {
  public static string Run(string arg) {
    var sw = Sw.App(); var doc = (ModelDoc2)sw.ActiveDoc;
    if (doc == null) return "no active document";
    doc.ClearSelection2(true);
    return "Equations dialog closed: " + sw.RunCommand((int)SolidWorks.Interop.swcommands.swCommands_e.swCommands_Equations, "");
  }
}
