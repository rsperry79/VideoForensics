' Runs a PowerShell script hidden and waits for it, so a Visual Studio build shows no console window. The exit code of the
' script is returned, so a failed step fails the build. Output goes to the log file, which is the last argument.
' Usage: wscript //B //Nologo stage-debug-build.vbs <script.ps1> <project.csproj> <Configuration> <log file>
Option Explicit

Dim args, command, shell, exitCode
Set args = WScript.Arguments
If args.Count <> 4 Then WScript.Quit 2

command = "powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File """ & args(0) & """ -ProjectPath """ & args(1) & """ -Configuration " & args(2) & " > """ & args(3) & """ 2>&1"

Set shell = CreateObject("WScript.Shell")
exitCode = shell.Run("cmd.exe /c """ & command & """", 0, True)
WScript.Quit exitCode
