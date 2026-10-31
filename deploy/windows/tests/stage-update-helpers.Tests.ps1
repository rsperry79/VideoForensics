#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot '..\stage-update-helpers.ps1')
}

Describe 'New-UpdateManifest' {
    BeforeEach {
        $script:dir = Join-Path ([IO.Path]::GetTempPath()) "vf-manifest-$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path (Join-Path $script:dir 'lib') -Force | Out-Null
        Set-Content -Path (Join-Path $script:dir 'app.exe') -Value 'binary-one' -NoNewline
        Set-Content -Path (Join-Path $script:dir 'lib\core.dll') -Value 'binary-two' -NoNewline
    }

    AfterEach {
        Remove-Item -Path $script:dir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'lists every file with a forward-slash relative path, lowercase sha256 and size' {
        $manifest = New-UpdateManifest -PackageDir $script:dir -Version '2.0.0' | ConvertFrom-Json

        $manifest.version | Should -Be '2.0.0'
        $manifest.files.path | Should -Be @('app.exe', 'lib/core.dll')
        $manifest.files[0].size | Should -Be 10
        $manifest.files[0].sha256 | Should -Be ((Get-FileHash -Algorithm SHA256 -Path (Join-Path $script:dir 'app.exe')).Hash.ToLowerInvariant())
    }

    It 'leaves an existing manifest.json out of its own file list' {
        Set-Content -Path (Join-Path $script:dir 'manifest.json') -Value '{}'

        $manifest = New-UpdateManifest -PackageDir $script:dir -Version '2.0.0' | ConvertFrom-Json

        $manifest.files.path | Should -Not -Contain 'manifest.json'
    }

    It 'keeps files as an array when the package has a single file' {
        Remove-Item -Path (Join-Path $script:dir 'lib') -Recurse -Force

        $json = New-UpdateManifest -PackageDir $script:dir -Version '2.0.0'

        $json | Should -Match '"files":\s*\['
    }
}

Describe 'Test-UpdateVersionName' {
    It 'accepts <Version>' -ForEach @(
        @{ Version = '1.1.0' }, @{ Version = '1.1.0-alpha.5+abc123' }
    ) {
        Test-UpdateVersionName $Version | Should -BeTrue
    }

    It 'rejects <Version>' -ForEach @(
        @{ Version = '..\evil' }, @{ Version = '1.0 0' }, @{ Version = '-1.0' }
    ) {
        Test-UpdateVersionName $Version | Should -BeFalse
    }
}
