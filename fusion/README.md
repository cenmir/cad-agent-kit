# Fusion

Not tested yet. The notes below come from Autodesk's API documentation and have not been checked here.

- Fusion's API (Python and C++) runs inside Fusion, as a script or an add-in started from
  Utilities > Add-Ins. There is no headless console program like `freecadcmd` and no COM server like
  SolidWorks'.
- To drive it from an assistant, an add-in has to receive the commands, for example by watching a
  folder for script files or by listening on a local port, and write back a report.
- Parameters are user parameters (`design.userParameters`), and sketch dimensions and feature
  extents take expressions of them, so the rules in [CLAUDE.md](../CLAUDE.md) apply unchanged.

Contributions that build and test this bridge are welcome. Add only what has been run, with the Fusion
version.
