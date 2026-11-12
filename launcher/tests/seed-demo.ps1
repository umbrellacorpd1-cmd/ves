<#
  Seeds a demo library (apps that exist on this machine + ~3 weeks of synthetic sessions)
  so screenshots show a populated launcher. Used by CI only.
#>
param([Parameter(Mandatory = $true)][string]$DataDir)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $DataDir | Out-Null

$candidates = @(
    @{ name = 'Google Chrome';  hours = 46; paths = @('C:\Program Files\Google\Chrome\Application\chrome.exe') },
    @{ name = 'Visual Studio';  hours = 31; paths = @(Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\Common7\IDE\devenv.exe' -ErrorAction SilentlyContinue | ForEach-Object FullName) },
    @{ name = 'Firefox';        hours = 19; paths = @('C:\Program Files\Mozilla Firefox\firefox.exe') },
    @{ name = 'Microsoft Edge'; hours = 11; paths = @('C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe', 'C:\Program Files\Microsoft\Edge\Application\msedge.exe') },
    @{ name = 'PowerShell 7';   hours = 8;  paths = @('C:\Program Files\PowerShell\7\pwsh.exe') },
    @{ name = 'Git Bash';       hours = 5;  paths = @('C:\Program Files\Git\git-bash.exe') },
    @{ name = 'Notepad';        hours = 3;  paths = @('C:\Windows\System32\notepad.exe') },
    @{ name = '7-Zip';          hours = 1;  paths = @('C:\Program Files\7-Zip\7zFM.exe') },
    @{ name = 'Registry Editor'; hours = 0; paths = @('C:\Windows\regedit.exe') }
)

$rand = New-Object System.Random 42
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$apps = New-Object System.Collections.ArrayList
$i = 0
foreach ($c in $candidates) {
    $path = $c.paths | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $path) { Write-Host "skip $($c.name) (not installed)"; continue }

    $sessions = New-Object System.Collections.ArrayList
    $remaining = [double]$c.hours * 3600
    while ($remaining -gt 600) {
        $len = [Math]::Min($remaining, 1200 + $rand.Next(0, 9000))
        $daysAgo = $rand.Next(0, 21)
        $start = $now - ($daysAgo * 86400) - $rand.Next(3600, 50000)
        [void]$sessions.Add([ordered]@{ s = [long]$start; e = [long]($start + $len + 120); d = [int]$len; x = ($rand.Next(0, 4) -eq 0) })
        $remaining -= $len
    }
    $last = if ($sessions.Count -gt 0) { ($sessions | ForEach-Object { $_.e } | Measure-Object -Maximum).Maximum } else { 0 }
    if ($i -eq 0) { $last = $now - 5400 }

    [void]$apps.Add([ordered]@{
        id         = [Guid]::NewGuid().ToString('N')
        name       = $c.name
        path       = $path
        args       = ''
        workdir    = ''
        admin      = ($c.name -eq 'Registry Editor')
        extra      = ''
        cover      = ''
        added      = [long]($now - 40 * 86400 + $i * 3600)
        lastPlayed = [long]$last
        launches   = [int]$sessions.Count
        sessions   = @($sessions)
    })
    $i++
}

$library = [ordered]@{
    version  = 1
    settings = [ordered]@{
        language         = 'en'
        accent           = '#E81C5A'
        title            = 'Umbra'
        minimizeOnLaunch = $false
        closeToTray      = $true
        trackExternal    = $true
        focusOnly        = $false
        sort             = 'recent'
        view             = 'grid'
        statsRange       = 14
        trayHintShown    = $true
    }
    apps     = @($apps)
}

$json = ConvertTo-Json -InputObject $library -Depth 8
[IO.File]::WriteAllText((Join-Path $DataDir 'library.json'), $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Seeded $($apps.Count) apps into $DataDir"
