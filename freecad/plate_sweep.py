# Parametric plate with a hole, driven by a spreadsheet, rebuilt over a parameter sweep.
# Run headless:  "C:\Program Files\FreeCAD 1.1\bin\freecadcmd.exe" plate_sweep.py
# Writes plate.FCStd and prints one line per case: parameters, validity, volume, expected volume.
import os
import FreeCAD as App
import Part, Sketcher

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "work")
os.makedirs(out, exist_ok=True)
doc = App.newDocument("plate")

# Parameters live in a spreadsheet. Aliases must not be unit symbols: N, m, S, h, L, W, T, D are all rejected
# ("Invalid alias"), so use descriptive names.
ss = doc.addObject("Spreadsheet::Sheet", "Params")
for row, (alias, value) in enumerate([("Length", 60), ("Width", 40), ("Thickness", 5), ("HoleDia", 10)], start=1):
    ss.set(f"A{row}", alias); ss.set(f"B{row}", str(value)); ss.setAlias(f"B{row}", alias)
doc.recompute()

body = doc.addObject("PartDesign::Body", "Body")
xy = body.Origin.OriginFeatures[3]   # XY_Plane

# Rectangle centred on the origin, fully constrained, sizes from the spreadsheet.
sk = body.newObject("Sketcher::SketchObject", "PlateSketch"); sk.AttachmentSupport = [(xy, "")]; sk.MapMode = "FlatFace"
p = [App.Vector(-30, -20, 0), App.Vector(30, -20, 0), App.Vector(30, 20, 0), App.Vector(-30, 20, 0)]
for i in range(4):
    sk.addGeometry(Part.LineSegment(p[i], p[(i + 1) % 4]))
c = [Sketcher.Constraint("Coincident", i, 2, (i + 1) % 4, 1) for i in range(4)]
c += [Sketcher.Constraint("Horizontal", 0), Sketcher.Constraint("Horizontal", 2),
      Sketcher.Constraint("Vertical", 1), Sketcher.Constraint("Vertical", 3),
      Sketcher.Constraint("Symmetric", 0, 1, 1, 2, -1, 1)]   # origin at the centre
for k in c:
    sk.addConstraint(k)
iL = sk.addConstraint(Sketcher.Constraint("DistanceX", 0, 1, 0, 2, 60))
iW = sk.addConstraint(Sketcher.Constraint("DistanceY", 1, 1, 1, 2, 40))
sk.setExpression(f"Constraints[{iL}]", "Params.Length"); sk.setExpression(f"Constraints[{iW}]", "Params.Width")
pad = body.newObject("PartDesign::Pad", "Plate"); pad.Profile = sk; pad.setExpression("Length", "Params.Thickness")

# Hole at the centre, diameter from the spreadsheet, through all.
hs = body.newObject("Sketcher::SketchObject", "HoleSketch"); hs.AttachmentSupport = [(xy, "")]; hs.MapMode = "FlatFace"
hs.addGeometry(Part.Circle(App.Vector(0, 0, 0), App.Vector(0, 0, 1), 5))
hs.addConstraint(Sketcher.Constraint("Coincident", 0, 3, -1, 1))
iD = hs.addConstraint(Sketcher.Constraint("Diameter", 0, 10)); hs.setExpression(f"Constraints[{iD}]", "Params.HoleDia")
pocket = body.newObject("PartDesign::Pocket", "Hole"); pocket.Profile = hs; pocket.Type = "ThroughAll"; pocket.Reversed = True
doc.recompute()

for s in (sk, hs):   # every sketch fully constrained, or it moves when a parameter changes
    print(f"{s.Name}: solve {s.solve()} (0 = ok), fully constrained {s.FullyConstrained}")

def case(**kw):
    for k, v in kw.items():
        ss.set(k, str(v))
    doc.recompute()
    L, W, T, D = (float(ss.get(a)) for a in ("Length", "Width", "Thickness", "HoleDia"))
    shape = body.Shape
    expected = (L * W - 3.141592653589793 * D**2 / 4) * T
    bad = [o.Name for o in doc.Objects if "Invalid" in o.State or "Error" in o.State]
    print(f"L={L:g} W={W:g} T={T:g} D={D:g} | valid {shape.isValid()} solids {len(shape.Solids)} "
          f"| V={shape.Volume:.3f} expected {expected:.3f} | errors {bad or 'none'}")

for D in (6, 10, 30, 45):          # 45 is wider than the plate: the model should report it, not hide it
    case(HoleDia=D)
case(Length=120, Width=20, HoleDia=10)
case(Length=60, Width=40, Thickness=5, HoleDia=10)        # back to the defaults before saving
doc.saveAs(os.path.join(out, "plate.FCStd"))
print("saved", os.path.join(out, "plate.FCStd"))
