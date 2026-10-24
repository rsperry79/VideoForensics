#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot '..\hotswap-helpers.ps1')

    $script:Installed = 'C:\Program Files\VideoForensics'
    $script:Publish = 'C:\Repo\src\client\web\VideoForensics.WebApp\bin\Release\net10.0\win-x64\publish'
    $script:Roots = @($script:Installed, $script:Publish)
}

Describe 'Get-ServiceBinPathRepoint' {
    BeforeAll {
        $script:NewExe = 'C:\Program Files\VideoForensics\VideoForensics.Client.WebApp.exe'
    }

    It 'returns the new binPath when the service still runs the old executable name' {
        $result = Get-ServiceBinPathRepoint -CurrentPathName 'C:\Program Files\VideoForensics\VideoForensics.WebApp.exe' -ExpectedExePath $script:NewExe

        $result | Should -Be $script:NewExe
    }

    It 'returns nothing when the service already runs the expected executable' {
        $result = Get-ServiceBinPathRepoint -CurrentPathName '"C:\Program Files\VideoForensics\VideoForensics.Client.WebApp.exe"' -ExpectedExePath $script:NewExe

        $result | Should -BeNullOrEmpty
    }

    It 'ignores case and surrounding quotes when comparing paths' {
        $result = Get-ServiceBinPathRepoint -CurrentPathName '"c:\program files\videoforensics\videoforensics.client.webapp.exe"' -ExpectedExePath $script:NewExe

        $result | Should -BeNullOrEmpty
    }
}

Describe 'Get-HotswapPortOwners' {
    It 'returns nothing when no process listens on the port' {
        Mock Get-NetTCPConnection { @() }

        $plan = Get-HotswapPortOwners -Port 5162 -AllowedRoots $script:Roots

        $plan.ToStop | Should -BeNullOrEmpty
        $plan.Foreign | Should -BeNullOrEmpty
    }

    It 'marks the owner for stopping when it runs from the installed binaries' {
        Mock Get-NetTCPConnection { [pscustomobject]@{ OwningProcess = 100 } }
        Mock Get-Process { [pscustomobject]@{ Id = 100; Path = "$script:Installed\VideoForensics.WebApp.exe" } }

        $plan = Get-HotswapPortOwners -Port 5162 -AllowedRoots $script:Roots

        $plan.ToStop | Should -Be @(100)
        $plan.Foreign | Should -BeNullOrEmpty
    }

    It 'marks the owner for stopping when it runs from the publish output' {
        Mock Get-NetTCPConnection { [pscustomobject]@{ OwningProcess = 101 } }
        Mock Get-Process { [pscustomobject]@{ Id = 101; Path = "$script:Publish\VideoForensics.WebApp.exe" } }

        $plan = Get-HotswapPortOwners -Port 5162 -AllowedRoots $script:Roots

        $plan.ToStop | Should -Be @(101)
    }

    It 'reports an owner from anywhere else as foreign and does not mark it for stopping' {
        Mock Get-NetTCPConnection { [pscustomobject]@{ OwningProcess = 102 } }
        Mock Get-Process { [pscustomobject]@{ Id = 102; Path = 'F:\Other\VideoForensics.WebApp.exe' } }

        $plan = Get-HotswapPortOwners -Port 5162 -AllowedRoots $script:Roots

        $plan.ToStop | Should -BeNullOrEmpty
        $plan.Foreign.Id | Should -Be @(102)
        $plan.Foreign.Path | Should -Be @('F:\Other\VideoForensics.WebApp.exe')
    }

    It 'does not match a sibling folder that only shares the install path as a prefix' {
        Mock Get-NetTCPConnection { [pscustomobject]@{ OwningProcess = 103 } }
        Mock Get-Process { [pscustomobject]@{ Id = 103; Path = 'C:\Program Files\VideoForensicsOld\VideoForensics.WebApp.exe' } }

        $plan = Get-HotswapPortOwners -Port 5162 -AllowedRoots $script:Roots

        $plan.ToStop | Should -BeNullOrEmpty
        $plan.Foreign.Id | Should -Be @(103)
    }

    It 'treats a listener whose process has already exited as nothing to stop' {
        Mock Get-NetTCPConnection { [pscustomobject]@{ OwningProcess = 104 } }
        Mock Get-Process { $null }

        $plan = Get-HotswapPortOwners -Port 5162 -AllowedRoots $script:Roots

        $plan.ToStop | Should -BeNullOrEmpty
        $plan.Foreign | Should -BeNullOrEmpty
    }

    It 'lists each owning process once when the port has several listeners' {
        Mock Get-NetTCPConnection {
            [pscustomobject]@{ OwningProcess = 100 }
            [pscustomobject]@{ OwningProcess = 100 }
        }
        Mock Get-Process { [pscustomobject]@{ Id = 100; Path = "$script:Installed\VideoForensics.WebApp.exe" } }

        $plan = Get-HotswapPortOwners -Port 5162 -AllowedRoots $script:Roots

        $plan.ToStop | Should -Be @(100)
    }
}
