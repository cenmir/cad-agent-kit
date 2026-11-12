# job.ps1: compile lib\*.cs plus one C# job against the SolidWorks API and print Job.Run(Arg).
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job jobs\Hello.cs
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File job.ps1 -Job jobs\Inspect.cs -Arg "C:\parts\bracket.SLDPRT"
# Needs Windows PowerShell 5.1 (powershell.exe). PowerShell 7 (pwsh) has no Marshal.GetActiveObject.
param([Parameter(Mandatory = $true)][string]$Job, [string]$Arg = '')
$here = $PSScriptRoot
if (-not $env:CAD_KIT_WORK) { $env:CAD_KIT_WORK = Join-Path $here 'work' }
try {
  # the newest installed SolidWorks that has the API assemblies
  $dirs = Get-ChildItem 'HKLM:\SOFTWARE\SolidWorks' -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -match '^SOLIDWORKS \d{4}$' } | Sort-Object PSChildName -Descending |
    ForEach-Object { (Get-ItemProperty "$($_.PSPath)\Setup" -ErrorAction SilentlyContinue).'SolidWorks Folder' }
  $dirs = @($dirs) + 'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\'
  $redist = $dirs | Where-Object { $_ -and (Test-Path (Join-Path $_ 'api\redist\SolidWorks.Interop.sldworks.dll')) } |
    Select-Object -First 1 | ForEach-Object { Join-Path $_ 'api\redist' }
  if (-not $redist) { throw 'SolidWorks API assemblies not found (api\redist\SolidWorks.Interop.sldworks.dll)' }
  $refs = 'sldworks', 'swconst', 'swcommands' | ForEach-Object { Join-Path $redist "SolidWorks.Interop.$_.dll" }
  foreach ($r in $refs) { Add-Type -Path $r }
  $jobPath = if ([IO.Path]::IsPathRooted($Job)) { $Job } else { Join-Path $here $Job }
  $files = @(Get-ChildItem (Join-Path $here 'lib\*.cs') | ForEach-Object FullName) + $jobPath
  $src = ($files | ForEach-Object { Get-Content -Raw $_ }) -join "`n"
  $src = $src -replace '(?m)^using [^\r\n]*\r?\n', ''   # one shared set of usings, below
  $src = "using System; using System.IO; using System.Text; using System.Collections.Generic; " +
         "using System.Runtime.InteropServices; using SolidWorks.Interop.sldworks; using SolidWorks.Interop.swconst;`n" + $src
  Add-Type -TypeDefinition $src -ReferencedAssemblies $refs -ErrorAction Stop
  [Job]::Run($Arg)
} catch {
  "JOB FAILED`n" + $_.Exception.Message   # compile errors arrive here as text
}
