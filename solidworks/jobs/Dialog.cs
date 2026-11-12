// Open the PropertyManager of one feature of the active document (Edit Feature), for a screenshot.
// Arg: the feature name as the tree shows it, e.g. "Sweep1". Leaves the dialog open: close it with
// ui.ps1 keys "{ESC}" (cancel changes nothing). An open PropertyManager blocks the next API command.
public static class Job {
  public static string Run(string feature) {
    var sw = Sw.App(); var doc = (ModelDoc2)sw.ActiveDoc;
    if (doc == null) return "no active document";
    doc.ClearSelection2(true);
    var f = (Feature)((PartDoc)doc).FeatureByName(feature);
    if (f == null) return "no feature " + feature + " in " + doc.GetTitle();
    f.Select2(false, 0);
    return "Edit Feature on " + feature + ": " + sw.RunCommand((int)SolidWorks.Interop.swcommands.swCommands_e.swCommands_EditFeature, "");
  }
}
