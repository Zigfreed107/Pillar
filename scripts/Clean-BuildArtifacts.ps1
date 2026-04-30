# Clean-BuildArtifacts.ps1
# Resets generated build outputs for this repository so locked WPF/XAML obj files do not block the next build.

# Use it like this from C:\Coding\Pillar:

# powershell -ExecutionPolicy Bypass -File .\scripts\Clean-BuildArtifacts.ps1
# If Visual Studio is open and you want the script to close it for you first:

# powershell -ExecutionPolicy Bypass -File .\scripts\Clean-BuildArtifacts.ps1 -CloseVisualStudio
# If you also want it to kill dotnet processes that might be holding locks:

# powershell -ExecutionPolicy Bypass -File .\scripts\Clean-BuildArtifacts.ps1 -CloseVisualStudio -StopDotNetPr


[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [switch]$CloseVisualStudio,
    [switch]$StopDotNetProcesses,
    [bool]$IncludeRepoRootArtifacts = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $repositoryRoot = Split-Path -Parent $scriptDirectory

    if (-not (Test-Path -LiteralPath (Join-Path -Path $repositoryRoot -ChildPath '.git'))) {
        throw "Could not find the repository root from '$PSCommandPath'."
    }

    return (Resolve-Path -LiteralPath $repositoryRoot).Path
}

function Stop-RepositoryBuildProcesses {
    param(
        [bool]$CloseVisualStudioProcess,
        [bool]$StopDotNetBuildProcesses
    )

    $processNames = New-Object System.Collections.Generic.List[string]
    $processNames.Add('MSBuild')
    $processNames.Add('VBCSCompiler')

    if ($CloseVisualStudioProcess) {
        $processNames.Add('devenv')
    }

    if ($StopDotNetBuildProcesses) {
        $processNames.Add('dotnet')
    }

    foreach ($processName in $processNames) {
        $runningProcesses = Get-Process -Name $processName -ErrorAction SilentlyContinue

        foreach ($runningProcess in $runningProcesses) {
            if ($PSCmdlet.ShouldProcess("process $($runningProcess.ProcessName) ($($runningProcess.Id))", 'Stop-Process')) {
                Stop-Process -Id $runningProcess.Id -Force
            }
        }
    }
}

function Get-BuildArtifactDirectories {
    param(
        [string]$RepositoryRootPath,
        [bool]$IncludeRootArtifacts
    )

    $directories = New-Object System.Collections.Generic.List[string]
    $searchRoots = New-Object System.Collections.Generic.List[string]
    $searchRoots.Add((Join-Path -Path $RepositoryRootPath -ChildPath 'src'))

    if ($IncludeRootArtifacts) {
        $searchRoots.Add($RepositoryRootPath)
    }

    foreach ($searchRoot in $searchRoots) {
        if (-not (Test-Path -LiteralPath $searchRoot)) {
            continue
        }

        $foundDirectories = Get-ChildItem -Path $searchRoot -Directory -Recurse -Force |
            Where-Object { $_.Name -in @('bin', 'obj') }

        foreach ($foundDirectory in $foundDirectories) {
            $resolvedPath = (Resolve-Path -LiteralPath $foundDirectory.FullName).Path

            if (-not $resolvedPath.StartsWith($RepositoryRootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to delete '$resolvedPath' because it is outside the repository root."
            }

            if (-not $directories.Contains($resolvedPath)) {
                $directories.Add($resolvedPath)
            }
        }
    }

    return $directories
}

function Remove-BuildArtifactDirectories {
    param(
        [System.Collections.Generic.List[string]]$DirectoriesToRemove
    )

    foreach ($directory in $DirectoriesToRemove) {
        if (-not (Test-Path -LiteralPath $directory)) {
            continue
        }

        if ($PSCmdlet.ShouldProcess($directory, 'Remove-Item -Recurse -Force')) {
            Remove-Item -LiteralPath $directory -Recurse -Force
        }
    }
}

$repositoryRoot = Get-RepositoryRoot

$visualStudioProcess = Get-Process -Name 'devenv' -ErrorAction SilentlyContinue
if ($visualStudioProcess -and -not $CloseVisualStudio) {
    throw "Visual Studio is running. Close it first, or rerun with -CloseVisualStudio."
}

Stop-RepositoryBuildProcesses -CloseVisualStudioProcess:$CloseVisualStudio -StopDotNetBuildProcesses:$StopDotNetProcesses

$artifactDirectories = Get-BuildArtifactDirectories -RepositoryRootPath $repositoryRoot -IncludeRootArtifacts:$IncludeRepoRootArtifacts

if ($artifactDirectories.Count -eq 0) {
    Write-Host "No build artifact directories were found under '$repositoryRoot'."
    return
}

Write-Host "Removing build artifacts from the following directories:"
foreach ($artifactDirectory in $artifactDirectories) {
    Write-Host " - $artifactDirectory"
}

Remove-BuildArtifactDirectories -DirectoriesToRemove $artifactDirectories
Write-Host 'Build artifacts cleaned successfully.'
