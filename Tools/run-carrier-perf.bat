@echo off
REM ============================================================
REM  Carrier 核心层性能基准（EditMode / batchmode）
REM
REM  运行前请关闭已打开本项目的 Unity 编辑器（batchmode 独占工程）。
REM  结果：
REM    TestResults\carrier-perf.xml   测试结果（NUnit）
REM    TestResults\carrier-perf.log   Unity 完整日志（含 [PERF] 行）
REM  只看数据时，可在日志里搜索 "[PERF]"。
REM ============================================================

set "UNITY=E:\Application\Software\Unity\Editor\2022.3.62f2c1\2022.3.62f2c1\Editor\Unity.exe"
set "PROJECT=D:\Work\Unity\NexusFramework"
set "OUT=%PROJECT%\TestResults"

if not exist "%OUT%" mkdir "%OUT%"

"%UNITY%" -batchmode -projectPath "%PROJECT%" ^
  -runTests -testPlatform EditMode -testCategory Performance ^
  -testResults "%OUT%\carrier-perf.xml" ^
  -logFile "%OUT%\carrier-perf.log" -quit

echo.
echo Exit code: %ERRORLEVEL%
echo Results: %OUT%\carrier-perf.xml
echo Log:     %OUT%\carrier-perf.log
pause
