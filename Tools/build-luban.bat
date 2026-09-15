@echo off
setlocal enabledelayedexpansion
REM ============================================================================
REM build-luban.bat - Rebuild the Luban AI toolchain into Tools/
REM
REM Builds three self-contained outputs from the Luban upstream sources:
REM   Tools/Luban/       main generator (schema-json, --errorFormat json)
REM   Tools/Luban.Agent/ agent CLI (validate / schema / list-tables / describe)
REM   Tools/Luban.Mcp/   MCP server (stdio)
REM
REM Usage: build-luban.bat [luban-source-dir]
REM        default source dir: D:\Work\CSharp\luban
REM
REM Requires: .NET SDK 8 or later.
REM
REM NOTE: this file is intentionally pure ASCII. cmd.exe reads .bat files using
REM the OEM code page (936/GBK on this machine), so non-ASCII comments here
REM would be decoded as garbage and break parsing. Keep it ASCII-only.
REM ============================================================================

set "LUBAN_SRC=%~1"
if "%LUBAN_SRC%"=="" set "LUBAN_SRC=D:\Work\CSharp\luban"
set "OUT=%~dp0"
if "%OUT:~-1%"=="\" set "OUT=%OUT:~0,-1%"

echo [build-luban] source: %LUBAN_SRC%
echo [build-luban] output: %OUT%
echo.

REM --- 1. preconditions ------------------------------------------------------
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [build-luban] ERROR: dotnet not found on PATH. Install .NET SDK 8 or later.
    exit /b 1
)

for %%P in (Luban Luban.Agent Luban.Mcp) do (
    if not exist "%LUBAN_SRC%\src\%%P\%%P.csproj" (
        echo [build-luban] ERROR: project not found: %LUBAN_SRC%\src\%%P\%%P.csproj
        exit /b 1
    )
)

REM --- 2. build the three projects -------------------------------------------
for %%P in (Luban Luban.Agent Luban.Mcp) do (
    echo [build-luban] building %%P ...
    dotnet build "%LUBAN_SRC%\src\%%P\%%P.csproj" -c Release -o "%OUT%\%%P" -v m --nologo
    if errorlevel 1 (
        echo [build-luban] ERROR: build failed for %%P
        exit /b 1
    )
)

REM --- 3. assert Templates completeness --------------------------------------
REM TemplateManager.Init() loads Templates/ next to the DLL; without
REM cs-simple-json the project's C# code target cannot be generated.
if not exist "%OUT%\Luban\Templates\cs-simple-json" (
    echo [build-luban] ERROR: Templates\cs-simple-json missing under %OUT%\Luban
    exit /b 1
)

REM --- 4. drop pdb files -----------------------------------------------------
REM .gitignore excludes *.pdb, and these DLLs are used as CLIs (no debugging).
for %%P in (Luban Luban.Agent Luban.Mcp) do (
    if exist "%OUT%\%%P\*.pdb" del /q "%OUT%\%%P\*.pdb"
)

REM --- 5. record provenance --------------------------------------------------
> "%OUT%\LUBAN_VERSION.txt" (
    echo Luban source: %LUBAN_SRC%
    echo.
    echo tag/commit:
    git -C "%LUBAN_SRC%" describe --tags --always
    echo last commit:
    git -C "%LUBAN_SRC%" log -1 --format=%%H
    echo committed at:
    git -C "%LUBAN_SRC%" show -s --format=%%cI HEAD
)

REM --- 6. smoke test ---------------------------------------------------------
echo.
echo [build-luban] smoke test: Luban.Agent capabilities
dotnet "%OUT%\Luban.Agent\Luban.Agent.dll" capabilities > "%TEMP%\luban-capabilities.json" 2>nul
if errorlevel 1 (
    echo [build-luban] ERROR: Luban.Agent capabilities failed to run
    exit /b 1
)
findstr /c:"agentResultVersion" "%TEMP%\luban-capabilities.json" >nul
if errorlevel 1 (
    echo [build-luban] ERROR: capabilities output is not a valid AgentResult JSON
    echo [build-luban]        see "%TEMP%\luban-capabilities.json"
    exit /b 1
)
del /q "%TEMP%\luban-capabilities.json" 2>nul

echo.
echo [build-luban] OK - toolchain rebuilt under %OUT%
exit /b 0
