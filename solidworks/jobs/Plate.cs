// Build a parametric plate with a centre hole from scratch, then test it over a parameter sweep.
// Shows the whole method: globals, fully defined sketches with dimensions linked to globals,
// features, and a check of every case against the volume it must have.
// Arg: optional output file; default <work>\Plate.SLDPRT.
public static class Job {
  static StringBuilder log = new StringBuilder();
  static ModelDoc2 doc;
  // Dimension the selected entity and link the dimension to a global: "D1@Sketch1" = "Length".
  static void Dim(Feature sketch, double x, double y, string global) {
    var dd = (DisplayDimension)doc.AddDimension2(x, y, 0);
    if (dd == null) throw new Exception("AddDimension2 failed for " + global);
    string name = ((Dimension)dd.GetDimension2(0)).Name + "@" + sketch.Name;
    if (doc.GetEquationMgr().Add2(-1, "\"" + name + "\" = \"" + global + "\"", true) < 0) throw new Exception("equation " + name);
  }
  static Feature CloseSketch(string what) {
    var sk = (Sketch)doc.SketchManager.ActiveSketch;
    int st = sk.GetConstrainedStatus();   // 3 = swFullyConstrained
    var f = (Feature)sk; doc.SketchManager.InsertSketch(true);
    log.AppendLine(what + " " + f.Name + ": " + (swConstrainedStatus_e)st);
    if (st != (int)swConstrainedStatus_e.swFullyConstrained) throw new Exception(f.Name + " is not fully defined");
    return f;
  }
  public static string Run(string outFile) {
    var sw = Sw.App();
    if (outFile == "") outFile = Path.Combine(Sw.WorkDir, "Plate.SLDPRT");
    Directory.CreateDirectory(Path.GetDirectoryName(outFile));
    try {
      string template = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
      doc = (ModelDoc2)sw.NewDocument(template, 0, 0, 0);
      if (doc == null) throw new Exception("NewDocument failed; default part template: '" + template + "'");
      // Templates differ: make the units millimetres so that "Length" = 60 means 60 mm.
      doc.Extension.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem, 0, (int)swUnitSystem_e.swUnitSystem_MMGS);
      var ext = doc.Extension; var sm = doc.SketchManager; var fm = doc.FeatureManager;

      Sw.SetGlobal(doc, "Length", "60"); Sw.SetGlobal(doc, "Width", "40");
      Sw.SetGlobal(doc, "PlateThickness", "5"); Sw.SetGlobal(doc, "HoleDia", "10");

      // Plate sketch: a centre rectangle on the origin. In an empty part the only thing to snap to is the
      // origin, so inference gives exactly the relation we want (centre on origin). Not so in a busy part.
      ext.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0); sm.InsertSketch(true);
      var segs = (object[])sm.CreateCenterRectangle(0, 0, 0, 0.030, 0.020, 0);
      SketchSegment top = null, side = null;
      foreach (SketchSegment s in segs) {
        if (s.ConstructionGeometry) continue;
        var l = (SketchLine)s; var a = (SketchPoint)l.GetStartPoint2(); var b = (SketchPoint)l.GetEndPoint2();
        if (Math.Abs(a.Y - b.Y) < 1e-9 && a.Y > 0) top = s;
        if (Math.Abs(a.X - b.X) < 1e-9 && a.X > 0) side = s;
      }
      var plateSketch = (Feature)sm.ActiveSketch;
      doc.ClearSelection2(true); top.Select4(false, null); Dim(plateSketch, 0, 0.030, "Length");
      doc.ClearSelection2(true); side.Select4(false, null); Dim(plateSketch, 0.040, 0, "Width");
      CloseSketch("plate sketch");
      doc.ClearSelection2(true); plateSketch.Select2(false, 0);
      var pad = fm.FeatureExtrusion3(true, false, false, (int)swEndConditions_e.swEndCondBlind, 0, 0.005, 0,
        false, false, false, false, 0, 0, false, false, false, false, true, true, true, 0, 0, false);
      if (pad == null) throw new Exception("extrusion failed");
      doc.GetEquationMgr().Add2(-1, "\"D1@" + pad.Name + "\" = \"PlateThickness\"", true);

      // Hole sketch: a circle on the origin, diameter from HoleDia, cut through all in both directions.
      doc.ClearSelection2(true);
      ext.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0); sm.InsertSketch(true);
      var circ = (SketchSegment)sm.CreateCircleByRadius(0, 0, 0, 0.005);
      var holeSketch = (Feature)sm.ActiveSketch;
      doc.ClearSelection2(true); circ.Select4(false, null); Dim(holeSketch, 0.008, 0.008, "HoleDia");
      CloseSketch("hole sketch");
      doc.ClearSelection2(true); holeSketch.Select2(false, 0);
      // Through All in both directions. "Through All - Both" (swEndCondThroughAllBoth) in one direction
      // is refused through the API (FeatureCut4 returns null), although the dialog offers it.
      var cut = fm.FeatureCut4(false, false, false, (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondThroughAll,
        0, 0, false, false, false, false, 0, 0, false, false, false, false, false, true, true, true, true, false, 0, 0, false, false);
      if (cut == null) throw new Exception("cut failed");
      doc.ForceRebuild3(false);
      log.AppendLine("built: " + Sw.Failures(doc));
      var tree = Sw.Tree(doc); log.Append(tree.Substring(tree.IndexOf("  Origin")));

      // Sweep: every case against the volume it must have, and the number of solid bodies. SolidWorks'
      // mass properties are approximate on curved faces (about 1e-5 relative), hence a relative tolerance.
      string[] cases = { "60 40 5 6", "60 40 5 30", "60 40 5 45", "120 20 5 10", "60 40 5 10" };
      foreach (var cs in cases) {
        var v = cs.Split(' ');
        Sw.SetGlobal(doc, "Length", v[0]); Sw.SetGlobal(doc, "Width", v[1]);
        Sw.SetGlobal(doc, "PlateThickness", v[2]); Sw.SetGlobal(doc, "HoleDia", v[3]);
        doc.ForceRebuild3(false);
        double L = double.Parse(v[0]), W = double.Parse(v[1]), T = double.Parse(v[2]), D = double.Parse(v[3]);
        double expected = (L * W - Math.PI * D * D / 4) * T;
        var bodies = (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true);
        double vol = 0; if (bodies != null) foreach (Body2 b in bodies) vol += ((double[])b.GetMassProperties(1.0))[3] * 1e9;
        log.AppendLine(string.Format("L={0} W={1} T={2} D={3} | {4} | bodies {5} | V={6:F3} expected {7:F3}{8}",
          v[0], v[1], v[2], v[3], Sw.Failures(doc), bodies == null ? 0 : bodies.Length, vol, expected,
          Math.Abs(vol - expected) > 1e-4 * expected ? "  <-- MISMATCH" : ""));
      }
      int e = 0, w = 0;
      bool ok = ext.SaveAs3(outFile, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref e, ref w);
      log.AppendLine("saved " + ok + " " + outFile);
    } catch (Exception ex) { log.AppendLine("EXCEPTION " + ex.Message); }
    finally { Sw.Close(sw, doc); }
    return log.ToString();
  }
}
