# Lessons from building parts with an AI assistant

These come from spur, helical, herringbone, straight and spiral bevel gears in SolidWorks 2026, a
spur gear in FreeCAD 1.1 and SolidCraft 0.29, a crush-rib test plate for 3D printing in SolidCraft,
and the example plate of this kit. They apply to every program; program-specific traps are in each
program's README.

## Design intent

- **Drive geometry from equations.** Global variables, equation-driven curves, dimensions linked to
  globals, one feature and then a pattern. Never fit a spline through points computed elsewhere: it
  looks right and cannot follow a parameter. Sampled geometry is acceptable for a picture, never for
  engineering CAD.
- **Keep the topology fixed over the parameter range.** On a spur gear the root circle and the base
  circle swap order near N = 42 (pressure angle 20°). One equation-driven curve whose branches are
  written without conditions (`max(v, 0) = (v + sqr(v^2))/2` in SolidWorks) avoids a different set of
  sketch entities on each side.
- **Reference datums, not faces.** Planes, axes, the origin and construction geometry survive a
  parameter change; face and edge references are renumbered ("could not find face or plane"). When a
  face reference cannot be avoided, it is the first thing to check when a rebuild fails.
- **Put datums where the theory puts them.** A gear tooth's mirror line goes through the pitch point.
  Anchored on the base circle instead, the tooth came out 19 % thin and nothing failed to warn about it.
- **Fillet once, then pattern.** Fillet a single tooth before the circular pattern: one blended edge
  instead of 2N.
- **Sweeps, not lofts, for helices.** A loft joins sections with straight chords between corresponding
  points and cuts across a helix. Sweep along a path with a helix as guide curve.

## The default case proves little

- A chamfer on a spur gear failed at exactly one combination, N = 20 and m = 1: the textbook example.
- A sketch fillet on a spiral bevel gear worked at 16 teeth and turned inside out at 32.
- A helix on the pitch circle made a helical sweep fail for N = 8 to 14; inside the root circle it never
  failed.
- A hole wider than its plate rebuilt without an error in SolidWorks and in FreeCAD, as two bodies.

Only a sweep over the whole parameter span finds such things, and it must include the default values.

## Check what was built, not that it built

- Compare with an independent value: the volume from a formula, the number of bodies, exact extents.
- A twisted part must keep the volume of the part it was twisted from, and each end face must be the
  start face turned through the expected angle. Sample the end-face edges and measure the
  point-to-polyline distance to the turned reference (point-to-point overstates the error). The
  helical gears matched the theory to about 1 µm.
- Cross-section the solid where its function is: the crush ribs were sliced at mid-height and near the
  top, and the tip diameters compared with the intended interference.
- Mating parts: assemble them and run interference detection.
- Check the hand and the direction of every helical or swept feature with a measured case of each hand,
  not by eye.

## Exact stays exact

- Moving solids with a general transform can turn every face into a B-spline (`transform_geometry` in
  build123d/OpenCascade); SolidWorks then finds no cylinder for a concentric mate. Use rigid transforms.
- Profiles must close exactly: a 0.26 mm gap between a fillet and a flank broke a bevel-gear wire.
- Know what a library gives you: SolidWorks Toolbox gears look right, but their flanks are circular
  arcs, not involutes.

## Physical parameter sweeps

When the right value depends on the printer or the material (press fits, crush ribs, clearances),
build a test strip: one part with a column per parameter value, every column driven by its own
parameter and engraved with its value. The crush-rib plate had five columns of holes from 0.2 mm too
tight to 0.2 mm too loose. Print once, try the bearing in each, read off the answer.

## Working with the assistant

- Ask it to say, before building, what each feature must produce (volume, extents), and to report the
  measured value next to it afterwards.
- It reports confidently either way. Believe numbers and pictures, not adjectives.
- Instructions written for people must be checked in the real dialogs. The SolidWorks Helix/Spiral
  dialog refuses `="SweepLength"`, although the same dimension can be linked in Tools > Equations; a
  tutorial checked only through the API would have told readers to type it there.
