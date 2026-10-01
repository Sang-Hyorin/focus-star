param([string]$CompilerPath = $env:FOCUSSTAR_CSC, [switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDir = Join-Path $projectRoot 'dist/windows'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
if (-not $CompilerPath) {
    $sdk = & dotnet --list-sdks | Select-Object -Last 1
    if (-not $sdk) { throw 'Install a .NET SDK, or pass -CompilerPath pointing to Roslyn csc.exe.' }
    $CompilerPath = Join-Path $env:ProgramFiles "dotnet/sdk/$($sdk.Split(' ')[0])/Roslyn/bincore/csc.dll"
}
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$references = @('mscorlib','System','System.Core','System.Xml','System.Drawing','System.Windows.Forms','System.Runtime.Serialization','System.IO.Compression','System.IO.Compression.FileSystem') | ForEach-Object { '/r:' + (Join-Path $framework "$_.dll") }
$sources = @('LocalActivityRecorder.cs','CrossDayArchive.cs') | ForEach-Object { Join-Path $projectRoot "windows-source/$_" }
function Compile([string[]]$Arguments) {
    if ($CompilerPath.EndsWith('.dll')) { & dotnet $CompilerPath @Arguments }
    else { & $CompilerPath @Arguments }
    if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
}
Compile (@('/nologo','/optimize+','/debug-','/nostdlib+','/target:winexe','/main:FocusStar.Program',"/win32icon:$projectRoot/assets/FocusStar.ico","/out:$outputDir/LocalActivityRecorder.exe") + $references + $sources)
Copy-Item -LiteralPath "$projectRoot/assets/FocusStar.ico" -Destination $outputDir
Copy-Item -LiteralPath "$projectRoot/settings.example.json" -Destination "$outputDir/settings.json"
Copy-Item -LiteralPath "$projectRoot/README.md" -Destination $outputDir
Copy-Item -LiteralPath "$projectRoot/LICENSE" -Destination $outputDir
if ($Test) {
    $testExe = Join-Path $projectRoot 'dist/ArchiveTests.exe'
    Compile (@('/nologo','/optimize+','/debug-','/nostdlib+','/target:exe','/main:FocusStar.ArchiveTests',"/out:$testExe") + $references + $sources + @("$projectRoot/windows-source/ArchiveTests.cs"))
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Archive tests failed.' }
}
Compress-Archive -Path "$outputDir/*" -DestinationPath "$projectRoot/dist/FocusStar-Windows.zip" -Force
