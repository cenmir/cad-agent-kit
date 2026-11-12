// Helpers for the gear examples: new part, equations that must succeed, selections with marks,
// sketch-to-model points, finding edges by position, and the measurements every check uses.
public static class Gear {
  public static ModelDoc2 NewPart(SldWorks sw) {
    string t = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
    var doc = (ModelDoc2)sw.NewDocument(t, 0, 0, 0);
    if (doc == null) throw new Exception("NewDocument failed, template '" + t + "'");
    doc.Extension.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem, 0, (int)swUnitSystem_e.swUnitSystem_MMGS);
    return doc;
  }
  // An equation that must be accepted: "lhs" = rhs.
  public static void Eq(ModelDoc2 doc, string lhs, string rhs) {
    if (doc.GetEquationMgr().Add2(-1, "\"" + lhs + "\" = " + rhs, true) < 0) throw new Exception("equation refused: \"" + lhs + "\" = " + rhs);
  }
  // The value of a global variable after the last rebuild.
  public static double Val(ModelDoc2 doc, string name) {
    var eq = doc.GetEquationMgr(); string key = "\"" + name + "\"=";
    for (int i = 0; i < eq.GetCount(); i++) if (eq.get_Equation(i).Replace(" ", "").StartsWith(key)) return eq.get_Value(i);
    throw new Exception("no global " + name);
  }
  // Dimension the selected entities, the text at the sketch point (u, v), and link the dimension to
  // an expression. Returns the dimension's name, "D3@Sketch1". The sketch Create* calls take sketch
  // coordinates, but AddDimension2 takes the text position in MODEL coordinates; for an angle that
  // position picks which of the angles between two lines is dimensioned (4.5 or 175.5 degrees).
  public static string Dim(SldWorks sw, ModelDoc2 doc, Feature sketch, double u, double v, string expr) {
    var p = ToModel(sw, (Sketch)sketch.GetSpecificFeature2(), u, v, 0);
    var dd = (DisplayDimension)doc.AddDimension2(p[0], p[1], p[2]);
    if (dd == null) throw new Exception("AddDimension2 failed for " + expr);
    string name = ((Dimension)dd.GetDimension2(0)).Name + "@" + sketch.Name;
    Eq(doc, name, expr); return name;
  }
  static SelectData Mark(ModelDoc2 doc, int mark) {
    var sd = (SelectData)((SelectionMgr)doc.SelectionManager).CreateSelectData(); sd.Mark = mark; return sd;
  }
  public static void Sel(ModelDoc2 doc, object o, bool append, int mark) {
    bool ok = o is Feature ? ((Feature)o).Select2(append, mark)
            : o is SketchSegment ? ((SketchSegment)o).Select4(append, Mark(doc, mark))
            : o is SketchPoint ? ((SketchPoint)o).Select4(append, Mark(doc, mark))
            : o is Body2 ? ((Body2)o).Select2(append, Mark(doc, mark))
            : ((Entity)o).Select4(append, Mark(doc, mark));
    if (!ok) throw new Exception("selection failed: " + o);
  }
  public static void SelOrigin(ModelDoc2 doc, bool append) {
    if (!doc.Extension.SelectByID2("Point1@Origin", "EXTSKETCHPOINT", 0, 0, 0, append, 0, null, 0)) throw new Exception("origin not selectable");
  }
  public static void Relate(ModelDoc2 doc, string relation, params object[] items) {
    doc.ClearSelection2(true);
    foreach (var o in items) { if (o == null) SelOrigin(doc, true); else Sel(doc, o, true, 0); }
    var sm = (SelectionMgr)doc.SelectionManager; int n = sm.GetSelectedObjectCount2(-1);
    var sk = (Sketch)doc.SketchManager.ActiveSketch; int before = Count(sk);
    doc.SketchAddConstraints(relation);
    if (Debug != null) Debug.AppendLine("  " + relation + " selected " + n + " types " + Types(sm, n) + " relations " + before + " -> " + Count(sk) + " status " + (swConstrainedStatus_e)sk.GetConstrainedStatus());
    doc.ClearSelection2(true);
  }
  public static StringBuilder Debug;
  static int Count(Sketch sk) { var r = (object[])sk.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll); return r == null ? 0 : r.Length; }
  static string Types(SelectionMgr sm, int n) { var l = new List<string>(); for (int i = 1; i <= n; i++) l.Add(((swSelectType_e)sm.GetSelectedObjectType3(i, -1)).ToString().Replace("swSel", "")); return string.Join(",", l); }
  public static SketchPoint Start(SketchSegment s) { return s is SketchLine ? (SketchPoint)((SketchLine)s).GetStartPoint2() : (SketchPoint)((SketchArc)s).GetStartPoint2(); }
  public static SketchPoint End(SketchSegment s) { return s is SketchLine ? (SketchPoint)((SketchLine)s).GetEndPoint2() : (SketchPoint)((SketchArc)s).GetEndPoint2(); }
  public static SketchPoint Centre(SketchSegment s) { return (SketchPoint)((SketchArc)s).GetCenterPoint2(); }
  // A point given in the coordinates of a sketch (u, v along the sketch, w along its normal), in model coordinates.
  public static double[] ToModel(SldWorks sw, Sketch sk, double u, double v, double w) {
    var mu = (MathUtility)sw.GetMathUtility();
    var inv = (MathTransform)((MathTransform)sk.ModelToSketchTransform).Inverse();
    return (double[])((MathPoint)((MathPoint)mu.CreatePoint(new double[] { u, v, w })).MultiplyTransform(inv)).ArrayData;
  }
  // A model point in the coordinates of a sketch (u, v, and w along its normal).
  public static double[] ToSketch(SldWorks sw, Sketch sk, double[] p) {
    var mu = (MathUtility)sw.GetMathUtility();
    return (double[])((MathPoint)((MathPoint)mu.CreatePoint(p)).MultiplyTransform((MathTransform)sk.ModelToSketchTransform)).ArrayData;
  }
  // The rotation matrix of a plane (rows: its x axis, y axis and normal, in model coordinates).
  public static double[] PlaneAxes(ModelDoc2 doc, string plane) {
    var rp = (RefPlane)((Feature)((PartDoc)doc).FeatureByName(plane)).GetSpecificFeature2();
    return (double[])((MathTransform)rp.Transform).ArrayData;
  }
  // The normal of a plane: in the templates we tested the Front Plane's normal is model x, not z.
  public static double[] Normal(ModelDoc2 doc, string plane) { var t = PlaneAxes(doc, plane); return new[] { t[6], t[7], t[8] }; }
  // Planar faces of a body whose normal is parallel to the axis: (position along the axis, face), sorted.
  public static List<Tuple<double, Face2>> FacesAcross(Body2 b, double[] ax) {
    var l = new List<Tuple<double, Face2>>();
    foreach (Face2 fc in (object[])b.GetFaces()) {
      var s = (Surface)fc.GetSurface(); if (!s.IsPlane()) continue;
      var pp = (double[])s.PlaneParams; double nd = pp[0] * ax[0] + pp[1] * ax[1] + pp[2] * ax[2];
      if (Math.Abs(Math.Abs(nd) - 1) < 1e-9) l.Add(Tuple.Create(pp[3] * ax[0] + pp[4] * ax[1] + pp[5] * ax[2], fc));
    }
    l.Sort((x, y) => x.Item1.CompareTo(y.Item1)); return l;
  }
  public static Body2[] Bodies(ModelDoc2 doc) {
    var o = (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true);
    var l = new List<Body2>(); if (o != null) foreach (Body2 b in o) l.Add(b); return l.ToArray();
  }
  public static void Suppress(ModelDoc2 doc, Feature f, bool on) {
    f.SetSuppression2(on ? (int)swFeatureSuppressionAction_e.swSuppressFeature : (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
      (int)swInConfigurationOpts_e.swThisConfiguration, null);
  }
  // Select the region of a sketch that contains the sketch point (u, v), as a contour (mark 4), for
  // an extrusion of Selected Contours. SelectByID2(..., "SKETCHREGION", x, y, z, ...) found nothing
  // when run from a job, so the regions are tested one by one: each boundary is sampled and the
  // point is tested with the even-odd rule, which also handles regions with holes.
  public static void SelRegion(SldWorks sw, ModelDoc2 doc, Feature sketch, double u, double v, bool append) {
    var sk = (Sketch)sketch.GetSpecificFeature2();
    var mu = (MathUtility)sw.GetMathUtility(); var toSketch = (MathTransform)sk.ModelToSketchTransform;
    var regions = (object[])sk.GetSketchRegions();
    if (regions == null) throw new Exception(sketch.Name + " has no closed regions");
    SketchRegion best = null; double bestBox = double.MaxValue;
    foreach (SketchRegion r in regions) {
      bool inside = false; double u0 = 1e9, u1 = -1e9, v0 = 1e9, v1 = -1e9;
      foreach (Edge e in (object[])r.GetEdges()) {
        var cp = (CurveParamData)e.GetCurveParams3();
        var t = (double[])((Curve)e.GetCurve()).GetTessPts(1e-7, 2e-5, cp.StartPoint, cp.EndPoint);   // start to end along the edge
        double[] prev = null;
        for (int i = 0; i + 2 < t.Length; i += 3) {
          var q = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new double[] { t[i], t[i + 1], t[i + 2] })).MultiplyTransform(toSketch)).ArrayData;
          u0 = Math.Min(u0, q[0]); u1 = Math.Max(u1, q[0]); v0 = Math.Min(v0, q[1]); v1 = Math.Max(v1, q[1]);
          if (prev != null && ((prev[1] > v) != (q[1] > v)) && u < prev[0] + (v - prev[1]) * (q[0] - prev[0]) / (q[1] - prev[1])) inside = !inside;
          prev = q;
        }
      }
      double box = (u1 - u0) * (v1 - v0);
      if (Debug != null) Debug.AppendLine(string.Format("  region: {0} edges, inside {1}, u {2:F3}..{3:F3} v {4:F3}..{5:F3} mm", ((object[])r.GetEdges()).Length, inside, u0 * 1e3, u1 * 1e3, v0 * 1e3, v1 * 1e3));
      if (inside && box < bestBox) { best = r; bestBox = box; }   // the smallest region that contains the point
    }
    if (best == null) throw new Exception("no region of " + sketch.Name + " contains " + (u * 1e3).ToString("F3") + ", " + (v * 1e3).ToString("F3") + " mm");
    var sd = (SelectData)((SelectionMgr)doc.SelectionManager).CreateSelectData(); sd.Mark = 4;
    if (!best.Select2(append, sd)) throw new Exception("region of " + sketch.Name + " found but not selectable");
  }
  // The edge of the solid bodies nearest to a model point, if it lies within tol.
  public static Edge EdgeNear(ModelDoc2 doc, double[] p, double tol) {
    Edge best = null; double bd = tol;
    foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true))
      foreach (Edge e in (object[])b.GetEdges()) {
        var c = (double[])e.GetClosestPointOn(p[0], p[1], p[2]);
        double d = Math.Sqrt((c[0] - p[0]) * (c[0] - p[0]) + (c[1] - p[1]) * (c[1] - p[1]) + (c[2] - p[2]) * (c[2] - p[2]));
        if (d < bd) { bd = d; best = e; }
      }
    if (best == null) throw new Exception(string.Format("no edge within {0} mm of ({1:F3}, {2:F3}, {3:F3}) mm", tol * 1e3, p[0] * 1e3, p[1] * 1e3, p[2] * 1e3));
    return best;
  }
  public static Feature Last(ModelDoc2 doc, string type) {
    Feature r = null;
    for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) if (f.GetTypeName2() == type) r = f;
    return r;
  }
  // The relations and dimensions of a sketch, to find what is missing when it is not fully defined.
  public static string Relations(Sketch sk) {
    var sb = new StringBuilder(); var rels = (object[])sk.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll);
    if (rels != null) foreach (SketchRelation r in rels) {
      var ents = (object[])r.GetEntities(); var names = new List<string>();
      if (ents != null) foreach (var o in ents)
        names.Add(o is SketchSegment ? ((swSketchSegments_e)((SketchSegment)o).GetType()).ToString().Replace("swSketch", "") + "#" + string.Join(".", (int[])((SketchSegment)o).GetID())
                : o is SketchPoint ? "point#" + string.Join(".", (int[])((SketchPoint)o).GetID()) : "external");
      sb.AppendLine("    " + ((swConstraintType_e)r.GetRelationType()).ToString().Replace("swConstraintType_", "") + ": " + string.Join(", ", names));
    }
    return sb.ToString();
  }
  // Throws with the relation list unless the active sketch is fully defined.
  public static void FullyDefined(ModelDoc2 doc, StringBuilder log) {
    var sk = (Sketch)doc.SketchManager.ActiveSketch; int st = sk.GetConstrainedStatus(); var f = (Feature)sk;
    log.AppendLine(f.Name + ": " + (swConstrainedStatus_e)st);
    if (st != (int)swConstrainedStatus_e.swFullyConstrained) throw new Exception(f.Name + " is not fully defined; its relations:\r\n" + Relations(sk));
  }
  public static double Volume(ModelDoc2 doc) {
    var o = (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true); double v = 0;
    if (o != null) foreach (Body2 b in o) v += ((double[])b.GetMassProperties(1.0))[3] * 1e9;
    return v;
  }
  public static int BodyCount(ModelDoc2 doc) {
    var o = (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true); return o == null ? 0 : o.Length;
  }
  public static void Save(ModelDoc2 doc, string file, StringBuilder log) {
    Directory.CreateDirectory(Path.GetDirectoryName(file));
    int e = 0, w = 0;
    bool ok = doc.Extension.SaveAs3(file, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref e, ref w);
    log.AppendLine("saved " + ok + " " + file + (ok ? "" : " error " + e));
  }
}
