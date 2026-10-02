[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    function Invoke-ReleaseCommand {
        param(
            [Parameter(Mandatory)]
            [string]$Command,

            [Parameter(Mandatory)]
            [string[]]$Arguments
        )

        Write-Host ">> $Command $($Arguments -join ' ')"
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "'$Command $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
        }
    }

    Invoke-ReleaseCommand 'dotnet' @('versionize')

    [xml]$project = Get-Content -Raw 'src\Ptu.Cli\Ptu.Cli.csproj'
    $version = $project.SelectSingleNode('//Version').InnerText.Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw 'Versionize did not produce a project version in src\Ptu.Cli\Ptu.Cli.csproj.'
    }

    Write-Host "Versionize produced version $version."

    Invoke-ReleaseCommand 'git' @('push', '--follow-tags', 'origin', 'main')
    Invoke-ReleaseCommand 'dotnet' @('pack', 'src\Ptu.Cli\Ptu.Cli.csproj', '--configuration', 'Release')
    Invoke-ReleaseCommand 'dotnet' @(
        'tool', 'update', '--global', 'sujithq.ptu.cli',
        '--version', $version,
        '--add-source', 'artifacts'
    )

    Write-Host "Release $version completed successfully."
}
finally {
    Pop-Location
}
