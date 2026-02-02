@echo off
chcp 65001 >nul
set "ROOT=%~dp0.."
cd /d "%ROOT%"

if not exist ".skills" (
  echo .skills not found
  exit /b 1
)

if not exist ".cursor" mkdir ".cursor"
if exist ".cursor\skills" rmdir ".cursor\skills" 2>nul
mklink /J ".cursor\skills" "%ROOT%\.skills"
if errorlevel 1 echo Failed to create junction .cursor\skills

if not exist ".opencode" mkdir ".opencode"
if exist ".opencode\skills" rmdir ".opencode\skills" 2>nul
mklink /J ".opencode\skills" "%ROOT%\.skills"
if errorlevel 1 echo Failed to create junction .opencode\skills

echo .cursor\skills and .opencode\skills linked to .skills
exit /b 0
