<#
  End-to-end UI test for Umbra Launcher on a real Windows desktop (GitHub Actions windows runner).
  Part 1 drives the real flow through UI Automation: add notepad.exe via the dialog, launch it from its card,
  keep it running ~70 s, close it, and assert that a matching session was written to library.json.
  Part 2 uses stand-in programs to check that each process is credited to the right app and that launcher hand-offs count.
  Part 3 seeds demo data and captures screenshots of every screen.
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
$matchDir = Join-Path $env:RUNNER_TEMP 'umbra-match'
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

    # Apps started outside the launcher (desktop, Start menu) are detected and counted too.
    $before2 = @(Get-Process notepad -ErrorAction SilentlyContinue | ForEach-Object Id)
    Start-Process -FilePath 'C:\Windows\System32\notepad.exe' | Out-Null
    [void](Find-El $win 'HeroFocus' 15)
    Write-Host '  external launch picked up (banner switched to the running app)'
    Start-Sleep -Seconds 20
    Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $before2 -notcontains $_.Id } | Stop-Process -Force
    Start-Sleep -Seconds 12
    $lib = Get-Content (Join-Path $e2eDir 'library.json') -Raw | ConvertFrom-Json
    $sessions = @(($lib.apps | Where-Object { $_.name -eq 'Notepad' }).sessions)
    if ($sessions.Count -lt 2) { throw 'Notepad started outside the launcher was not recorded' }
    $ext = $sessions[-1]
    Write-Host "  external session: $($ext.d) s, detected=$($ext.x)"
    if (-not $ext.x) { throw 'External session is not marked as detected' }
    if ([int]$ext.d -lt 15 -or [int]$ext.d -gt 45) { throw "External session length $($ext.d) s is outside 15..45 s" }
    Copy-Item (Join-Path $e2eDir 'library.json') (Join-Path $Out 'e2e-library.json') -Force
    Write-Host '  PASS: app started outside the launcher was tracked'

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

    # ---------------- Part 2: which process counts for which app ----------------
    Write-Host '== Part 2: process attribution (shared folder, child hand-off, non-child hand-off)'
    $bin = Join-Path $env:RUNNER_TEMP 'umbra-bin'
    Remove-Item -Recurse -Force $matchDir, $bin -ErrorAction SilentlyContinue
    foreach ($d in 'shared', 'child', 'broker') { New-Item -ItemType Directory -Force (Join-Path $bin $d) | Out-Null }
    New-Item -ItemType Directory -Force $matchDir | Out-Null

    # Windowless stand-in for real programs: "sleep <s>" waits; "spawn <exe> <s>" starts <exe> as a child and exits.
    $dummy = Join-Path $bin 'dummy.exe'
    Add-Type -OutputAssembly $dummy -OutputType WindowsApplication -TypeDefinition @"
public static class Dummy {
    public static void Main(string[] args) {
        if (args.Length >= 3 && args[0] == "spawn") {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args[1], "sleep " + args[2]) { UseShellExecute = false });
            return;
        }
        System.Threading.Thread.Sleep((args.Length >= 2 ? int.Parse(args[1]) : 30) * 1000);
    }
}
"@
    $alpha = Join-Path $bin 'shared\alpha.exe'
    $beta = Join-Path $bin 'shared\beta.exe'
    $childStub = Join-Path $bin 'child\stub.exe'
    $childGame = Join-Path $bin 'child\game.exe'
    $brokerStub = Join-Path $bin 'broker\stub.exe'
    $brokerGame = Join-Path $bin 'broker\game.exe'
    foreach ($f in $alpha, $beta, $childStub, $childGame, $brokerStub, $brokerGame) { Copy-Item $dummy $f }

    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    function New-Entry([string]$name, [string]$path, [string]$arguments) {
        [ordered]@{ id = [Guid]::NewGuid().ToString('N'); name = $name; path = $path; args = $arguments; workdir = ''; admin = $false
                    extra = ''; cover = ''; added = [long]$now; lastPlayed = 0; launches = 0; sessions = @() }
    }
    $library = [ordered]@{
        version  = 1
        settings = [ordered]@{ language = 'en'; accent = '#E81C5A'; title = 'Umbra'; minimizeOnLaunch = $false; closeToTray = $true
                               trackExternal = $true; focusOnly = $false; sort = 'name'; view = 'grid'; statsRange = 14; trayHintShown = $true }
        apps     = @(
            (New-Entry 'Alpha' $alpha ''),
            (New-Entry 'Beta' $beta ''),
            (New-Entry 'ChildStub' $childStub "spawn `"$childGame`" 20"),
            (New-Entry 'BrokerStub' $brokerStub 'sleep 2')
        )
    }
    [IO.File]::WriteAllText((Join-Path $matchDir 'library.json'), (ConvertTo-Json -InputObject $library -Depth 6), (New-Object System.Text.UTF8Encoding($false)))

    Start-Launcher $matchDir
    $t0 = Get-Date
    Start-Process -FilePath $alpha -ArgumentList 'sleep', '20'       # outside the launcher; Beta lives in the same folder
    Invoke-El (Find-El $win 'Play_ChildStub')                          # starts game.exe as a child, then exits at once
    Invoke-El (Find-El $win 'Play_BrokerStub')                         # runs 2 s and exits...
    Start-Sleep -Seconds 4
    Start-Process -FilePath $brokerGame -ArgumentList 'sleep', '20'   # ...then another process starts the game (not a child)
    while (((Get-Date) - $t0).TotalSeconds -lt 80) { Start-Sleep -Seconds 1 }   # launched sessions stay open 60 s after launch

    Copy-Item (Join-Path $matchDir 'library.json') (Join-Path $Out 'attribution-library.json')
    $lib = Get-Content (Join-Path $matchDir 'library.json') -Raw | ConvertFrom-Json
    function Get-Sessions([string]$name) { @(($lib.apps | Where-Object { $_.name -eq $name }).sessions) }
    function Assert-OneSession([string]$name, [int]$min, [int]$max, [bool]$detected) {
        $s = Get-Sessions $name
        Write-Host ("  {0}: {1} session(s) [{2}]" -f $name, $s.Count, (($s | ForEach-Object { "$($_.d) s, detected=$($_.x)" }) -join '; '))
        if ($s.Count -ne 1) { throw "$name should have exactly one session" }
        if ([int]$s[0].d -lt $min -or [int]$s[0].d -gt $max) { throw "$name session of $($s[0].d) s is outside $min..$max s" }
        if ([bool]$s[0].x -ne $detected) { throw "$name session should have detected=$detected" }
    }
    Assert-OneSession 'Alpha' 15 30 $true
    Assert-OneSession 'ChildStub' 15 30 $false
    Assert-OneSession 'BrokerStub' 15 35 $false
    $betaSessions = Get-Sessions 'Beta'
    Write-Host "  Beta: $($betaSessions.Count) session(s)"
    if ($betaSessions.Count -ne 0) { throw 'Beta was credited with time from Alpha, which only shares its folder' }
    Stop-Launcher
    Write-Host '  PASS: each process credited to its own app; both launcher hand-offs followed'

    # ---------------- Part 3: seeded demo screenshots ----------------
    Write-Host '== Part 3: demo screenshots'
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

    # Resource use while tracking one running app (scans every 2 s): window visible, then hidden in the tray.
    function Measure-Cpu([int]$seconds) {
        $proc.Refresh()
        $t0 = $proc.TotalProcessorTime.TotalMilliseconds
        Start-Sleep -Seconds $seconds
        $proc.Refresh()
        return ($proc.TotalProcessorTime.TotalMilliseconds - $t0) / ($seconds * 1000) / [Environment]::ProcessorCount * 100
    }
    $visibleCpu = Measure-Cpu 20
    Invoke-El (Find-El $win 'CloseButton')
    Start-Sleep -Seconds 2
    $trayCpu = Measure-Cpu 30
    $proc.Refresh()
    Write-Host ("  launcher CPU while tracking: window visible {0:N2}%, hidden in tray {1:N2}% of total CPU ({2} cores); working set {3:N0} MB" -f $visibleCpu, $trayCpu, [Environment]::ProcessorCount, ($proc.WorkingSet64 / 1MB))

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
    foreach ($dir in @($e2eDir, $matchDir, $demoDir)) {
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
