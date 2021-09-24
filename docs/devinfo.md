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

* Make code changes and commit
* Tag commit with version number
* build locally and verify
* Push 
* Download nuget package from github actions and verify

## To build a new package locally:

> dotnet build --configuration release

## To push a new package to the package registry at github:

> dotnet nuget push "source\bin\Release\Tobii.Glasses3.SDK.<major>.<minor>.<rev>.nupkg" --source "github"


