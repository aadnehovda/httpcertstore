[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'httpcertstore.csproj'))).Project.PropertyGroup.Version
$artifactRoot = Join-Path $projectRoot 'artifacts'
# Always publish into a fresh directory so stale files cannot enter a package.
$stagingRoot = Join-Path $artifactRoot ('staging/' + [Guid]::NewGuid().ToString('N'))
$variants = @(
    @{ Name = 'self-contained'; Profile = 'WindowsSelfContained'; Files = @('httpcertstore.exe', 'appsettings.json') },
    @{ Name = 'framework-dependent'; Profile = 'WindowsFrameworkDependent'; Files = @('httpcertstore.exe', 'appsettings.json') }
)

Push-Location $projectRoot
try {
    foreach ($variant in $variants) {
        $packageName = "httpcertstore-$version-win-x64-" + $variant.Name
        $publishPath = Join-Path $stagingRoot $packageName
        & dotnet publish httpcertstore.csproj --configuration Release `
            "-p:PublishProfile=$($variant.Profile)" --output $publishPath
        if ($LASTEXITCODE -ne 0) { throw "Publishing $packageName failed." }

        if ($variant.Name -eq 'framework-dependent') {
            # The runtime config is embedded in the single-file EXE. Validate the
            # generated input used by the bundler, rather than shipping a sidecar.
            $runtimeConfigPath = & dotnet msbuild httpcertstore.csproj -nologo `
                -p:Configuration=Release "-p:PublishProfile=$($variant.Profile)" `
                -getProperty:ProjectRuntimeConfigFilePath
            if ($LASTEXITCODE -ne 0) { throw 'Could not locate the generated runtime configuration.' }
            $config = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
            $frameworks = @($config.runtimeOptions.frameworks)
            $baseline = ([xml](Get-Content -LiteralPath 'httpcertstore.csproj')).Project.PropertyGroup.RuntimeFrameworkVersion
            if ($config.runtimeOptions.rollForward -ne 'LatestPatch' -or
                $frameworks.Count -ne 2 -or
                @($frameworks | Where-Object { $_.version -ne $baseline }).Count -ne 0 -or
                'Microsoft.NETCore.App' -notin $frameworks.name -or
                'Microsoft.AspNetCore.App' -notin $frameworks.name) {
                throw 'The framework-dependent package does not enforce the expected runtime baseline.'
            }
        }

        $actualFiles = @(Get-ChildItem -LiteralPath $publishPath -Recurse -File)
        $differences = Compare-Object ($variant.Files | Sort-Object) `
            ($actualFiles | ForEach-Object { $_.FullName.Substring($publishPath.Length + 1) } | Sort-Object)
        if ($differences) { throw "Unexpected package contents for ${packageName}: $($differences | Out-String)" }

        Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'LICENSE.txt') -Destination $publishPath
        $archive = Join-Path $artifactRoot "$packageName.zip"
        Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $archive -Force
        $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        Set-Content -LiteralPath "$archive.sha256" -Value "$hash  $packageName.zip" -Encoding ascii
        Write-Output "Packaged $archive"
    }
}
finally {
    Pop-Location
}
