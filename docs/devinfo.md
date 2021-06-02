# Instructions for Tobii Pro developers

## To set up github credentials locally

> dotnet nuget add source https://nuget.pkg.github.com/tobiipro/index.json -n github -u <github user name> -p <github token with access to packages>

To create a new token for access to the internal Tobii Pro package registry: 
* go to https://github.com/settings/tokens
* click "Generate new token"
* give it a good name and select "read:packages" in the list of scopes
* click "Generate" and make a copy of the token (it will look like this: "ghp_gcNbfyuKJGHl85LNhgtrflbJTln8nlbbh6lvgffkm")
* Disable SSO for this token

## To make a new release:

* Make code changes
* Update <Version> in G3SDK\G3SDK.csproj (Properties=>Package=>Package version)
* Update Assembly version and File Version
* Build and verify

## To build a new package:

> dotnet build --configuration release

## Make sure the nupkg-file was correctly built: 

> dir source\bin\Release\*.nupkg

## To push a new package to the package registry at github:

> dotnet nuget push "source\bin\Release\Tobii.Pro.G3.SDK.net.<major>.<minor>.<rev>.nupkg" --source "github"


