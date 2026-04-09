param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

dotnet publish "src/AgentUsageViewer.App/AgentUsageViewer.App.csproj" `
    -c $Configuration `
    -r $Runtime `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:PublishTrimmed=true `
    -p:TrimMode=partial
