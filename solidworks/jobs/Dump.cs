// Dump a copy of a part as text, to learn how it was built: for each feature its type and parents,
// for each sketch its entities (sketch coordinates, mm), relations and dimensions, and the equations
// of equation-driven curves. Arg: "file" or "file|Sketch1,Sketch2" to dump only those sketches.
public static class Job {
  static string P(double[] p) { return string.Format("({0:F4}, {1:F4})", p[0] * 1e3, p[1] * 1e3); }
  static string Pt(SketchPoint p) { return string.Format("({0:F4}, {1:F4}, {2:F4})", p.X * 1e3, p.Y * 1e3, p.Z * 1e3); }
  public static string Run(string arg) {
    var a = arg.Split('|'); var only = a.Length > 1 ? new List<string>(a[1].Split(',')) : null;
    var sw = Sw.App(); ModelDoc2 doc = null; var sb = new StringBuilder();
    try {
      doc = Sw.OpenCopy(sw, a[0]); doc.ForceRebuild3(false);
      for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) {
        string t = f.GetTypeName2();
        if (t.EndsWith("Folder") || t == "DetailCabinet" || t == "MaterialFolder") continue;
        var par = new List<string>(); var ps = (object[])f.GetParents();
        if (ps != null) foreach (Feature q in ps) par.Add(q.Name);
        var kids = new List<string>(); var sub = (Feature)f.GetFirstSubFeature();
        while (sub != null) { kids.Add(sub.Name); sub = (Feature)sub.GetNextSubFeature(); }
        sb.AppendLine(f.Name + " [" + t + "] parents: " + string.Join(", ", par) + (kids.Count > 0 ? " | sub: " + string.Join(", ", kids) : ""));
        var dd = (DisplayDimension)f.GetFirstDisplayDimension();
        while (dd != null) { var dm = (Dimension)dd.GetDimension2(0);
          sb.AppendLine("    dim " + dm.Name + " = " + dm.SystemValue
            + " (" + (dm.DrivenState == (int)swDimensionDrivenState_e.swDimensionDriven ? "driven" : "driving") + ")");
          dd = (DisplayDimension)f.GetNextDisplayDimension(dd); }
        var sk = f.GetSpecificFeature2() as Sketch;
        if (sk == null || (only != null && !only.Contains(f.Name))) continue;
        sb.AppendLine("    3D: " + sk.Is3D() + ", status " + (swConstrainedStatus_e)sk.GetConstrainedStatus());
        var segs = (object[])sk.GetSketchSegments();
        if (segs != null) foreach (SketchSegment s in segs) {
          string id = "#" + string.Join(".", (int[])s.GetID());
          string c = s.ConstructionGeometry ? " construction" : "";
          switch ((swSketchSegments_e)s.GetType()) {
            case swSketchSegments_e.swSketchLINE: { var l = (SketchLine)s;
              sb.AppendLine("    line " + id + c + " " + Pt((SketchPoint)l.GetStartPoint2()) + " -> " + Pt((SketchPoint)l.GetEndPoint2())); break; }
            case swSketchSegments_e.swSketchARC: { var r = (SketchArc)s;
              sb.AppendLine("    arc " + id + c + " centre " + Pt((SketchPoint)r.GetCenterPoint2()) + " R " + (r.GetRadius() * 1e3).ToString("F4")
                + (r.IsCircle() == 1 ? " full circle" : " from " + Pt((SketchPoint)r.GetStartPoint2()) + " to " + Pt((SketchPoint)r.GetEndPoint2()))); break; }
            case swSketchSegments_e.swSketchSPLINE: { var sp = (SketchSpline)s;
              string eq = "";
              try { string x, y, z; double t0, t1, rot, ox, oy; bool angRange, lock0, lock1;
                if (sp.GetEquationParameters2(out x, out y, out z, out t0, out t1, out angRange, out rot, out ox, out oy, out lock0, out lock1))
                  eq = " EQUATION t " + t0 + ".." + t1 + " locks " + lock0 + "," + lock1 + (rot != 0 || ox != 0 || oy != 0 ? " rot " + rot + " offset " + ox + "," + oy : "")
                     + "\r\n      x: " + x + "\r\n      y: " + y + (string.IsNullOrEmpty(z) ? "" : "\r\n      z: " + z); }
              catch { }
              var pts = (object[])sp.GetPoints2();
              sb.AppendLine("    spline " + id + c + " " + (pts == null ? 0 : pts.Length) + " points" + eq); break; }
            default: sb.AppendLine("    " + (swSketchSegments_e)s.GetType() + " " + id + c); break;
          }
        }
        var pts2 = (object[])sk.GetSketchPoints2();
        if (pts2 != null) foreach (SketchPoint p in pts2) if (p.Type == (int)swSketchPointType_e.swSketchPointType_User)
          sb.AppendLine("    point #" + string.Join(".", (int[])p.GetID()) + " " + Pt(p));
        var rels = (object[])sk.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll);
        if (rels != null) foreach (SketchRelation r in rels) {
          var ents = (object[])r.GetEntities(); var types = (int[])r.GetEntitiesType(); var names = new List<string>();
          if (ents != null) for (int i = 0; i < ents.Length; i++) {
            var o = ents[i];
            if (o is SketchSegment) names.Add(((swSketchSegments_e)((SketchSegment)o).GetType()).ToString().Replace("swSketch", "").ToLower() + "#" + string.Join(".", (int[])((SketchSegment)o).GetID()));
            else if (o is SketchPoint) names.Add("point#" + string.Join(".", (int[])((SketchPoint)o).GetID()));
            else names.Add(((swSketchRelationEntityTypes_e)types[i]).ToString().Replace("swSketchRelationEntityType_", ""));
          }
          sb.AppendLine("    rel " + ((swConstraintType_e)r.GetRelationType()).ToString().Replace("swConstraintType_", "") + ": " + string.Join(", ", names));
        }
      }
    } catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex); }
    finally { Sw.Close(sw, doc); }
    return sb.ToString();
  }
}
