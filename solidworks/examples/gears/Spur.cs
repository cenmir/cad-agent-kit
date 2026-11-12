// A parametric involute spur gear, built from an empty part.
//   Step 1  global variables (m, N, PressureAngle, FaceWidth and the radii that follow)
//   Step 2  Sketch1 on the Front Plane: root and tip circles, base and pitch circles (construction),
//           the flank as ONE equation-driven curve, Fixed; a datum line from the origin to the point
//           where the flank crosses the pitch circle; the tooth centreline at MidAngle from it
//   Step 3  Boss-Extrude1: the half tooth (one region of Sketch1); Plane1 through the centreline,
//           normal to the Front Plane; Boss-Extrude2: the root disc
//   Step 4  Fillet1 on the root edge of the flank, R_fillet
//   Step 5  Axis1 (Top Plane x Right Plane); CirPattern1 of the half tooth and its fillet, N instances
//   Step 6  Mirror1 of CirPattern1 about Plane1
// The flank x(t), y(t) is the involute for t > 0 and a radial line below the base circle for t < 0
// (max/min written as (v + sqr(v^2))/2), so the sketch keeps the same entities whether the root
// circle lies inside the base circle (N < 42) or outside it.
// Arg: optional output file, default <work>\Spur.SLDPRT. Reference: 1235.08 mm3 at N = 20, m = 1, b = 4.
public static class Job {
  static SldWorks sw; static ModelDoc2 doc; static StringBuilder log = new StringBuilder();

  public static string Run(string outFile) {
    sw = Sw.App(); if (outFile == "") outFile = Path.Combine(Sw.WorkDir, "Spur.SLDPRT");
    try { Build(); Check(); Gear.Save(doc, outFile, log); }
    catch (Exception ex) { log.AppendLine("EXCEPTION " + ex.Message); }
    finally { Sw.Close(sw, doc); }
    return log.ToString();
  }

  static void Build() {
    doc = Gear.NewPart(sw);
    var ext = doc.Extension; var sm = doc.SketchManager; var fm = doc.FeatureManager;

    // Step 1: globals, in the order of the chapter's table
    string[,] g = {
      { "m", "1" }, { "N", "20" }, { "PressureAngle", "20" }, { "FaceWidth", "4" },
      { "PitchRadius", "\"m\" * \"N\" / 2" },
      { "BR", "\"PitchRadius\" * cos ( \"PressureAngle\" )" },
      { "TipRadius", "\"m\" * ( \"N\" + 2 ) / 2" },
      { "RootRadius", "\"PitchRadius\" - 1.25 * \"m\"" },
      { "t_tip", "sqr ( ( \"TipRadius\" / \"BR\" ) ^ 2 - 1 )" },
      { "MidAngle", "360 / ( \"N\" * 4 )" },
      { "R_fillet", "0.9 * \"BR\" * ( pi / ( 2 * \"N\" ) - ( tan ( \"PressureAngle\" ) - \"PressureAngle\" * pi / 180 ) )" } };
    for (int i = 0; i < g.GetLength(0); i++) Sw.SetGlobal(doc, g[i, 0], g[i, 1]);
    doc.ForceRebuild3(false);
    // the same numbers in metres and radians, only to place entities near where they belong;
    // the relations and the equations then put them exactly there
    double rp = Gear.Val(doc, "PitchRadius") / 1e3, rb = Gear.Val(doc, "BR") / 1e3, ra = Gear.Val(doc, "TipRadius") / 1e3,
           rf = Gear.Val(doc, "RootRadius") / 1e3, mid = Gear.Val(doc, "MidAngle") * Math.PI / 180, b = Gear.Val(doc, "FaceWidth") / 1e3;

    // Step 2: Sketch1
    ext.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0); sm.InsertSketch(true);
    var sketch = (Feature)sm.ActiveSketch;
    sm.AddToDB = true;                                    // no inferred relations; we add each one
    var root  = (SketchSegment)sm.CreateCircleByRadius(0, 0, 0, rf);
    var basec = (SketchSegment)sm.CreateCircleByRadius(0, 0, 0, rb);
    var pitch = (SketchSegment)sm.CreateCircleByRadius(0, 0, 0, rp);
    var tip   = (SketchSegment)sm.CreateCircleByRadius(0, 0, 0, ra);
    basec.ConstructionGeometry = true; pitch.ConstructionGeometry = true;
    const string v = "(t * \"t_tip\")", vp = "((" + v + " + sqr(" + v + " ^ 2)) / 2)", vm = "((" + v + " - sqr(" + v + " ^ 2)) / 2)";
    string xt = "\"BR\" * (sin(" + vp + ") - " + vp + " * cos(" + vp + "))";
    string yt = "\"BR\" * (cos(" + vp + ") + " + vp + " * sin(" + vp + ")) + " + vm + " * \"BR\"";
    // the last two arguments lock the ends at t1 and t2; without them the curve is not fully defined
    var curve = (SketchSegment)sm.CreateEquationSpline2(xt, yt, "", "-1", "1", false, 0, 0, 0, true, true);
    if (curve == null) throw new Exception("equation-driven curve refused");
    double ap = Math.Sqrt(rp * rp / (rb * rb) - 1);                       // involute parameter at the pitch circle
    double px = rb * (Math.Sin(ap) - ap * Math.Cos(ap)), py = rb * (Math.Cos(ap) + ap * Math.Sin(ap));
    var datum = (SketchSegment)sm.CreateLine(0, 0, 0, px, py, 0); datum.ConstructionGeometry = true;
    double th = Math.Atan2(px, py) + mid;                                 // centreline, MidAngle past the datum line
    var centre = (SketchSegment)sm.CreateLine(0, 0, 0, ra * Math.Sin(th), ra * Math.Cos(th), 0);
    sm.AddToDB = false;
    // With AddToDB the circle centres and the two line starts, all created at (0, 0), are one sketch
    // point: one relation puts it on the origin.
    Gear.Relate(doc, "sgCOINCIDENT", Gear.Centre(root), null);
    Gear.Relate(doc, "sgFIXED", curve);
    Gear.Relate(doc, "sgCOINCIDENT", Gear.End(datum), pitch);
    Gear.Relate(doc, "sgCOINCIDENT", Gear.End(datum), curve);
    Gear.Relate(doc, "sgCOINCIDENT", Gear.End(centre), tip);
    // diameters (SolidWorks dimensions a circle as a diameter) and the angle, linked to the globals
    var circles = new[] { root, basec, pitch, tip }; var radii = new[] { "RootRadius", "BR", "PitchRadius", "TipRadius" };
    for (int i = 0; i < 4; i++) { doc.ClearSelection2(true); Gear.Sel(doc, circles[i], false, 0);
      double r = new[] { rf, rb, rp, ra }[i]; Gear.Dim(sw, doc, sketch, -1.3 * r, (0.4 + 0.2 * i) * r, "2 * \"" + radii[i] + "\""); }
    doc.ClearSelection2(true); Gear.Sel(doc, datum, false, 0); Gear.Sel(doc, centre, true, 0);
    double tb = Math.Atan2(px, py) + mid / 2;                             // the text between the two lines
    Gear.Dim(sw, doc, sketch, 0.6 * rf * Math.Sin(tb), 0.6 * rf * Math.Cos(tb), "\"MidAngle\"");
    Gear.FullyDefined(doc, log);
    sm.InsertSketch(true);

    // Step 3: the half tooth. A point inside it: halfway between flank and centreline at mid-depth.
    double rm = (Math.Max(rf, rb) + ra) / 2, am = Math.Sqrt(rm * rm / (rb * rb) - 1);
    double flankAng = Math.Atan2(rb * (Math.Sin(am) - am * Math.Cos(am)), rb * (Math.Cos(am) + am * Math.Sin(am)));
    double ta = (flankAng + th) / 2;
    doc.ClearSelection2(true); Gear.SelRegion(sw, doc, sketch, rm * Math.Sin(ta), rm * Math.Cos(ta), false);
    var boss1 = fm.FeatureExtrusion3(true, false, false, (int)swEndConditions_e.swEndCondBlind, 0, b, 0,
      false, false, false, false, 0, 0, false, false, false, false, true, true, true, 0, 0, false);
    if (boss1 == null) throw new Exception("Boss-Extrude1 failed");
    Gear.Eq(doc, "D1@" + boss1.Name, "\"FaceWidth\"");
    log.AppendLine(boss1.Name + " (half tooth) V=" + Gear.Volume(doc).ToString("F3"));

    // Plane1: the centreline of Sketch1 and the Front Plane, perpendicular
    doc.ClearSelection2(true); Gear.Sel(doc, centre, false, 0);
    ext.SelectByID2("Front Plane", "PLANE", 0, 0, 0, true, 1, null, 0);
    var plane1 = (Feature)fm.InsertRefPlane((int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Coincident, 0,
      (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Perpendicular, 0, 0, 0);
    if (plane1 == null) throw new Exception("Plane1 failed");

    // the root disc: the region of Sketch1 opposite the tooth
    doc.ClearSelection2(true); Gear.SelRegion(sw, doc, sketch, 0, -rf / 2, false);
    var boss2 = fm.FeatureExtrusion3(true, false, false, (int)swEndConditions_e.swEndCondBlind, 0, b, 0,
      false, false, false, false, 0, 0, false, false, false, false, true, true, true, 0, 0, false);
    if (boss2 == null) throw new Exception("Boss-Extrude2 failed");
    Gear.Eq(doc, "D1@" + boss2.Name, "\"FaceWidth\"");
    log.AppendLine(boss2.Name + " (root disc) V=" + Gear.Volume(doc).ToString("F3") + ", bodies " + Gear.BodyCount(doc));

    // Step 4: the root edge of the flank, at the root radius, halfway along the face width
    double ar = rf > rb ? Math.Sqrt(rf * rf / (rb * rb) - 1) : 0;
    double eu = rf > rb ? rb * (Math.Sin(ar) - ar * Math.Cos(ar)) : 0, ev = rf > rb ? rb * (Math.Cos(ar) + ar * Math.Sin(ar)) : rf;
    Edge edge = null;
    foreach (double w in new[] { b / 2, -b / 2 }) {          // the extrusion went one way or the other along the normal
      try { edge = Gear.EdgeNear(doc, Gear.ToModel(sw, (Sketch)sketch.GetSpecificFeature2(), eu, ev, w), 1e-6); break; } catch { }
    }
    if (edge == null) throw new Exception("root edge of the flank not found");
    doc.ClearSelection2(true); Gear.Sel(doc, edge, false, 0);
    var fillet = (Feature)fm.FeatureFillet3((int)(swFeatureFilletOptions_e.swFeatureFilletPropagate | swFeatureFilletOptions_e.swFeatureFilletUniformRadius),
      Gear.Val(doc, "R_fillet") / 1e3, 0, 0, (int)swFeatureFilletType_e.swFeatureFilletType_Simple, 0, 0, null, null, null, null, null, null, null);
    if (fillet == null) throw new Exception("Fillet1 failed");
    Gear.Eq(doc, "D1@" + fillet.Name, "\"R_fillet\"");

    // Step 5: Axis1 from two default planes, then the circular pattern
    doc.ClearSelection2(true);
    ext.SelectByID2("Top Plane", "PLANE", 0, 0, 0, false, 0, null, 0); ext.SelectByID2("Right Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
    if (!doc.InsertAxis2(true)) throw new Exception("Axis1 failed");
    var axis = Gear.Last(doc, "RefAxis");
    doc.ClearSelection2(true); Gear.Sel(doc, boss1, false, 4); Gear.Sel(doc, fillet, true, 4); Gear.Sel(doc, axis, true, 1);
    int n = (int)Math.Round(Gear.Val(doc, "N"));
    var pattern = fm.FeatureCircularPattern5(n, 2 * Math.PI, false, "NULL", false, true, false, false, false, false, 1, 0, "NULL", false);
    if (pattern == null) throw new Exception("CirPattern1 failed");
    Gear.Eq(doc, "D1@" + pattern.Name, "\"N\"");

    // Step 6: mirror the pattern about Plane1
    doc.ClearSelection2(true); Gear.Sel(doc, pattern, false, 1); Gear.Sel(doc, plane1, true, 2);
    var mirror = fm.InsertMirrorFeature2(false, false, false, false, 0);
    if (mirror == null) throw new Exception("Mirror1 failed");
    doc.ForceRebuild3(false);
    log.AppendLine("built: " + Sw.Failures(doc) + ", bodies " + Gear.BodyCount(doc) + ", V=" + Gear.Volume(doc).ToString("F3") + " (chapter: 1235.08)");
    var tree = Sw.Tree(doc); log.Append(tree.Substring(tree.IndexOf("  Origin")));
  }

  // Rebuild over tooth counts on both sides of N = 42 (root circle inside / outside the base circle),
  // other modules and pressure angles, then back to the example.
  static void Check() {
    string[] cases = { "1 8 20 4", "1 12 20 4", "1 41 20 4", "1 42 20 4", "1 50 20 4", "1 100 20 4", "2 20 20 4", "1 20 25 4", "1 20 20 4" };
    foreach (var cs in cases) {
      var c = cs.Split(' ');
      string applied = Sw.SetGlobals(doc, new[] { "m", "N", "PressureAngle", "FaceWidth" }, c);
      log.AppendLine(string.Format("m={0} N={1} phi={2} b={3} | {4}{5} | bodies {6} | V={7:F3}", c[0], c[1], c[2], c[3], applied != "" ? applied + " | " : "", Sw.Failures(doc), Gear.BodyCount(doc), Gear.Volume(doc)));
    }
  }
}
