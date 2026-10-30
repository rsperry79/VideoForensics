<#
.SYNOPSIS
    Helpers for stage-update.ps1. Kept separate so they can be unit-tested without publishing or touching the updates folder.
#>

function New-UpdateManifest {
    <#
    .SYNOPSIS
        Builds the manifest.json text for an update package: its version and every file's relative path, SHA-256 and size.
        The shape matches UpdateManifest in VideoForensics.Client.Hosting (camelCase, '/' separators, lowercase hex hashes).
    #>
    param(
        [Parameter(Mandatory)] [string]$PackageDir,
        [Parameter(Mandatory)] [string]$Version
    )

    $root = (Resolve-Path $PackageDir).Path.TrimEnd('\')
    $files = Get-ChildItem -Path $root -Recurse -File |
        Where-Object { $_.Name -ne 'manifest.json' } |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path   = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
                sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
                size   = $_.Length
            }
        }

    return ([ordered]@{ version = $Version; files = @($files) } | ConvertTo-Json -Depth 4)
}

function Test-UpdateVersionName {
    <#
    .SYNOPSIS
        True when the version can be used as the staged folder name. Mirrors the check in UpdateStager.
    #>
    param([Parameter(Mandatory)] [string]$Version)

    return $Version -match '^[0-9A-Za-z][0-9A-Za-z.+-]*$'
}
