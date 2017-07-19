namespace DotnetMono
open System
open System.Linq
open System.Collections
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open Argu
open Shell
module Main =
    type CLIArguments =
        | [<AltCommandLine("-p")>] Project of project:string
        | [<AltCommandLine("-f")>] Framework of framework:string
        | [<AltCommandLine("-r")>] Runtime of runtime:string 
        | InferRuntime
        | [<AltCommandLine("-c")>] Configuration of configuration:string 
        | Restore
        | [<EqualsAssignment>]FrameworkPathOverride of frameworkPathOverride:string
        | [<EqualsAssignment>][<AltCommandLine("-mo")>] MonoOptions of monoOptions:string 
        | [<EqualsAssignment>][<AltCommandLine("-po")>] ProgramOptions of programOptions:string 
        with
            interface IArgParserTemplate with
                member s.Usage =
                    match s with
                    | Project _ -> "(Optional) Specify path to proj file.  Will default to current directory."
                    | Framework _ -> "(Mandatory) Specify a framework.  Most likely net462.  List available here: https://docs.microsoft.com/en-us/nuget/schema/target-frameworks"
                    | Runtime _ -> "(Optional) Specify a runtime. List available here: https://github.com/dotnet/docs/blob/master/docs/core/rid-catalog.md.  You will probably either need to run dotnet restore properly with runtime or pass --restore."
                    | InferRuntime _ -> "(Optional) Try to run explicitly on the current runtime. You will probably either need to run dotnet restore properly with runtime or pass --restore."
                    | Configuration _ -> "(Optional) Specify a configuration. (Debug|Release|Others) Will default to Debug"
                    | Restore -> "(Optional) Will attempt dotnet restore"
                    | FrameworkPathOverride _ -> "(Optional) Set FrameworkPathOverride as Environment Variable or as argument.  It will try to infer based on known good locations on osx/linux."
                    | MonoOptions _ -> "(Optional) Flags to be passed to mono."
                    | ProgramOptions _ -> "(Optional) Flags to be passed to running exe."

    let getProjectFile directory =
        let projectFiles = Directory.GetFiles(directory, "*.*proj");

        if projectFiles.Length = 0 then    
            failwith "No valid projects found"
        
        elif projectFiles.Length > 1 then   
            failwith "Too many project files"    

        projectFiles |> Seq.head

    let getDefaultProject () =
        let directory = Directory.GetCurrentDirectory()
        getProjectFile directory


    let getAssemblyName (project : string) =
        
        let doc = Xml.XmlDocument()
        use projStream =File.OpenRead(project)
        doc.Load(projStream)
        doc
            .GetElementsByTagName("AssemblyName")
            .Cast<Xml.XmlNode>()
        |> Seq.tryHead
        |> Option.map(fun x -> x.InnerText)

    let getProjectName (project : string) =
        IO.Path.GetFileNameWithoutExtension project 

    let inline (|?) (a: 'a option) b = 
        if a.IsSome then a.Value else b 

    let getExecutable project path =
        let exe = getAssemblyName project |?  (getProjectName project) |> sprintf "%s.exe"

        let exePath = Path.Combine(path, exe)

        if File.Exists exePath |> not then 
            failwithf "No exe founds %s" exePath

        exePath             


    [<EntryPoint>]
    let main argv =
        Console.CancelKeyPress.Add(fun _ ->
            printfn "CKP %s closing up" "dotnet-mono"
            Shell.killAllCreatedProcesses()
        )

        System.Runtime.Loader.AssemblyLoadContext.Default.add_Unloading(fun ctx ->
            printfn "ALC %s closing up" "dotnet-mono"
            Shell.killAllCreatedProcesses()
        )

        let currentProcess  = Process.GetCurrentProcess()
        printfn "dotnet-mono current process: %d" currentProcess.Id

        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
        let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet-mono", errorHandler = errorHandler)

        let results = parser.Parse(argv)

        let project = 
            match results.TryGetResult <@ Project @> with
            | Some p when p.EndsWith("proj") -> p
            | Some p -> getProjectFile p
            | _ -> getDefaultProject ()

        let frameworkPathOverride =
            match Environment.getEnvironmentVariable "FrameworkPathOverride" with
            | Some fpo -> fpo
            | None -> 
                match results.TryGetResult <@ FrameworkPathOverride @> with
                | Some fpo -> fpo
                | None -> 
                    match inferFrameworkPathOverride() with
                    | Some fpo -> fpo
                    | None -> 
                        parser.PrintUsage() |> printfn "%s"
                        failwith "Could not find FrameworkPathOverride in Environemnt, Argument, or Inferring"

        let envVars = 
            Environment.GetEnvironmentVariables()
            |> Seq.cast<DictionaryEntry> 
            |> Seq.append([DictionaryEntry("FrameworkPathOverride",frameworkPathOverride)])
        let projectRoot = (project |> IO.FileInfo).DirectoryName |> string

        let framework = results.GetResult <@ Framework @>
        let runtime = 
            match results.TryGetResult <@ Runtime @> with
            | Some r -> Some r
            | None -> 
                match results.TryGetResult <@ InferRuntime @> with
                | Some _ -> inferRuntime () |> Some
                | None -> None

        let runtimeArgs, runtimePath =
            match runtime with
            | Some r ->  sprintf "--runtime %s" r, r
            | None -> String.Empty, String.Empty
        let configuration = results.GetResult (<@ Configuration @>, defaultValue="Debug")
        let monoOptions = results.GetResult (<@ MonoOptions @>, defaultValue="")

        let programOptions = results.GetResult (<@ ProgramOptions @>, defaultValue="")
        
       
        if results.Contains <@ Restore @> then
            dotnetRestore [
                runtimeArgs
                project
            ] envVars
        
        dotnetBuild [
            sprintf "--configuration %s" configuration
            runtimeArgs
            sprintf "--framework %s" framework
            project
        ] envVars
         
        
        let buildChunkOutputPath = projectRoot @@ "bin" @@ configuration @@ framework @@ runtimePath
        let exe = buildChunkOutputPath |> getExecutable project
        mono buildChunkOutputPath monoOptions exe programOptions envVars

        0
