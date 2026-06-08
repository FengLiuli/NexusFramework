set WORKSPACE=..\..\..
set OUTPUT_JSON_DIR=%WORKSPACE%\Assets\Demo\TableData
if not "%1"=="" set OUTPUT_JSON_DIR=%1
set OUTPUT_CODE_DIR=%WORKSPACE%\Assets\Demo\Emberheart\GAS\TableClass
set CONF_ROOT=%WORKSPACE%\Assets\Demo\Table
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll

dotnet %LUBAN_DLL% ^
    -t client ^
    -c cs-simple-json ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%OUTPUT_CODE_DIR% ^
    -x outputDataDir=%OUTPUT_JSON_DIR% ^
    -x l10n.provider=default ^
    -x "l10n.textFile.path=*@%CONF_ROOT%\Datas\l10n.json" ^
    -x l10n.textFile.keyFieldName=key ^
    -x l10n.textFile.languageFieldName=zh ^
    -x l10n.convertTextKeyToValue=1
pause