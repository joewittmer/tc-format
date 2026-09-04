[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string] $Version = '1.0.1.0',

    [Parameter()]
    [string] $PublishDirectory,

    [Parameter()]
    [string] $OutputDirectory,

    [Parameter()]
    [string] $ExtensionPackage
)

$ErrorActionPreference = 'Stop'

$repositoryDirectory = Split-Path -Parent $PSScriptRoot
$publishDirectoryProvided = -not [string]::IsNullOrWhiteSpace($PublishDirectory)
if (-not $PublishDirectory) {
    $PublishDirectory = Join-Path $repositoryDirectory 'artifacts\publish\win-x64'
}
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryDirectory 'artifacts\installer'
}

$publishDirectoryPath = [System.IO.Path]::GetFullPath($PublishDirectory)
$outputDirectoryPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$publishedExecutable = Join-Path $publishDirectoryPath 'tc_format.exe'
if (-not $publishDirectoryProvided) {
    $cliProject = Join-Path $repositoryDirectory 'src\TcFormat.Cli\TcFormat.Cli.csproj'
    & dotnet publish $cliProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained `
        -p:Version=$Version `
        --output $publishDirectoryPath
    if ($LASTEXITCODE -ne 0) {
        throw "CLI publish failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Published executable not found: $publishedExecutable"
}

if (-not $ExtensionPackage) {
    $extensionProject = Join-Path $repositoryDirectory 'src\TcFormat.Xae\TcFormat.Xae.csproj'
    & dotnet build $extensionProject `
        --configuration Release `
        -p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        throw "XAE extension build failed with exit code $LASTEXITCODE."
    }

    $ExtensionPackage = Join-Path $repositoryDirectory 'src\TcFormat.Xae\bin\Release\net472\TcFormat.Xae.vsix'
}

$extensionPackagePath = [System.IO.Path]::GetFullPath($ExtensionPackage)
if (-not (Test-Path -LiteralPath $extensionPackagePath -PathType Leaf)) {
    throw "XAE extension package not found: $extensionPackagePath"
}

$extensionContentDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryDirectory 'artifacts\extension\TcFormat.Xae'))
$artifactsDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryDirectory 'artifacts'))
if (-not $extensionContentDirectory.StartsWith(
        $artifactsDirectory + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to prepare extension files outside the artifacts directory: $extensionContentDirectory"
}

if (Test-Path -LiteralPath $extensionContentDirectory) {
    Remove-Item -LiteralPath $extensionContentDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $extensionContentDirectory | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory(
    $extensionPackagePath,
    $extensionContentDirectory)

New-Item -ItemType Directory -Force -Path $outputDirectoryPath | Out-Null

$compilerCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
$compilerCandidates = @(
    if ($compilerCommand) { $compilerCommand.Source }
    Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'
    Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'
    Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
)
$compiler = $compilerCandidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
    Select-Object -First 1
if (-not $compiler) {
    throw 'ISCC.exe was not found. Install Inno Setup 6 or 7 and try again.'
}

$scriptPath = Join-Path $PSScriptRoot 'tc_format.iss'
$versionCore = ($Version -split '[-+]')[0]
$versionInfo = if (($versionCore -split '\.').Count -eq 3) { "$versionCore.0" } else { $versionCore }
& $compiler `
    "/DMyAppVersion=$Version" `
    "/DMyAppVersionInfo=$versionInfo" `
    "/DMyPublishDir=$publishDirectoryPath" `
    "/DMyVsixPath=$extensionPackagePath" `
    "/DMyVsixContentDir=$extensionContentDirectory" `
    "/DMyOutputDir=$outputDirectoryPath" `
    $scriptPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}
