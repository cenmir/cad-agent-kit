# ui.ps1: drive the screen of the desktop session, for screenshots of dialogs. Run it in that session
# (directly, or through remote.ps1). Ask the user to keep hands off the mouse and keyboard first.
#   ui.ps1 focus                      bring SolidWorks to the front, maximised
#   ui.ps1 shot C:\temp\screen.png    capture the primary screen
#   ui.ps1 cursor X Y                 move the mouse (away from the dialog, or a tooltip ends up in the shot)
#   ui.ps1 click X Y                  left click
#   ui.ps1 type X Y "text" [enter]    click into a field, select all, type (SendKeys syntax), optionally Enter
#   ui.ps1 keys "{ESC}"               send keys to the foreground window (SendKeys syntax)
#   ui.ps1 resize "Equations" X Y W H move and size every visible window whose title contains the text
# Coordinates are physical pixels.
param([string]$Action, [string]$A1, [string]$A2, [string]$A3, [string]$A4, [string]$A5)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class UI {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool r);
  public static List<string> Fit(string part, int x, int y, int w, int hh) {
    var log = new List<string>();
    EnumWindows((h, l) => { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); var t = sb.ToString();
      if (IsWindowVisible(h) && t.Contains(part)) log.Add(t + " -> " + MoveWindow(h, x, y, w, hh, true)); return true; }, IntPtr.Zero);
    return log; }
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(100);
    mouse_event(2, 0, 0, 0, UIntPtr.Zero); mouse_event(4, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(300); }
}
"@
[UI]::SetProcessDPIAware() | Out-Null
$send = { param($s) [System.Windows.Forms.SendKeys]::SendWait($s); Start-Sleep -Milliseconds 300 }
switch ($Action) {
  'focus' {
    $h = (Get-Process SLDWORKS -ErrorAction Stop | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1).MainWindowHandle
    [UI]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [UI]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)  # an Alt tap lifts the foreground lock
    [UI]::ShowWindow($h, 3) | Out-Null; [UI]::SetForegroundWindow($h) | Out-Null; Start-Sleep -Milliseconds 500
    "SolidWorks in front: " + ([UI]::GetForegroundWindow() -eq $h)
  }
  'shot' {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save($A1); "saved $A1 ($($b.Width) x $($b.Height)); a blank image means the session is disconnected or locked"
  }
  'cursor' { [UI]::SetCursorPos([int]$A1, [int]$A2) | Out-Null; "cursor at $A1,$A2" }
  'click'  { [UI]::Click([int]$A1, [int]$A2); "clicked $A1,$A2" }
  'type'   {
    [UI]::Click([int]$A1, [int]$A2); & $send '^a'
    foreach ($ch in $A3.ToCharArray()) { [System.Windows.Forms.SendKeys]::SendWait([string]$ch); Start-Sleep -Milliseconds 50 }
    if ($A4 -eq 'enter') { & $send '{ENTER}' }; "typed into $A1,$A2"
  }
  'keys'   { & $send $A1; "sent $A1" }
  'resize' { $r = [UI]::Fit($A1, [int]$A2, [int]$A3, [int]$A4, [int]$A5); if ($r) { $r } else { "no visible window with '$A1' in its title" } }
  default  { "unknown action '$Action': focus, shot, cursor, click, type, keys, resize" }
}
