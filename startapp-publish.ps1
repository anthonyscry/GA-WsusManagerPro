cd "C:\projects\GA-WsusManagerPro\src\WsusManager.App"
dotnet publish "C:\projects\GA-WsusManagerPro\src\WsusManager.App\WsusManager.App.csproj" --configuration Release --self-contained true --runtime win-x64 -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true --output "C:\projects\GA-WsusManagerPro\publish"
