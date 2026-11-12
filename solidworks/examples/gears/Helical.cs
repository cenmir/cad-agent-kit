// A helical or herringbone gear, built on a copy of the spur part (Spur.cs or SpurDefinitive.SLDPRT):
//   Step 1  globals HelixAngle, SweepLength (FaceWidth, or FaceWidth/2 for a herringbone),
//           Lead = pi m N / tan(HelixAngle), Twist = SweepLength / Lead * 360 (a check only)
//   Step 2  the profile: Convert Entities of the spur face that lies in the Front Plane
//   Step 3  the path: a line on the Right Plane from the origin along the axis, length SweepLength
//   Step 4  a circle of diameter RootRadius on the Front Plane and a helix on it, height and pitch
//           linked to SweepLength and Lead through equations (the Helix dialog takes numbers only)
//   Step 5  sweep, the helix as guide curve, Profile Twist "Follow Path and 1st Guide Curve";
//           Delete/Keep Body keeps the sweep
//   Step 6  herringbone only: mirror the body about the Front Plane, merged
// The twist is NOT typed into the sweep: "Specify Twist Value" is a feature setting that no
// equation reaches. The helix carries it, and the guide need not touch the profile.
// Then a parameter sweep that compares every end face of the gear with the spur outline turned
// through theta(z) = 360 z / Lead (micrometres), and the volume with the spur's.
// Arg: "spurPart|helical|out" or "spurPart|herringbone|out" (out optional).
public static class Job {
  static SldWorks sw; static ModelDoc2 doc; static StringBuilder log = new StringBuilder(); static string saved;

  public static string Run(string arg) {
    var a = arg.Split('|'); bool hb = a.Length > 1 && a[1].Trim() == "herringbone";
    string outFile = a.Length > 2 && a[2].Trim() != "" ? a[2].Trim() : Path.Combine(Sw.WorkDir, hb ? "Herringbone.SLDPRT" : "Helical.SLDPRT");
    sw = Sw.App();
    try {
      doc = Sw.OpenCopy(sw, a[0].Trim()); Build(hb); Gear.Save(doc, outFile, log); saved = outFile;
      // Check the saved file, reopened. Checked in the same session right after the build, the
      // parameter changes of some cases were not applied (N had no effect, later cases lagged).
      Sw.Close(sw, doc); doc = Sw.Open(sw, outFile); doc.ForceRebuild3(false);
      Check(hb); Gear.Save(doc, outFile, log);
    }
    catch (Exception ex) { log.AppendLine("EXCEPTION " + ex.Message); }
    finally { Sw.Close(sw, doc); }
    return log.ToString();
  }

  static void Build(bool hb) {
    var ext = doc.Extension; var sm = doc.SketchManager; var fm = doc.FeatureManager;
    // Step 1
    Sw.SetGlobal(doc, "m", "1"); Sw.SetGlobal(doc, "N", "20"); Sw.SetGlobal(doc, "PressureAngle", "20");
    Sw.SetGlobal(doc, "FaceWidth", "10"); Sw.SetGlobal(doc, "HelixAngle", "30");
    Sw.SetGlobal(doc, "SweepLength", hb ? "\"FaceWidth\" / 2" : "\"FaceWidth\"");
    Sw.SetGlobal(doc, "Lead", "pi * \"m\" * \"N\" / tan ( \"HelixAngle\" )");
    Sw.SetGlobal(doc, "Twist", "\"SweepLength\" / \"Lead\" * 360");
    var ch = (Feature)((PartDoc)doc).FeatureByName("Chamfer1");       // the downloaded part has one; it shrinks the face
    if (ch != null) Gear.Suppress(doc, ch, true);
    doc.ForceRebuild3(false);
    log.AppendLine("spur: " + Sw.Failures(doc) + ", V=" + Gear.Volume(doc).ToString("F3"));
    var ax = Gear.Normal(doc, "Front Plane");                          // the gear axis

    // Step 2: the profile, the spur face in the Front Plane
    Face2 front = null;
    foreach (var t in Gear.FacesAcross(Gear.Bodies(doc)[0], ax)) if (Math.Abs(t.Item1) < 1e-9) front = t.Item2;
    if (front == null) throw new Exception("no face of the spur part in the Front Plane");
    ext.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0); sm.InsertSketch(true);
    doc.ClearSelection2(true); ((Entity)front).Select4(false, null); sm.SketchUseEdge3(false, false);
    var profile = (Feature)sm.ActiveSketch; Gear.FullyDefined(doc, log); sm.InsertSketch(true);

    // Step 3: the path, from the origin along the axis into the spur part
    var across = Gear.FacesAcross(Gear.Bodies(doc)[0], ax);
    double zEnd = Math.Abs(across[across.Count - 1].Item1) > Math.Abs(across[0].Item1) ? across[across.Count - 1].Item1 : across[0].Item1;
    ext.SelectByID2("Right Plane", "PLANE", 0, 0, 0, false, 0, null, 0); sm.InsertSketch(true);
    var pathSk = (Sketch)sm.ActiveSketch; var path = (Feature)pathSk;
    var e = Gear.ToSketch(sw, pathSk, new[] { zEnd * ax[0], zEnd * ax[1], zEnd * ax[2] });
    sm.AddToDB = true;                                   // the end must not snap to an edge of the spur part
    var line = (SketchSegment)sm.CreateLine(0, 0, 0, e[0], e[1], 0);
    sm.AddToDB = false;
    Gear.Relate(doc, "sgCOINCIDENT", Gear.Start(line), null);
    Gear.Relate(doc, Math.Abs(e[0]) > Math.Abs(e[1]) ? "sgHORIZONTAL2D" : "sgVERTICAL2D", line);
    doc.ClearSelection2(true); Gear.Sel(doc, line, false, 0);
    Gear.Dim(sw, doc, path, e[0] / 2 + 0.002, e[1] / 2 + 0.002, "\"SweepLength\"");
    Gear.FullyDefined(doc, log); sm.InsertSketch(true);

    // Step 4: the circle (diameter RootRadius, so inside the root circle at every N) and the helix
    ext.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0); sm.InsertSketch(true);
    var circleSk = (Feature)sm.ActiveSketch;
    sm.AddToDB = true; var circ = (SketchSegment)sm.CreateCircleByRadius(0, 0, 0, Gear.Val(doc, "RootRadius") / 2e3); sm.AddToDB = false;
    Gear.Relate(doc, "sgCOINCIDENT", Gear.Centre(circ), null);
    doc.ClearSelection2(true); Gear.Sel(doc, circ, false, 0);
    Gear.Dim(sw, doc, circleSk, 0.006, 0.006, "\"RootRadius\"");
    Gear.FullyDefined(doc, log); sm.InsertSketch(true);
    doc.ClearSelection2(true); Gear.Sel(doc, circleSk, false, 0);
    doc.InsertHelix(false, false, false, false, (int)swHelixDefinedBy_e.swHelixDefinedByHeightAndPitch,
      Gear.Val(doc, "SweepLength") / 1e3, Gear.Val(doc, "Lead") / 1e3, 0, 0, 0);   // counterclockwise: right hand
    var helix = Gear.Last(doc, "Helix"); if (helix == null) throw new Exception("helix failed");
    Gear.Eq(doc, "D3@" + helix.Name, "\"SweepLength\""); Gear.Eq(doc, "D4@" + helix.Name, "\"Lead\"");
    doc.ForceRebuild3(false); log.AppendLine(helix.Name + ": " + Sw.Failures(doc));

    // Step 5: the sweep, then keep only its body
    doc.ClearSelection2(true); Gear.Sel(doc, profile, false, 1); Gear.Sel(doc, path, true, 4); Gear.Sel(doc, helix, true, 2);
    var sweep = fm.InsertProtrusionSwept4(false, false, (int)swTwistControlType_e.swTwistControlFollowPathFirstGuideCurve,
      false, false, 0, 0, false, 0, 0, 0, 0, false, true, true, 0, true, false, 0, 0);
    if (sweep == null) throw new Exception("sweep failed");
    doc.ClearSelection2(true);
    foreach (var b in Gear.Bodies(doc)) if (!b.Name.Contains(sweep.Name)) Gear.Sel(doc, b, true, 0);
    if (fm.InsertDeleteBody2(false) == null) throw new Exception("Delete/Keep Body failed");

    // Step 6: the herringbone, the body mirrored about the Front Plane and merged
    if (hb) {
      doc.ClearSelection2(true);
      ext.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 2, null, 0); Gear.Sel(doc, Gear.Bodies(doc)[0], true, 256);
      if (fm.InsertMirrorFeature2(true, false, true, false, 0) == null) throw new Exception("mirror failed");
    }
    doc.ForceRebuild3(false);
    log.AppendLine("built: " + Sw.Failures(doc) + ", bodies " + Gear.BodyCount(doc) + ", V=" + Gear.Volume(doc).ToString("F3"));
    var tree = Sw.Tree(doc); log.Append(tree.Substring(tree.IndexOf("  Sketch1 ")));
  }

  // ---- the check ----
  static Feature helixF, deleteF;
  // Edge points of a face, one polyline per edge (null between edges), chords of 1 um, at most 20 um apart.
  static List<double[]> Sample(Face2 f) {
    var pts = new List<double[]>();
    foreach (Edge e in (object[])f.GetEdges()) {
      var c = (Curve)e.GetCurve(); var cp = (CurveParamData)e.GetCurveParams3();
      var t = (double[])c.GetTessPts(1e-6, 2e-5, cp.StartPoint, cp.EndPoint);
      for (int i = 0; i + 2 < t.Length; i += 3) pts.Add(new[] { t[i], t[i + 1], t[i + 2] });
      pts.Add(null);
    }
    return pts;
  }
  static double[] T;   // the Front Plane's axes: in-plane coordinates of a model point
  static double[] UV(double[] p) { return new[] { p[0] * T[0] + p[1] * T[1] + p[2] * T[2], p[0] * T[3] + p[1] * T[4] + p[2] * T[5] }; }
  // the largest distance (um) from the points of outline b to the polylines of outline a turned through deg
  static double Dev(List<double[]> a, List<double[]> b, double deg) {
    double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180), worst = 0;
    var A = new List<double[]>();
    foreach (var p in a) { if (p == null) { A.Add(null); continue; } var q = UV(p); A.Add(new[] { c * q[0] - s * q[1], s * q[0] + c * q[1] }); }
    int k = 0;
    foreach (var p in b) {
      if (p == null || k++ % 4 != 0) continue;    // every fourth point against the whole reference outline
      var q = UV(p); double best = 1e18;
      for (int i = 0; i + 1 < A.Count; i++) {
        var r0 = A[i]; var r1 = A[i + 1]; if (r0 == null || r1 == null) continue;
        double dx = r1[0] - r0[0], dy = r1[1] - r0[1], L = dx * dx + dy * dy;
        double t = L > 0 ? Math.Max(0, Math.Min(1, ((q[0] - r0[0]) * dx + (q[1] - r0[1]) * dy) / L)) : 0;
        double ex = r0[0] + t * dx - q[0], ey = r0[1] + t * dy - q[1]; best = Math.Min(best, ex * ex + ey * ey);
      }
      worst = Math.Max(worst, Math.Sqrt(best));
    }
    return worst * 1e6;
  }
  static string Measure(bool hb, double hand) {
    Body2 spur = null, gear = null;
    foreach (var b in Gear.Bodies(doc)) { if (b.Name == "Mirror1" || b.Name.StartsWith("Mirror1[")) spur = b; else gear = b; }
    if (spur == null || gear == null) return "bodies? " + Sw.Bodies(doc);
    var ax = Gear.Normal(doc, "Front Plane"); T = Gear.PlaneAxes(doc, "Front Plane");
    double vs = ((double[])spur.GetMassProperties(1.0))[3] * 1e9, vg = ((double[])gear.GetMassProperties(1.0))[3] * 1e9;
    var spurEnds = Gear.FacesAcross(spur, ax); var refPts = Sample(spurEnds[0].Item2);
    double lead = Gear.Val(doc, "Lead") / 1e3, worst = 0; var ends = new List<string>();
    foreach (var t in Gear.FacesAcross(gear, ax)) {
      double z = t.Item1 - spurEnds[0].Item1, th = hand * (hb ? Math.Abs(z) : z) / lead * 360;
      double dv = Dev(refPts, Sample(t.Item2), th); worst = Math.Max(worst, dv);
      ends.Add(string.Format("z={0:F2} theta={1:F3} dev={2:F2}um", z * 1e3, th, dv));
    }
    return string.Format("dV={0:F3}% | {1} | worst {2:F2} um{3}", (vg - vs) / vs * 100, string.Join(", ", ends), worst, worst > 5 ? "  <-- CHECK" : "");
  }
  static void Hand(bool left) {
    var d = (HelixFeatureData)helixF.GetDefinition(); d.Clockwise = left; helixF.ModifyDefinition(d, doc, null);
  }
  static void Check(bool hb) {
    helixF = Gear.Last(doc, "Helix"); deleteF = Gear.Last(doc, "DeleteBody");
    string[] cases = { "1 20 20 10 30 R", "1 8 20 10 30 R", "1 12 20 10 30 R", "1 14 20 10 30 R", "1 50 20 10 30 R", "1 90 20 10 30 R", "1 100 20 10 30 R",
                       "2 20 20 10 30 R", "0.5 30 20 6 30 R", "1 20 25 10 30 R", "1 20 20 10 15 R", "1 20 20 10 45 R",
                       "1 20 20 30 30 R", "1 20 20 10 30 L", "1 20 20 10 30 R" };
    log.AppendLine("m N phi b beta hand | rebuild | volume against the spur, end faces against the turned spur outline");
    foreach (var cs in cases) {
      var c = cs.Split(' ');
      Hand(c[5] == "L");
      string applied = Sw.SetGlobals(doc, new[] { "m", "N", "PressureAngle", "FaceWidth", "HelixAngle" }, new[] { c[0], c[1], c[2], c[3], c[4] });
      string f = (applied != "" ? applied + " | " : "") + Sw.Failures(doc);
      Gear.Suppress(doc, deleteF, true); doc.ForceRebuild3(false);       // bring the spur body back as the reference
      string m = f == "ok" ? Measure(hb, c[5] == "L" ? -1 : 1) : "not measured";
      Gear.Suppress(doc, deleteF, false); doc.ForceRebuild3(false);
      log.AppendLine(cs + " | " + f + " | " + m);
      // After a failed rebuild SolidWorks may ignore the next changes (they came back NOT APPLIED):
      // reopen the saved part before the next case.
      if (f != "ok") { Sw.Close(sw, doc); doc = Sw.Open(sw, saved); helixF = Gear.Last(doc, "Helix"); deleteF = Gear.Last(doc, "DeleteBody"); }
    }
  }
}
