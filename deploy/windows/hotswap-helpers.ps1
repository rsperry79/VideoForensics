# Helpers for hotswap-service.ps1. Kept separate so the decision logic can be tested without running the swap.

<#
.SYNOPSIS
    Returns the binPath the service needs when it still runs a different executable, or $null when it is already right.

.DESCRIPTION
    The installed service may still point at an older executable name (VideoForensics.WebApp.exe) after the
    rename to VideoForensics.Client.WebApp.exe. Copying the new files does not change what the service runs, so the
    hot-swap repoints it. Paths compare case-insensitively and ignore surrounding quotes.
#>
function Get-ServiceBinPathRepoint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$CurrentPathName,
        [Parameter(Mandatory)][string]$ExpectedExePath
    )

    $current = $CurrentPathName.Trim().Trim('"')
    if ($current -ieq $ExpectedExePath) {
        return $null
    }

    return $ExpectedExePath
}

<#
.SYNOPSIS
    Works out which process listening on a port is safe to stop during a hot-swap.

.DESCRIPTION
    A process is safe to stop only when its executable runs from one of AllowedRoots (the installed
    binaries, the publish output, or this repo's WebApp build). Any other listener is reported as
    Foreign and left alone, so the swap never kills an unrelated program that happens to use the port.
    The root match requires a trailing separator, so C:\Program Files\VideoForensicsOld does not match
    C:\Program Files\VideoForensics.
#>
function Get-HotswapPortOwners {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][string[]]$AllowedRoots
    )

    $ownerIds = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { $_.OwningProcess } |
        Sort-Object -Unique)

    $toStop = [System.Collections.Generic.List[int]]::new()
    $foreign = [System.Collections.Generic.List[object]]::new()

    foreach ($ownerId in $ownerIds) {
        $process = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
        if (-not $process) {
            continue
        }

        $path = $process.Path
        $allowed = $path -and ($AllowedRoots | Where-Object {
            $path.StartsWith($_.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)
        })

        if ($allowed) {
            $toStop.Add($process.Id)
        } else {
            $foreign.Add([pscustomobject]@{ Id = $process.Id; Path = $path })
        }
    }

    [pscustomobject]@{
        ToStop = $toStop.ToArray()
        Foreign = $foreign.ToArray()
    }
}
