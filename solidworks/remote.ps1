# remote.ps1: run a kit script inside the logged-in desktop session, for shells that are not in it
# (ssh, a service). COM to SolidWorks fails there with CO_E_SERVER_EXEC_FAILURE, and so does any UI work.
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File remote.ps1 job.ps1 -Job jobs\Hello.cs
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File remote.ps1 ui.ps1 shot C:\temp\screen.png
# Everything after the script name is passed on to it. Optional: $env:CAD_KIT_USER, $env:CAD_KIT_TIMEOUT (s).
param([string]$Script)
$here = $PSScriptRoot
$user = $env:CAD_KIT_USER
if (-not $user) {   # Win32_ComputerSystem.UserName is empty while the session is disconnected; quser is not
  $line = quser 2>$null | Select-Object -Skip 1 | Select-Object -First 1
  if ($line) { $user = ($line.Trim().TrimStart('>') -split '\s+')[0] }
}
if (-not $user) { 'No one is logged in at the desktop; SolidWorks needs a logged-in session.'; exit 1 }
$timeout = if ($env:CAD_KIT_TIMEOUT) { [int]$env:CAD_KIT_TIMEOUT } else { 900 }
# every run has its own request, result and task name, so a run that outlives its timeout cannot
# be confused with the next one
$id = (Get-Date -Format 'yyyyMMdd_HHmmss_') + (Get-Random -Maximum 10000)
$dir = Join-Path $here 'work\remote'; New-Item -ItemType Directory -Force $dir | Out-Null
$out = Join-Path $dir "$id.result.txt"; $req = Join-Path $dir "$id.request.ps1"; $task = "CadAgentKit_$id"
# write the call literally: -Name tokens stay parameter names, everything else is a quoted string
$q = { param($s) "'" + ($s -replace "'", "''") + "'" }
$call = "& " + (& $q (Join-Path $here $Script))
foreach ($a in $args) { $call += ' ' + $(if ("$a" -match '^-[A-Za-z]\w*$') { $a } else { & $q "$a" }) }
@"
`$env:CAD_KIT_WORK = $(& $q (Join-Path $here 'work'))
try { $call *>&1 | Out-String -Width 500 | Set-Content -Encoding UTF8 $(& $q $out) }
catch { `$_.ToString() | Set-Content -Encoding UTF8 $(& $q $out) }
'done' | Set-Content $(& $q "$out.done")
"@ | Set-Content -Encoding UTF8 $req
# conhost --headless keeps the console off the user's screen (Windows Terminal ignores hidden windows)
$a = New-ScheduledTaskAction -Execute conhost.exe -Argument "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$req`""
$p = New-ScheduledTaskPrincipal -UserId "$env:COMPUTERNAME\$user" -LogonType Interactive
Register-ScheduledTask -TaskName $task -Action $a -Principal $p -Force -ErrorAction Stop | Out-Null
Start-ScheduledTask -TaskName $task
$t = 0; while (-not (Test-Path "$out.done") -and $t -lt $timeout) { Start-Sleep 1; $t += 1 }
Unregister-ScheduledTask -TaskName $task -Confirm:$false
if (Test-Path "$out.done") { Get-Content $out }
else { "TIMEOUT after $timeout s. The job goes on and writes $out (and $out.done) when it ends. A long job is fine; " +
       "one waiting for a dialog in SolidWorks needs the dialog closed (look first: ui.ps1 shot). Do not kill it." }
