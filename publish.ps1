# Builds the single self-contained GammaKey.exe into .\publish
$ErrorActionPreference = "Stop"
dotnet test
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
dotnet publish src/GammaKey/GammaKey.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
Get-Item publish\GammaKey.exe | Select-Object Name, @{n="MB";e={[math]::Round($_.Length/1MB,1)}}
