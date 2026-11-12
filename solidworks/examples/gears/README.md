# Gear examples

Complete SolidWorks builds of involute gears, each a C# job that makes the part from an empty
document (or from the spur part), checks it over a range of parameters, and saves it. They follow
the tutorials of the Machine Elements book at <https://mechanics.ju.se> step by step, and
`parts/` holds the parts built by hand from those tutorials, which the jobs are compared against.

| Job | Builds | Checked against |
|---|---|---|
| `Spur.cs` | spur gear from an empty part | `parts/SpurDefinitive.SLDPRT`: same volume to 1e-14 over 20 cases, N = 8 to 100, m = 1 and 2.5 |
| `Helical.cs` | helical or herringbone gear on a copy of the spur part | every end face against the spur outline turned through 360 z / Lead: within 0.5 to 1.3 µm for N = 8 to 90, m = 0.5 to 2, phi = 20° and 25°, beta = 15° to 45°, b = 6 to 30 mm, both hands; volume within 0.06 % of the spur's |

The straight and spiral bevel gears (`parts/bevel_straight.SLDPRT`, `parts/bevel_spiral.SLDPRT`,
built by hand from the book's tutorials) are next.

`Gear.lib.cs` holds their helpers; `job.ps1` compiles every `*.lib.cs` next to a job with it.

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job examples/gears/Spur.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job examples/gears/Helical.cs -Arg "C:\...\work\Spur.SLDPRT|herringbone"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job jobs/Compare.cs -Arg "C:\...\work\Spur.SLDPRT|examples\gears\parts\SpurDefinitive.SLDPRT|N=8,20,42,100|m=1,2.5|suppress=Chamfer1"
```

## What they show

- **Design intent.** Every size is a global variable or follows from one; every sketch is fully
  defined before it is closed; the involute flank is one equation-driven curve, never a spline
  through computed points; one tooth (or tooth space) is built and then patterned.
- **One curve for every tooth count.** The flank `x(t), y(t)` is the involute for t > 0 and a
  radial line below the base circle for t < 0, with max/min written as `(v + sqr(v^2))/2`, so the
  sketch has the same entities whether the root circle lies inside the base circle (N < 42 at 20°)
  or outside it.
- **Datums the theory gives.** The datum line ends where the flank crosses the pitch circle, the
  mirror plane holds the tooth centreline, the pattern axis is the intersection of two default
  planes. None of them depends on a face of the solid.
- **A twist no equation can reach, carried by a helix.** The sweep's "Specify Twist Value" is not a
  dimension. The helix's height and pitch are (`D3`, `D4`), so the helix drives the twist.
- **Checks against the theory, not "no errors".** Volumes against the reference part or the spur,
  end faces against the turned spur outline, the number of bodies, and every parameter read back
  after the rebuild.

## Ranges found

- **Spur:** rebuilds from N = 8 to 100. `R_fillet` = 0.9 rb (pi/(2N) - inv phi) shrinks with N and
  turns negative at N = 105 (phi = 20°); beyond that SolidWorks refuses the fillet equation.
- **Helical and herringbone:** rebuild up to N = 90 at m = 1; at N = 100 the sweep fails on the tiny
  root fillets (0.034 mm) of the converted profile. The hand-built `HelicalGear.SLDPRT` fails at the
  same N.

## Lessons from building them through the API

- After a rebuild fails, SolidWorks may ignore the next parameter changes: they come back unchanged,
  with no error. `Sw.SetGlobals` reads every value back and reports `NOT APPLIED`; the checks then
  reopen the saved part before the next case.
- Check the saved part reopened, not the document the build left behind: checked in the same session
  straight after the build, some parameter changes had no effect.
- The other API traps (coordinates of `AddDimension2`, curve end locks, merged points, Selected
  Contours, inactive documents) are in [../../README.md](../../README.md).
