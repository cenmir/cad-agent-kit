# SolidWorks

The assistant writes each task as a small C# file (a *job*), and `job.ps1` compiles it against the
SolidWorks API assemblies and runs it in the SolidWorks that is already open. Tested on Windows 11
with SolidWorks 2026 (rev 34.4.1) and Windows PowerShell 5.1.

| File | Purpose |
|---|---|
| `job.ps1` | compile `lib\*.cs` + one job, run `Job.Run(arg)`, print the result |
| `remote.ps1` | run `job.ps1` or `ui.ps1` inside the desktop session, for shells that are not in it (ssh) |
| `ui.ps1` | focus SolidWorks, screenshot, click, type, keys, resize a dialog |
| `lib/Sw.cs` | helpers: connect, open a copy, set globals, tree, failures, bodies with exact extents |
| `jobs/Hello.cs` | smoke test: version and open documents |
| `jobs/Inspect.cs` | a copy of a part: equations, feature tree, errors, bodies |
| `jobs/Sweep.cs` | a copy of a part rebuilt over every combination of global values |
| `jobs/Plate.cs` | builds a parametric plate from scratch and tests it: the method in one file |
| `jobs/Open.cs`, `jobs/Close.cs` | open a document visibly; close documents without saving |
| `jobs/Dialog.cs`, `jobs/Equations.cs` | open a feature's PropertyManager, or Tools > Equations |

## Running a job

From the repository's `solidworks` folder, in a terminal on the Windows desktop (Claude Code's Bash,
PowerShell or cmd):

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job jobs\Hello.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job jobs\Inspect.cs -Arg "C:\parts\bracket.SLDPRT"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job jobs\Sweep.cs -Arg "C:\parts\gear.SLDPRT|N=8,20,60|HelixAngle=15,30"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job jobs\Plate.cs
```

In Bash (Claude Code's default shell on Windows) a backslash outside quotes is an escape character:
write `jobs/Hello.cs` or `"jobs\Hello.cs"`, and quote every Windows path.

Always `powershell.exe` (Windows PowerShell 5.1), never `pwsh`: PowerShell 7 runs on .NET without
`Marshal.GetActiveObject`. `job.ps1` finds the newest installed SolidWorks in the registry
(`HKLM:\SOFTWARE\SolidWorks\SOLIDWORKS <year>\Setup`) and falls back to
`C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS`. Work copies go to `solidworks\work`.

**From a shell outside the desktop session** (ssh into the PC, a service), COM to SolidWorks fails
with `CO_E_SERVER_EXEC_FAILURE`, and screenshots and clicks have no screen. Put `remote.ps1` in front;
it runs the same command as a one-off scheduled task in the session of the logged-in user and prints
the result:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File remote.ps1 job.ps1 -Job jobs\Hello.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File remote.ps1 ui.ps1 shot C:\temp\screen.png
```

It finds the user with `quser` (`Win32_ComputerSystem.UserName` is empty while the session is
disconnected); set `$env:CAD_KIT_USER` to choose one. It waits 900 s (`$env:CAD_KIT_TIMEOUT`). A
Windows ssh server usually starts `cmd`, so put anything longer than one line into a `.ps1` file.

## First run: checks and troubleshooting

```powershell
$PSVersionTable.PSVersion                                   # 5.1 in powershell.exe
Get-ChildItem HKLM:\SOFTWARE\SolidWorks | % PSChildName     # installed versions
Get-Process SLDWORKS | select Id, SessionId, MainWindowTitle # running, and in which session
```

| Symptom | Cause and fix |
|---|---|
| `SolidWorks is not reachable (Operation unavailable ...)` | SolidWorks is not running. Ask the user to start it. |
| same, although SolidWorks runs | The terminal and SolidWorks run at different elevation (one "as administrator"). Both normal, or both elevated. |
| `CO_E_SERVER_EXEC_FAILURE` | The shell is not in the desktop session: use `remote.ps1`. |
| `API assemblies not found` | Unusual install path: check `api\redist\SolidWorks.Interop.sldworks.dll` under the SolidWorks folder. |
| `JOB FAILED` with line numbers | A compile error in the job; the line numbers count from the top of `lib\*.cs` + job. |
| `... is an ambiguous reference between 'System.X' and 'SolidWorks.Interop.sldworks.X'` | Write `System.Environment`, `System.Attribute` etc. in full. |
| Running scripts is disabled | `-ExecutionPolicy Bypass` covers the normal policy; a group policy can still forbid it, ask IT. |
| The job never returns | It waits for a dialog. Screenshot (`ui.ps1 shot`), then close the dialog. Do not kill the job. |
| A blank screenshot | The desktop session is disconnected or locked; someone must be logged in at the screen. |

## Writing a job

A job is one static class `Job` with `public static string Run(string arg)`; the returned text is the
report. `job.ps1` adds the `using` lines (`System`, `System.IO`, `System.Text`,
`System.Collections.Generic`, `System.Runtime.InteropServices`, `SolidWorks.Interop.sldworks`,
`SolidWorks.Interop.swconst`) and strips the job's own, so jobs carry none.

```csharp
// Report the mass of a copy of a part. Arg: the original file.
public static class Job {
  public static string Run(string file) {
    var sw = Sw.App(); ModelDoc2 doc = null;
    try {
      doc = Sw.OpenCopy(sw, file);
      doc.ForceRebuild3(false);
      return "rebuild: " + Sw.Failures(doc) + "\r\n" + Sw.Bodies(doc);
    } catch (Exception ex) { return "EXCEPTION " + ex.Message; }
    finally { Sw.Close(sw, doc); }
  }
}
```

- The compiler is C# 5 (Windows PowerShell's `Add-Type`): no `$"..."`, `?.`, `out var`, `nameof` or
  `=>` members. `string.Format` and ordinary lambdas work.
- `swcommands` is referenced but not imported: write
  `SolidWorks.Interop.swcommands.swCommands_e.swCommands_EditFeature` in full.
- Look up a signature instead of guessing:
  ```powershell
  $a = [Reflection.Assembly]::LoadFrom('C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll')
  $a.GetType('SolidWorks.Interop.sldworks.IFeatureManager').GetMethods() | ? Name -eq FeatureCut4 | % { $_.ToString() }
  ```
- **Porting an existing C# macro or add-in:** replace the macro's `swApp` with `Sw.App()`, put the body
  into `Job.Run`, and return the text it would have shown in a message box. Message boxes and
  `Debug.Print` have no one to read them.

## The method in one job: `jobs/Plate.cs`

`Plate.cs` builds a plate with a centre hole from an empty part and is worth reading before building
anything. It sets the units to millimetres, adds the globals `Length`, `Width`, `PlateThickness`,
`HoleDia`, draws two sketches, links every dimension to a global, checks that each sketch is fully
defined before closing it, extrudes, cuts, and rebuilds five parameter sets, each checked against the
volume `(L W - pi D^2/4) T` and the number of bodies. Its output on SolidWorks 2026:

```
plate sketch Sketch1: swFullyConstrained
hole sketch Sketch2: swFullyConstrained
built: ok
L=60 W=40 T=5 D=6 | ok | bodies 1 | V=11858.636 expected 11858.628
L=60 W=40 T=5 D=30 | ok | bodies 1 | V=8465.615 expected 8465.708
L=60 W=40 T=5 D=45 | ok | bodies 2 | V=4395.272 expected 4047.844  <-- MISMATCH
L=120 W=20 T=5 D=10 | ok | bodies 1 | V=11607.321 expected 11607.301
L=60 W=40 T=5 D=10 | ok | bodies 1 | V=11607.321 expected 11607.301
```

The hole of 45 mm is wider than the 40 mm plate. SolidWorks rebuilds without an error or a warning
and leaves two bodies; only the check against the expected volume and the body count shows it.

## Common operations

| Task | API |
|---|---|
| New part | `sw.NewDocument(sw.GetUserPreferenceStringValue(swDefaultTemplatePart), 0, 0, 0)` |
| Units | `Extension.SetUserPreferenceInteger(swUnitSystem, 0, swUnitSystem_MMGS)`; templates differ |
| Global variable | `Sw.SetGlobal(doc, "N", "20")` (`EquationMgr.set_Equation` or `Add2`) |
| Link a dimension | `Add2(-1, "\"D1@Sketch1\" = \"Length\"", true)`, name from `Dimension.Name + "@" + sketch.Name` |
| Sketch | `SelectByID2("Front Plane", "PLANE", ...)`, `SketchManager.InsertSketch(true)`, `CreateLine`, `CreateCenterRectangle`, `CreateCircleByRadius` |
| Dimension | select the entity, `AddDimension2(x, y, 0)` (the position of the text, in metres) |
| Fully defined? | `((Sketch)sm.ActiveSketch).GetConstrainedStatus()` = 3 before `InsertSketch(true)` closes it |
| Equation-driven curve | `SketchManager.CreateEquationSpline2` |
| Extrude, cut | `FeatureManager.FeatureExtrusion3` (23 arguments), `FeatureCut4` (27) |
| Sweep, helix | `InsertProtrusionSwept4`, `InsertHelix` |
| Bodies | `InsertDeleteBody2`, `InsertMirrorFeature2` |
| Change a feature | `Feature.GetDefinition()`, edit the data object, `Feature.ModifyDefinition` |
| Suppress | `Feature.SetSuppression2` |
| Rebuild and failures | `ForceRebuild3(false)`, `Sw.Failures(doc)` (`Feature.GetErrorCode2` over the tree) |
| Measure | `Body2.GetMassProperties` (volume = `[3]`, m^3), `Body2.GetExtremePoint` for exact extents, planes from `Surface.PlaneParams`, edge points from `Curve.GetTessPts` |
| Plane orientation | `((RefPlane)feature.GetSpecificFeature2()).Transform` |

The API works in metres and radians. Equations and dialogs use the document's units (degrees for
angles, including `sin()` and `atn()` inside equations).

## Screenshots of the user interface

Ask the user to keep hands off first. The sequence for a screenshot of a feature's dialog:

```
job.ps1 -Job jobs\Open.cs -Arg "C:\...\copy.SLDPRT"   # visible, active, isometric
ui.ps1 focus                                          # SolidWorks maximised and in front
job.ps1 -Job jobs\Dialog.cs -Arg Sweep1               # Edit Feature: the PropertyManager opens
ui.ps1 cursor 3000 1300                               # mouse away, or a tooltip lands in the picture
ui.ps1 shot C:\temp\dialog.png                        # full screen; crop afterwards
ui.ps1 keys "{ESC}"                                   # cancel the dialog, nothing changes
job.ps1 -Job jobs\Close.cs -Arg copy.SLDPRT           # close the copy without saving
```

(Prefix each with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`, and with `remote.ps1`
from outside the desktop session.) Look at every picture before using it.

- Edit Feature is `RunCommand(swCommands_EditFeature = 623)`, Tools > Equations is
  `RunCommand(swCommands_Equations = 51)`. The Equations dialog is modal: the job returns only when the
  dialog closes, so run it through `remote.ps1` or in the background, then screenshot and Esc.
- A PropertyManager left open blocks the next API command. Close it with Esc.
- `ui.ps1 resize "Equations" X Y W H` moves and sizes a dialog by its title.
- A tap of Alt before `SetForegroundWindow` (in `ui.ps1 focus`) lets a background process bring
  SolidWorks to the front.
- Scheduled tasks start through `conhost.exe --headless`; Windows Terminal ignores hidden windows and
  flashes a console on the user's screen.

## Traps

- **Invisible dialogs.** `AddDimension2` opens the Modify box and waits for a click unless
  `swInputDimValOnCreate` is off. `Sw.App()` turns it off.
- **Reserved names.** `EquationMgr.Add2` returns -1, without a reason, for a global called
  `Thickness` (reserved, presumably for sheet metal). `Length`, `Width`, `Height`, `Depth`, `T` and
  `HoleDia` are accepted. Check the return value of every `Add2`.
- **Inferred relations.** `CreateLine` and friends snap to nearby geometry as the mouse does and add
  relations that break later. In an empty sketch the only thing to snap to is the origin, which is
  usually wanted. Next to model geometry set `SketchManager.AddToDB = true` while creating entities,
  then add the relations you want with `SketchAddConstraints`.
- **Through All - Both.** `FeatureCut4` with `swEndCondThroughAllBoth` in one direction returns null.
  Use two directions, each `swEndCondThroughAll`.
- **Loose boxes.** `Body2.GetBodyBox` was 0.15 mm too big on a plain plate. `Sw.Bodies` uses
  `GetExtremePoint`, which is exact.
- **Approximate volume.** `GetMassProperties` differs from the exact volume by about 1e-5 relative
  once there are curved faces. Compare with a relative tolerance.
- **Finding what you just made.** `FeatureByPositionReverse(0)` is not reliably the new feature. Take a
  new sketch from `SketchManager.ActiveSketch` before closing it, and a feature from the return value.
- **Duplicate titles.** `OpenDoc6` fails with error 65536 if a document with the same file name is
  open; `Sw.OpenCopy` gives every copy a time stamp.
- **Orientation.** In the templates we tested, the Front Plane's normal is model x. Read it from
  `RefPlane.Transform` (the normal is elements 6 to 8).
- **Face references.** Convert Entities and other face references renumber when parameters change
  ("could not find face or plane"). Prefer planes, axes and sketch geometry.
- **Values in dialogs.** A field driven from Tools > Equations still shows a plain number in its
  PropertyManager. Show the Equations dialog when the links matter.

## SolidWorks behaviour worth knowing

- A sweep's **Specify Twist Value** is a feature setting, not a dimension; no equation reaches it.
  Drive the twist by a helix guide curve instead (Profile Twist: Follow Path and 1st Guide Curve);
  the helix need not touch the profile.
- The **Helix/Spiral** dialog accepts numbers only, but its height and pitch are dimensions (`D3`, `D4`)
  that Tools > Equations can drive.
- **Mirror Entities** turns an equation-driven curve into a plain spline, and equation-driven curves
  cannot be trimmed. Mirror the feature instead.
- A **sketch fillet** can turn inside out at other parameter values. Fillet the solid edge.
- **Toolbox gears** are not involutes: each flank is a circular arc of radius d/5. Never use them where
  the tooth shape matters.
