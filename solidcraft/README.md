# SolidCraft

SolidCraft is a parametric CAD program built to be driven by an AI assistant. It needs no bridge
code: it ships an MCP server (Model Context Protocol, the standard way to give an assistant tools)
and a command line. Tested with SolidCraft 0.29 (November 2026) on Windows 11.

It is young. Many features take a long time to compute, and it cannot yet match SolidWorks in range or
robustness. Use it when SolidWorks is not available.

## MCP server

```
solidcraft-cli mcp --headless                          # its own session, no window
solidcraft-cli mcp --connect 127.0.0.1:PORT            # drive the open SolidCraft window; the user watches
```

Start the program with a control port (`solidcraft --control PORT`) for the second form. Register the
server with the assistant, for Claude Code:

```
claude mcp add solidcraft -- "C:\Program Files\SolidCraft\solidcraft-cli.exe" mcp --connect 127.0.0.1:27878
```

The tools include `execute`, `batch` (all or nothing), `set_parameter`, `check_parameters` (try values
and put the design back), `measure`, `body_topology`, `screenshot`, `export`, `undo`, and
`skill_reference`, which returns the program's own modelling guide. Read that guide before the first
build.

## Command line

```
solidcraft-cli commands                                   # every command and its parameters, as JSON
solidcraft-cli run s.json --results --save s.sc --json-out out.json
solidcraft-cli eval s.sc --json-out out.json              # measure
solidcraft-cli snapshot --in s.sc --out s.png --view iso  # picture
solidcraft-cli script design.sc                           # a design's history back as a script
```

A script is `{"commands": [{"command": "sketch.create", "params": {...}}, ...]}`.

## Things to know

- Units are millimetres and degrees in expressions; Z is up.
- Parameters are named, and feature fields take expressions (`"L/2 - 10"`). A plain number never
  follows a parameter.
- `sketch.equation_curve` draws a curve from x(t), y(t) over named parameters and refits it when they
  change. An involute fitted at tolerance 1e-4 had 32 points and 0.09 µm deviation.
- Primitives with expressions (`solid.box`, `solid.cylinder`, `solid.hole`, patterns, mirror) build
  fast and follow the parameters; sketch only when the profile is neither a box nor a circle.
- `solid.fillet` supports only edges between two planar faces, and long fillets can run for many
  minutes. A fillet on all 40 root edges of a gear ran for 16 minutes and the program had to be
  killed, losing unsaved work. Fillet one tooth with `fit: "reduce"` (it shrinks the radius to what
  fits and says so), then pattern. **Save before every slow step:** a running command cannot be
  cancelled.
- A call whose error starts `still running:` was not cancelled; do not send it again.
- `solidcraft-cli snapshot` failed on a path with non-ASCII characters. Keep work paths ASCII.
- Verify with `measure` after each stage: a cut that leaves the volume unchanged has missed, a join that
  adds a body did not touch. Hole positions come from `body_topology` (cylinder axis and radius).
