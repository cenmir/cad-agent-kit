# Instructions for the AI assistant

You drive a CAD program on the user's Windows PC through the scripts in this repository. Read this
file, then the README of the program you use ([solidworks/](solidworks/README.md),
[freecad/](freecad/README.md), [solidcraft/](solidcraft/README.md), [fusion/](fusion/README.md)),
and [docs/lessons.md](docs/lessons.md) before you build anything.

## Which program

Use SolidWorks whenever it is installed (the SolidWorks README shows how to check). It is the most
capable and robust. Use FreeCAD or SolidCraft when there is no SolidWorks, or when the user asks for
them. SolidCraft is young: many features take minutes, and it is less robust. For SolidWorks, write
C# and nothing else (no VBA, no Python bridges): the jobs in this kit, the API examples and most
users' existing macros are C#, and new jobs must fit next to them.

## First session on a new PC

1. Check the environment: Windows, the CAD program installed, Windows PowerShell 5.1
   (`powershell.exe`, not `pwsh`). The SolidWorks README has the commands.
2. Ask the user to start SolidWorks if it is not running.
3. Run the smoke test (`jobs\Hello.cs`) and report the version it prints. If it fails, follow the
   troubleshooting table in the SolidWorks README; do not guess.
4. Build the example plate (`jobs\Plate.cs`) once to prove that building, equations and measuring
   work on this machine.

## Rules of conduct

- Work on copies. `Sw.OpenCopy` copies a file into the work folder under a new name before opening
  it. Never save over a user's file, and never close a user's document unasked.
- Ask before you take over the screen (focus, dialogs, keystrokes, screenshots), and tell the user
  when you are done.
- Never kill a job while it is inside a call to SolidWorks; that has crashed SolidWorks. A job that
  hangs is almost always waiting for a dialog: find it with a screenshot and close it.
- Report numbers: feature errors, volumes, extents, deviations from the expected value. Say what you
  verified through the API and what you checked in the real dialogs.
- Use the program's default feature names (Sketch3, Helix/Spiral1, Sweep1) unless the user asks
  otherwise; instructions for people must match what they will see.

## Ask before you build

- Is the part to be parametric?
- If so: which parameters, and what span for each (smallest and largest value)?
- If the user does not know the spans, offer to find them: sweep the parameters, rebuild at every
  value, report where the model fails and why. Present the sweep as a table (parameter values,
  failing features, key measurements), propose fixes, and sweep again after each fix.

## Rules for generating CAD

**Do not assume. Build it and check it, all the time.**

- Work the numbers out before you build: the parameters, the volume and extents the part must have
  after each feature, where each hole or face must be.
- After every feature: rebuild, read the error codes of the whole tree, and measure what the feature
  should have done. A call that returned without error proves nothing. A cut that leaves the volume
  unchanged has missed; a part that becomes two bodies has been cut in two.
- Never assume orientations, names or directions. Read plane orientations, feature names and the
  direction a feature went from the model. In the SolidWorks templates we tested, the Front Plane's
  normal was model x, not z.
- **Every sketch is fully defined** before it is closed (SolidWorks:
  `Sketch.GetConstrainedStatus() == swFullyConstrained`; FreeCAD: `sketch.FullyConstrained`).
  An under-defined sketch moves when a parameter changes.
- **Design intent:** sizes come from global variables through equations or expressions; curves come
  from their equations (equation-driven curves), never from a spline through points you computed;
  one feature and then a pattern; references to planes, axes and the origin rather than to faces and
  edges, which are renumbered when a parameter changes.
- Check against an independent value: a volume from the formula, the number of bodies, exact extents,
  an outline compared with the theory. Use a relative tolerance (SolidWorks mass properties on curved
  faces are good to about 1e-5).
- Run every sweep over the whole span the user gave, including the default values.

## Writing new code

- Read the API first. Look up a signature by reflection on the interop assembly (SolidWorks) or in
  the program's help instead of guessing; the SolidWorks README shows how.
- Keep jobs small: one job, one purpose, a text report as the result. Put helpers that more than one
  job needs into `solidworks/lib/Sw.cs`.
- When you learn something new about a program (a trap, a refused option, a reserved name), add it to
  that program's README or to `docs/lessons.md`, with the version you found it in.
