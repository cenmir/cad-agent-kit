# FreeCAD

The assistant writes a Python script and runs it headless with `freecadcmd`, FreeCAD's console
program. No window is needed and nothing else needs installing. Tested with FreeCAD 1.1.4 on
Windows 11.

```
"C:\Program Files\FreeCAD 1.1\bin\freecadcmd.exe" plate_sweep.py
```

The `bin` folder also holds FreeCAD's own `python.exe`, and in the Windows installer `ccx.exe`
(CalculiX) and `gmsh.exe`. Write output files to absolute paths. `freecadcmd` prints recompute
progress bars full of tabs and carriage returns; filter the output for your own lines.

## The method in one script: `plate_sweep.py`

The same plate as SolidWorks' `Plate.cs`: parameters in a spreadsheet, two fully constrained
sketches whose dimensions are expressions of the spreadsheet aliases, a Pad and a through-all Pocket,
then a parameter sweep with every case checked against the expected volume and the solid count.
Output with FreeCAD 1.1.4:

```
PlateSketch: solve 0 (0 = ok), fully constrained True
HoleSketch: solve 0 (0 = ok), fully constrained True
L=60 W=40 T=5 D=6 | valid True solids 1 | V=11858.628 expected 11858.628 | errors none
L=60 W=40 T=5 D=30 | valid True solids 1 | V=8465.708 expected 8465.708 | errors none
L=60 W=40 T=5 D=45 | valid True solids 2 | V=4395.445 expected 4047.844 | errors none
L=120 W=20 T=5 D=10 | valid True solids 1 | V=11607.301 expected 11607.301 | errors none
L=60 W=40 T=5 D=10 | valid True solids 1 | V=11607.301 expected 11607.301 | errors none
```

As in SolidWorks, the 45 mm hole in the 40 mm plate gives no error and a *valid* shape, which is two
solids with the wrong volume. FreeCAD's volumes are exact to the printed digits.

## Things to know

- **Spreadsheet aliases must not be unit symbols.** `N`, `m`, `S`, `h`, `L`, `W`, `T` and `D` are all
  rejected ("Invalid alias"). Use descriptive names (`Length`, `HoleDia`).
- Inside expressions, trigonometric functions of plain numbers work in degrees.
- `sketch.FullyConstrained` tells whether a sketch is fully constrained after `doc.recompute()`.
- Sketch constraints take expressions: `sketch.setExpression("Constraints[3]", "Params.Length")`.
  Feature properties too: `pad.setExpression("Length", "Params.Thickness")`.
- An object's `State` contains `"Invalid"` or `"Error"` when its recompute failed; check every object
  after every recompute.
- There is no equation-driven curve in core FreeCAD. A curve from its equation can be built as a
  Sketcher B-spline whose poles are spreadsheet expressions (cubic Hermite segments), which keeps the
  sketch fully constrained. PartDesign also has `InvoluteGear`.
- A fillet cannot be the original of a pattern; use the pattern's `TransformMode = "Whole shape"`,
  and pattern a small sector, not a whole cylinder (fusing N full cylinders ran to 5 GB of memory).
  Set `body.Tip` before adding the next transform.
