// Learn more about F# at http://fsharp.org

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.IO
open Argu


module Shell =
    let execute (program : string) args workingdir =
        let psi = 
            ProcessStartInfo(
                FileName = program, 
                Arguments = args, 
                //UseShellExecute = false,
                WorkingDirectory = workingdir)
        Environment.GetEnvironmentVariables()
        |> Seq.cast<DictionaryEntry> 
        |> Seq.iter(fun (kvp) ->
            try
                psi.Environment.Add(kvp.Key |> string,kvp.Value |> string)
            with _ -> ()
        )
        Process.Start(psi).WaitForExit()

    let dotnet args =
        execute "dotnet" args null
    
    let dotnetRestore args =
        dotnet ("restore " + args)
    let dotnetBuild args =
        dotnet ("build " + args)

    let mono args =
        execute "mono" args null

module Main =
    open Shell
    type CLIArguments =
        | [<AltCommandLine("-p")>] Project of project:string option
        | [<AltCommandLine("-f")>] Framework of framework:string
        | [<AltCommandLine("-r")>] Runtime of runtime:string 
        | [<AltCommandLine("-c")>] Configuration of configuration:string 
        with
            interface IArgParserTemplate with
                member s.Usage =
                    match s with
                    | Project _ -> "specify a project.  Will default to current folder"
                    | Framework _ -> "specify a framework."
                    | Runtime _ -> "specify a runtime."
                    | Configuration _ -> "specify a configuration. Will default to debug"


    let getDefaultProject () =
        let directory = Directory.GetCurrentDirectory()
        let projectFiles = Directory.GetFiles(directory, "*.*proj");

        if projectFiles.Length = 0 then    
            failwith "No valid projects found"
        
        elif projectFiles.Length > 1 then   
            failwith "Too many project files"    

        projectFiles |> Seq.head
                
    let (@@) path1 path2 = IO.Path.Combine(path1,path2)

    [<EntryPoint>]
    let main argv =
        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
        let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet-mono", errorHandler = errorHandler)

        let results = parser.Parse(argv)

        let project = 
            match results.TryGetResult <@ Project @> with
            | Some (Some p) -> p
            | _ -> getDefaultProject ()

        let projectRoot = (project |> IO.FileInfo).DirectoryName |> string

        let framework = results.GetResult <@ Framework @>
        let runtime = results.GetResult <@ Runtime @>
        let configuration = results.GetResult (<@ Configuration @>, defaultValue="Debug")

        dotnetRestore (sprintf "--runtime %s %s " runtime project)
        dotnetBuild (sprintf "-c %s -r %s -f %s %s " configuration runtime framework project)
        
        let buildChunkOutputPath = projectRoot @@ "bin" @@ configuration @@ framework

        IO.Directory.GetFiles(buildChunkOutputPath, "*.exe")
        |> Seq.head
        |> FileInfo
        |> string
        |> mono


        //Microsoft.Build.Exceptions.InvalidProjectFileException: The imported project "/usr/local/share/dotnet/Sdks/FSharp.NET.Sdk/Sdk/Sdk.props" was not found. Confirm that the path in the <Import> declaration is correct, and that the file exists on disk.  /Users/jimmybyrd/Documents/GitHub/dotnetcoreplayground/rc4/rc4.fsproj
        // let globalProperties =
        //     dict[
        //         "MSBuildExtensionsPath", "/usr/local/share/dotnet/"
        //         "TargetFramework", framework
        //     ]
       
        0 // return an integer exit code
