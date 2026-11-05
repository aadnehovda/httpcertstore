[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Net.Http

$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'httpcertstore.csproj'))).Project.PropertyGroup.Version
$artifactRoot = Join-Path $projectRoot 'artifacts'
$testRoot = Join-Path $artifactRoot ('smoke/' + [Guid]::NewGuid().ToString('N'))
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.UseProxy = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(3)

try {
    foreach ($variant in @('self-contained', 'framework-dependent')) {
        $portProbe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 27510)
        $portProbe.Server.ExclusiveAddressUse = $true
        try { $portProbe.Start() }
        catch { throw 'Port 27510 is already in use; no existing process will be stopped.' }
        finally { $portProbe.Stop() }

        $name = "httpcertstore-$version-win-x64-$variant"
        $packagePath = Join-Path $testRoot $name
        $archive = Join-Path $artifactRoot "$name.zip"
        $expectedHash = ((Get-Content -LiteralPath "$archive.sha256") -split ' ')[0]
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
            throw "Checksum mismatch for $name."
        }
        Expand-Archive -LiteralPath $archive -DestinationPath $packagePath

        $executableVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $packagePath 'httpcertstore.exe'))
        if ($executableVersion.FileVersion -ne "$version.0" -or
            ($executableVersion.ProductVersion -split '\+')[0] -ne $version) {
            throw "Incorrect executable version in $name."
        }

        # Test both bundled executables alone: neither needs application DLLs
        # or sidecar configuration. Framework-dependent still uses the host runtime.
        $runPath = Join-Path $packagePath 'standalone'
        New-Item -ItemType Directory -Path $runPath | Out-Null
        Copy-Item -LiteralPath (Join-Path $packagePath 'httpcertstore.exe') -Destination $runPath

        $process = Start-Process -FilePath (Join-Path $runPath 'httpcertstore.exe') `
            -WorkingDirectory $runPath -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $packagePath 'stdout.log') `
            -RedirectStandardError (Join-Path $packagePath 'stderr.log')
        try {
            $ready = $false
            for ($attempt = 0; $attempt -lt 120; $attempt++) {
                if ($process.HasExited) { throw "$name exited before it was ready; see $packagePath logs." }
                $startupLog = Get-Content -LiteralPath (Join-Path $packagePath 'stdout.log') -Raw
                if ($startupLog -match 'Now listening on: http://127\.0\.0\.1:27510') {
                    $ready = $true
                    break
                }
                Start-Sleep -Milliseconds 250
            }
            if (-not $ready) { throw "$name did not start in time." }

            $missingHostnamePath = '/CurrentUser/My?server_name=httpcertstore-smoke-' + [Guid]::NewGuid().ToString('N') + '.invalid'
            foreach ($path in @('/', '/CurrentUser/My', '/CurrentUser/My/0000000000000000000000000000000000000000', $missingHostnamePath)) {
                $response = $client.GetAsync("http://127.0.0.1:27510$path").GetAwaiter().GetResult()
                try {
                    $expected = if ($path -eq '/' -or $path -eq '/CurrentUser/My') { 200 } else { 404 }
                    if ([int]$response.StatusCode -ne $expected) { throw "$name returned $($response.StatusCode) for $path." }
                    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    if ($path -eq '/' -and $body -notmatch 'Caddy compatible certificate manager') { throw 'Unexpected landing page.' }
                    if ($path -eq '/CurrentUser/My') { $null = ConvertFrom-Json -InputObject $body }
                }
                finally { $response.Dispose() }
            }

            Write-Output "Passed ${name}: executable version, startup on loopback, certificate listing, missing certificate and hostname."
        }
        finally {
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            $process.Dispose()
        }
    }
}
finally { $client.Dispose() }
