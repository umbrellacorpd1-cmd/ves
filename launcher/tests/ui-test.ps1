<#
  End-to-end UI test for Umbra Launcher on a real Windows desktop (GitHub Actions windows runner).
  Part 1 drives the real flow through UI Automation: add notepad.exe via the dialog, launch it from its card,
  keep it running ~70 s, close it, and assert that a matching session was written to library.json.
  Part 2 seeds demo data and captures screenshots of every screen.
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class TestNative {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODE {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion; public short dmDriverVersion; public short dmSize; public short dmDriverExtra;
        public int dmFields; public int dmPositionX; public int dmPositionY; public int dmDisplayOrientation; public int dmDisplayFixedOutput;
        public short dmColor; public short dmDuplex; public short dmYResolution; public short dmTTOption; public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels; public int dmBitsPerPel; public int dmPelsWidth; public int dmPelsHeight;
        public int dmDisplayFlags; public int dmDisplayFrequency; public int dmICMMethod; public int dmICMIntent;
        public int dmMediaType; public int dmDitherType; public int dmReserved1; public int dmReserved2;
        public int dmPanningWidth; public int dmPanningHeight;
    }
    [DllImport("user32.dll")] public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
    [DllImport("user32.dll")] public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    public static string SetResolution(int w, int h) {
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        if (!EnumDisplaySettings(null, -1, ref dm)) return "EnumDisplaySettings failed";
        dm.dmPelsWidth = w; dm.dmPelsHeight = h; dm.dmFields = 0x80000 | 0x100000;
        return ChangeDisplaySettings(ref dm, 0).ToString();
    }
}
"@

[void][TestNative]::SetProcessDPIAware()
$AE = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]

function Find-El($parent, [string]$id, [int]$timeout = 20) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeout) {
        $el = $parent.FindFirst($Scope::Descendants, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 300
    }
    throw "UI element '$id' not found within $timeout s"
}

function Invoke-El($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Select-El($el) { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Set-Value($el, [string]$text) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text) }
function Get-Value($el) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value }

function Save-Shot([string]$name) {
    try {
        $r = $script:win.Current.BoundingRectangle
        $x = [int]$r.X; $y = [int]$r.Y; $w = [int]$r.Width; $h = [int]$r.Height
        if ($w -le 0 -or $h -le 0) { throw "window has no size" }
    } catch {
        $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $x = $b.X; $y = $b.Y; $w = $b.Width; $h = $b.Height
    }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $bmp.Save((Join-Path $Out "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "  screenshot $name.png ($w x $h)"
}

function Start-Launcher([string]$dataDir) {
    $p = Start-Process -FilePath $Exe -ArgumentList @('--data', "`"$dataDir`"") -PassThru
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $p.Id)),
        (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt 30) {
        if ($p.HasExited) { throw "Launcher exited early with code $($p.ExitCode)" }
        $w = $AE::RootElement.FindFirst($Scope::Children, $cond)
        if ($w) { $script:win = $w; $script:proc = $p; Start-Sleep -Milliseconds 1500; return }
        Start-Sleep -Milliseconds 300
    }
    throw 'Launcher window did not appear'
}

function Stop-Launcher {
    if ($script:proc -and -not $script:proc.HasExited) { Stop-Process -Id $script:proc.Id -Force; $script:proc.WaitForExit(10000) | Out-Null }
}

function Bring-Front {
    $h = [IntPtr]$script:win.Current.NativeWindowHandle
    try { $script:win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal) } catch { }
    [void][TestNative]::ShowWindow($h, 9)
    [void][TestNative]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 800
}

function Hover($el) {
    $r = $el.Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point ([int]($r.X + $r.Width / 2)), ([int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 600
}

function Park-Mouse { [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point 5, 1070 }

function Scroll-Top {
    try {
        $sv = Find-El $script:win 'LibraryScroller' 5
        $sv.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern).SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 0)
        Start-Sleep -Milliseconds 500
    } catch { Write-Host "  (scroll-to-top unavailable: $($_.Exception.Message))" }
}

$failed = $false
$e2eDir = Join-Path $env:RUNNER_TEMP 'umbra-e2e'
$demoDir = Join-Path $env:RUNNER_TEMP 'umbra-demo'

try {
    Write-Host "Display: $([TestNative]::SetResolution(1920, 1080)) -> $([System.Windows.Forms.Screen]::PrimaryScreen.Bounds)"
    Park-Mouse

    # ---------------- Part 1: real add -> launch -> track flow ----------------
    Write-Host '== Part 1: add notepad.exe, launch it, verify tracked time'
    Remove-Item -Recurse -Force $e2eDir -ErrorAction SilentlyContinue
    Start-Launcher $e2eDir
    Save-Shot '01-empty-state'

    Invoke-El (Find-El $win 'AddButton')
    $pathBox = Find-El $win 'EditorPath'
    Set-Value $pathBox 'C:\Windows\System32\notepad.exe'
    Start-Sleep -Milliseconds 1800
    $nameBox = Find-El $win 'EditorName'
    $autoName = Get-Value $nameBox
    Write-Host "  auto-detected name: '$autoName'"
    if ($autoName -ne 'Notepad') { Set-Value $nameBox 'Notepad' }
    Save-Shot '02-add-dialog'
    Invoke-El (Find-El $win 'EditorSave')
    [void](Find-El $win 'Card_Notepad')
    Start-Sleep -Seconds 2
    Save-Shot '03-first-app'

    $before = @(Get-Process notepad -ErrorAction SilentlyContinue | ForEach-Object Id)
    Invoke-El (Find-El $win 'Play_Notepad')
    $launchedAt = Get-Date
    Start-Sleep -Seconds 3
    $state = $win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Current.WindowVisualState
    Write-Host "  launcher window state after launch: $state (minimize-on-launch is on by default)"
    $np = @(Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id })
    if ($np.Count -eq 0) { throw 'Notepad did not start' }
    Write-Host "  notepad pid(s): $($np.Id -join ', ')"

    Start-Sleep -Seconds 20
    Bring-Front
    Save-Shot '04-running'

    while (((Get-Date) - $launchedAt).TotalSeconds -lt 70) { Start-Sleep -Seconds 1 }
    Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id } | Stop-Process -Force
    Write-Host '  notepad closed after ~70 s, waiting for the session to be saved'
    Start-Sleep -Seconds 14

    $lib = Get-Content (Join-Path $e2eDir 'library.json') -Raw | ConvertFrom-Json
    $app = $lib.apps | Where-Object { $_.name -eq 'Notepad' }
    if (-not $app) { throw 'Notepad missing from library.json' }
    $sessions = @($app.sessions)
    Write-Host "  sessions recorded: $($sessions.Count); launches: $($app.launches)"
    if ($sessions.Count -lt 1) { throw 'No session was recorded' }
    $d = [int]$sessions[-1].d
    Write-Host "  last session: $d s (expected ~70 s)"
    if ($d -lt 60 -or $d -gt 100) { throw "Recorded session length $d s is outside 60..100 s" }
    Bring-Front
    Save-Shot '05-after-session'
    Copy-Item (Join-Path $e2eDir 'library.json') (Join-Path $Out 'e2e-library.json')
    Write-Host '  PASS: playtime tracked and persisted'

    # Close button keeps the launcher alive in the tray; starting the exe again restores the same instance.
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Invoke-El (Find-El $win 'CloseButton')
    Start-Sleep -Seconds 2
    if ($proc.HasExited) { throw 'Launcher exited instead of staying in the tray' }
    if ([TestNative]::IsWindowVisible($hwnd)) { throw 'Window is still visible after closing to tray' }
    $second = Start-Process -FilePath $Exe -ArgumentList @('--data', "`"$e2eDir`"") -PassThru
    if (-not $second.WaitForExit(15000)) { throw 'Second instance did not hand over and exit' }
    Start-Sleep -Seconds 2
    if (-not [TestNative]::IsWindowVisible($hwnd)) { throw 'Existing window was not restored by the second launch' }
    Write-Host '  PASS: close-to-tray and single-instance restore'
    Stop-Launcher

    # ---------------- Part 2: seeded demo screenshots ----------------
    Write-Host '== Part 2: demo screenshots'
    Remove-Item -Recurse -Force $demoDir -ErrorAction SilentlyContinue
    & (Join-Path $PSScriptRoot 'seed-demo.ps1') -DataDir $demoDir
    Start-Launcher $demoDir
    Bring-Front
    Start-Sleep -Seconds 2
    Scroll-Top
    Save-Shot '10-library'

    $cards = $win.FindAll($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
    $firstCard = $null
    foreach ($c in $cards) { if ($c.Current.AutomationId -like 'Card_*') { $firstCard = $c; break } }
    if ($firstCard) { Hover $firstCard; Save-Shot '11-card-hover'; Park-Mouse }

    Invoke-El (Find-El $win 'HeroDetails')
    Start-Sleep -Seconds 1
    Save-Shot '12-details-drawer'
    Invoke-El (Find-El $win 'DrawerClose')
    Start-Sleep -Milliseconds 600

    $before = @(Get-Process notepad -ErrorAction SilentlyContinue | ForEach-Object Id)
    Invoke-El (Find-El $win 'Play_Notepad')
    Start-Sleep -Seconds 8
    Bring-Front
    Scroll-Top
    Save-Shot '13-running-hero'

    Select-El (Find-El $win 'NavStats')
    Start-Sleep -Seconds 1
    Save-Shot '14-statistics'

    Select-El (Find-El $win 'NavSettings')
    Start-Sleep -Seconds 1
    Save-Shot '15-settings'

    Select-El (Find-El $win 'NavLibrary')
    Invoke-El (Find-El $win 'LanguageToggle')
    Start-Sleep -Seconds 1
    Scroll-Top
    Save-Shot '16-library-ru'

    Select-El (Find-El $win 'ViewList')
    Start-Sleep -Seconds 1
    Save-Shot '17-list-ru'

    Select-El (Find-El $win 'NavStats')
    Start-Sleep -Seconds 1
    Save-Shot '18-statistics-ru'

    Select-El (Find-El $win 'NavLibrary')
    Select-El (Find-El $win 'ViewGrid')
    Invoke-El (Find-El $win 'AddButton')
    Start-Sleep -Seconds 1
    Save-Shot '19-add-dialog-ru'
    Invoke-El (Find-El $win 'EditorCancel')

    # Resource use while tracking one running app (scans every 2 s).
    $proc.Refresh()
    $cpu0 = $proc.TotalProcessorTime.TotalMilliseconds
    Start-Sleep -Seconds 30
    $proc.Refresh()
    $cpuPct = ($proc.TotalProcessorTime.TotalMilliseconds - $cpu0) / 30000 / [Environment]::ProcessorCount * 100
    Write-Host ("  launcher CPU while tracking: {0:N2}% of total CPU, working set {1:N0} MB" -f $cpuPct, ($proc.WorkingSet64 / 1MB))

    Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id } | Stop-Process -Force
    Stop-Launcher
    Write-Host 'Done.'
}
catch {
    $failed = $true
    Write-Host "FAILED: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
    try { Save-Shot 'failure' } catch { }
}
finally {
    Stop-Launcher
    foreach ($dir in @($e2eDir, $demoDir)) {
        $log = Join-Path $dir 'logs\launcher.log'
        if (Test-Path $log) {
            $dest = Join-Path $Out ("log-" + (Split-Path $dir -Leaf) + ".txt")
            Copy-Item $log $dest
            Write-Host "---- $(Split-Path $dir -Leaf) log ----"
            Get-Content $log | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
        }
    }
}

if ($failed) { exit 1 }
