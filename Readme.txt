
To set up github credentials locally

> dotnet nuget add source https://nuget.pkg.github.com/tobiipro/index.json -n github -u <github user name> -p <github token with access to packages>

To make a new release:

* Make code changes
* Update <Version> in G3SDK\G3SDK.csproj (Properties=>Package=>Package version)
* Update Assembly version and File Version
* Build and verify

To build a new package:

> dotnet build --configuration release

Make sure the nupkg-file was correctly built: 

> dir G3SDK\bin\Release\*.nupkg

To push a new package to github:

> dotnet nuget push "G3SDK\bin\Release\Tobii.Pro.G3.SDK.net.0.x.y.nupkg" --source "github"


