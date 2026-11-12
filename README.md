# cad-agent-kit

Let an AI coding assistant, such as Claude Code, build, test, measure and photograph CAD models on
your own computer. The kit holds the scripts that connect the assistant to the CAD program, and the
instructions that make it work the way an engineer does: parametric models, fully defined sketches,
and a check of every result against a number it must have.

| Program | How the assistant drives it | Status |
|---|---|---|
| [SolidWorks](solidworks/) | C# jobs compiled against the SolidWorks API from PowerShell | tested with SolidWorks 2026 |
| [FreeCAD](freecad/) | Python scripts run headless by `freecadcmd` | tested with FreeCAD 1.1.4 |
| [SolidCraft](solidcraft/) | its own MCP server and command line | tested with SolidCraft 0.29; young and slow on many features |
| [Fusion](fusion/) | Python API inside Fusion | not tested yet |

Use SolidWorks where you have it. It has the widest range of features and the most robust kernel of
the four.

## What it can do

- **Build a part from a description, a sketch or a tutorial**, step by step, and say which step
  fails and why.
- **Test a parametric model.** It changes the global variables (a tooth count, a length, an angle)
  case after case, rebuilds, and lists every feature that fails and every result that is wrong. It
  finds the values where your model breaks before your examiner or customer does.
- **Measure.** Volume, exact extents, the position of a hole, an outline compared with the theory.
- **Take screenshots** of dialogs and the model, for reports and instructions.
- **Check a tutorial** by following it literally through the API.

Two examples of what this finds. A helical-gear tutorial typed the sweep's twist angle into the
Sweep dialog and expected an equation to drive it; tested through the API, that angle turned out to
be a setting that no equation can reach. And a plate whose hole is made wider than the plate
rebuilds in both SolidWorks and FreeCAD without a single error, as two separate bodies with the wrong
volume. Only a check against the volume the plate must have catches it.

## What you need

- A Windows PC with the CAD program installed and licensed (student licences work).
- An AI assistant that can run commands on that PC, for example
  [Claude Code](https://claude.com/claude-code) in a terminal or in VS Code.
- For SolidWorks: SolidWorks running, with you logged in to Windows.

Nothing else needs installing for SolidWorks and FreeCAD: the scripts use Windows PowerShell and the
files that come with the CAD program.

## Getting started

1. Clone or download this repository, for example to `C:\cad-agent-kit`. Keep the path free of
   non-ASCII characters.
2. Start SolidWorks (or have FreeCAD installed).
3. Open a terminal in the repository folder and start Claude Code there. It reads
   [CLAUDE.md](CLAUDE.md), which tells it how to work. Other assistants: point them to
   [AGENTS.md](AGENTS.md).
4. Ask: *"Run the SolidWorks smoke test."* It should answer with the SolidWorks version and the
   documents you have open. Then: *"Build the example plate and test it."*

## How to ask

Describe the goal and the part in plain words, and say which file to start from:

- "Open `C:\Projects\gear.SLDPRT` on a copy and check that it rebuilds for N from 8 to 120."
- "Follow the steps in this tutorial in SolidWorks and tell me where they go wrong."
- "Build a bracket like this sketch, 4 mm plate, two M6 holes, parametric in plate thickness and
  hole spacing."
- "Open the Sweep dialog of Sweep1 and take a screenshot of it."

Good reports contain numbers (volumes, extents, which feature failed at which value), not "it works".

## Parametric or not? Decide first

The assistant asks before it builds. If the part must be parametric, it needs to know **which
parameters** (for a gear: module, tooth count, face width, helix angle) and **over what spans** (tooth
count 8 to 120). If you do not know the spans, let it find them: it sweeps the parameters, rebuilds
at every value and reports where the model breaks. Testing a model at a hundred parameter sets is
tedious by hand and quick for the assistant, and this is where it adds the most.

## Ground rules

- **It works on copies.** Your files are copied to `solidworks\work` (or `freecad\work`) before they
  are opened. Keep your own backups regardless.
- **Hands off during screenshots.** To photograph a dialog the assistant brings SolidWorks to the
  front, opens the dialog and presses keys. It asks you first; leave mouse and keyboard alone until
  it is done.
- **Check its claims** the way you would check a classmate's: open the part, change a value, rebuild.
- **The API and the dialogs differ.** Something the API can set may be refused in a dialog, and the
  other way round. Instructions for people must be checked in the real dialogs too.
- **Coursework:** follow your course's rules on AI. Having the assistant check your model is not the
  same as having it make the model.

## Layout

```
CLAUDE.md          instructions for the assistant (AGENTS.md points here)
docs/lessons.md    what building gears and plates this way taught us
solidworks/        job.ps1, remote.ps1, ui.ps1, lib/Sw.cs, jobs/*.cs, README.md
  examples/gears/  spur, helical and herringbone gears, built and verified from scratch
freecad/           plate_sweep.py, README.md
solidcraft/        README.md
fusion/            README.md (to be written)
```

## Contributing

New jobs, programs and lessons are welcome. A SolidWorks job is one C# file with a class `Job` and a
method `Run(string)`; existing C# macros port easily (see [solidworks/README.md](solidworks/README.md)).
Every lesson in `docs/lessons.md` should come from a part that was actually built and checked, with
the version of the program it was found in.

## Licence

MIT, see [LICENSE](LICENSE).
