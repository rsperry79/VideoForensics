#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

# Static checks on the Inno Setup script and the release pipeline. The installer cannot be compiled
# in unit tests, so these assert the wiring that lets each optional utility be selected at install time.

BeforeAll {
    $script:iss = Get-Content -Raw -Path (Join-Path $PSScriptRoot '..\VideoForensics.iss')
    $script:buildWindows = Get-Content -Raw -Path (Join-Path $PSScriptRoot '..\..\..\.github\workflows\build-windows.yml')
    $script:release = Get-Content -Raw -Path (Join-Path $PSScriptRoot '..\..\..\.github\workflows\release-installers.yml')
}

Describe 'Optional Tools installer wiring' {
    It 'defines the LogViewer publish dir and an existence guard for its exe' {
        $script:iss | Should -Match '#ifndef LogViewerPublishDir'
        $script:iss | Should -Match '#define LogViewerExeExists FileExists\(LogViewerPublishDir \+ "\\VideoForensics\.Utils\.LogViewer\.exe"\)'
    }

    It 'offers the tools component when any optional utility is present' {
        $script:iss | Should -Match '#if DbSetupExeExists \|\| DbDiagnosticsExeExists \|\| DbRepairExeExists \|\| LogViewerExeExists\r?\nName: "tools"'
    }

    It 'copies LogViewer into its own Tools subfolder under the tools component' {
        $script:iss | Should -Match 'Source: "\{#LogViewerPublishDir\}\\\*"; DestDir: "\{app\}\\Tools\\LogViewer"; Components: tools'
    }

    It 'adds a LogViewer Start Menu entry under Optional Tools' {
        $script:iss | Should -Match 'Name: "\{group\}\\Optional Tools\\Log Viewer"; Filename: "\{app\}\\Tools\\LogViewer\\VideoForensics\.Utils\.LogViewer\.exe"; Components: tools'
    }

    It 'adds LogViewer to the Optional Tools PATH on install and removes it on uninstall' {
        $script:iss | Should -Match "AddDirToPath\(ExpandConstant\('\{app\}\\Tools\\LogViewer'\)\)"
        $script:iss | Should -Match "RemoveDirFromPath\(ExpandConstant\('\{app\}\\Tools\\LogViewer'\)\)"
    }

    It 'ships the tools README whenever any optional utility is present' {
        $script:iss | Should -Match '#if DbSetupExeExists \|\| DbDiagnosticsExeExists \|\| DbRepairExeExists \|\| LogViewerExeExists\r?\nSource: "ToolsReadme\.txt"'
    }
}

Describe 'Optional Tools release pipeline' {
    It 'publishes LogViewer, DbDiagnostics and DbRepair in build-windows' {
        $script:buildWindows | Should -Match '-o publish-logviewer'
        $script:buildWindows | Should -Match '-o publish-dbdiagnostics'
        $script:buildWindows | Should -Match '-o publish-dbrepair'
    }

    It 'archives every optional utility publish dir in the Windows publish tar' {
        $script:buildWindows | Should -Match 'tar -cf windows-publish\.tar publish-win publish-desktop publish-dbsetup publish-dbdiagnostics publish-dbrepair publish-logviewer'
    }

    It 'passes every optional utility publish dir to ISCC' {
        $script:release | Should -Match '/DLogViewerPublishDir='
        $script:release | Should -Match '/DDbDiagnosticsPublishDir='
        $script:release | Should -Match '/DDbRepairPublishDir='
    }
}
