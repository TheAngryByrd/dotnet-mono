#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System

#if MONO
let inferFrameworkPathOverride () =
    let mscorlib = "mscorlib.dll"
    let possibleFrameworkPaths =
        [ 
            "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/"
            "/usr/local/Cellar/mono/4.6.12/lib/mono/4.5/"
            "/usr/lib/mono/4.5/"
        ]

    possibleFrameworkPaths
    |> Seq.find (fun p ->IO.File.Exists(p @@ mscorlib))

Environment.SetEnvironmentVariable("FrameworkPathOverride", inferFrameworkPathOverride ())
#endif
let release = LoadReleaseNotes "RELEASE_NOTES.md"
let srcGlob = "src/**/*.fsproj"
let testsGlob = "tests/**/*.fsproj"

Target "Clean" (fun _ ->
    ["bin"; "temp" ;"dist"]
    |> CleanDirs

    !! srcGlob
    ++ testsGlob
    |> Seq.collect(fun p -> 
        ["bin";"obj"] 
        |> Seq.map(fun sp ->
             IO.Path.GetDirectoryName p @@ sp)
        )
    |> CleanDirs

    )

Target "DotnetRestore" (fun _ ->
    !! srcGlob
    ++ testsGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Restore (fun c ->
            { c with
                Project = proj
            }) 
))

Target "DotnetBuild" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Build (fun c ->
            { c with
                Project = proj
            }) 
))

Target "DotnetTest" (fun _ ->
    !! testsGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Test (fun c ->
            { c with
                Project = proj
                WorkingDir = IO.Path.GetDirectoryName proj
            }) 
))

Target "DotnetPack" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Pack (fun c ->
            { c with
                Project = proj
                Configuration = "Release"
                OutputPath = IO.Directory.GetCurrentDirectory() @@ "dist"
                AdditionalArgs = 
                [
                    sprintf "/p:PackageVersion=%s" release.NugetVersion
                    sprintf "/p:PackageReleaseNotes=\"%s\"" (String.Join("\n",release.Notes))
                ]
            }) 
    )
)

Target "Publish" (fun _ ->
    Paket.Push(fun c ->
            { c with 
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )
)

Target "Release" (fun _ ->

    if Git.Information.getBranchName "" <> "master" then failwith "Not on master"

    let releaseNotesGitCommitFormat = ("",release.Notes |> Seq.map(sprintf "* %s\n")) |> String.Join

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s \n%s" release.NugetVersion releaseNotesGitCommitFormat)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
)

"Clean"
  ==> "DotnetRestore"
  ==> "DotnetBuild"
  ==> "DotnetTest"
  ==> "DotnetPack"
  ==> "Publish"
  ==> "Release"

RunTargetOrDefault "DotnetPack"