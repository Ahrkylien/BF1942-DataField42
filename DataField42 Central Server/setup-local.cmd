@echo off
rem Runs setup-local.ps1 for this one process with the execution policy bypassed. Some machines
rem refuse unsigned PowerShell scripts outright, and a batch file is not subject to that policy.
rem Nothing about the machine's policy is changed. Arguments are passed through.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-local.ps1" %*
exit /b %ERRORLEVEL%
