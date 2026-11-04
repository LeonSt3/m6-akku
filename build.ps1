$ErrorActionPreference = 'Stop'
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskCompiler = Join-Path $taskFramework 'csc.exe'
$taskMetadata = Join-Path $env:WINDIR 'System32\WinMetadata'
$taskRuntime = Get-ChildItem (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime') -Recurse -Filter System.Runtime.dll | Select-Object -First 1 -ExpandProperty FullName
$taskReferences = @('System.Windows.Forms.dll','System.Drawing.dll','System.Xml.dll')
$taskReferences += Join-Path $taskFramework 'System.Runtime.WindowsRuntime.dll'
$taskReferences += Join-Path $taskFramework 'System.Runtime.InteropServices.WindowsRuntime.dll'
$taskReferences += $taskRuntime
$taskReferences += @('Windows.UI.winmd','Windows.Data.winmd','Windows.Foundation.winmd') | ForEach-Object { Join-Path $taskMetadata $_ }
$taskArguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/warn:4',('/out:' + (Join-Path $PSScriptRoot 'M6Akku.exe')))
$taskArguments += '/win32manifest:' + (Join-Path $PSScriptRoot 'source\app.manifest')
$taskArguments += $taskReferences | ForEach-Object { '/r:' + $_ }
$taskArguments += Get-ChildItem (Join-Path $PSScriptRoot 'source') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $taskCompiler @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Kompilierung fehlgeschlagen.' }
Write-Output (Join-Path $PSScriptRoot 'M6Akku.exe')
