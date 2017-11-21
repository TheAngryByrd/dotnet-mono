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
open Logary.Facade
open Logary.Facade.Message
open ProcessLogging
open Dotnet.ProjInfo
module Main =
    

    
    type CLIArguments =
        | [<AltCommandLine("-p")>] Project of project:string
        | [<AltCommandLine("-f")>] Framework of framework:string
        | [<AltCommandLine("-r")>] Runtime of runtime:string 
        | InferRuntime
        | [<AltCommandLine("-c")>] Configuration of configuration:string 
        | Restore
        | No_Restore
        | [<EqualsAssignment>]FrameworkPathOverride of frameworkPathOverride:string
        | [<EqualsAssignment>][<AltCommandLine("-mo")>] MonoOptions of monoOptions:string 
        | [<EqualsAssignment>][<AltCommandLine("-po")>] ProgramOptions of programOptions:string 
        | LoggerLevel of logLevel:string
        | No_Build
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
                    | No_Restore -> "(Optional) Will pass --no-restore to dotnet build."
                    | FrameworkPathOverride _ -> "(Optional) Set FrameworkPathOverride as Environment Variable or as argument.  It will try to infer based on known good locations on osx/linux."
                    | MonoOptions _ -> "(Optional) Flags to be passed to mono."
                    | ProgramOptions _ -> "(Optional) Flags to be passed to running exe."
                    | LoggerLevel _ -> "(Optional) LogLevel for dotnet-mono defaults to Info (Verbose|Debug|Info|Warn|Error|Fatal)"
                    | No_Build -> "(Optional) Will attempt to skip dotnet build."

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



    let msbuildExec projDir =
        Dotnet.ProjInfo.Inspect.dotnetMsbuild <| 
            fun exe args ->
                let result = execProcessAndReturnMessages' projDir args exe 
                result.ExitCode,  (projDir, exe, args |> String.concat " ")
    let gp () = Dotnet.ProjInfo.Inspect.getProperties (["AssemblyName"; "TargetFrameworks"; "TargetFramework"])

    type ProjInfo = {
        TargetFrameworks : string list
        AssemblyName : string option
    }
    let getProjInfo additionalMSBuildProps (project : string)=
        let projDir = Path.GetDirectoryName project
        let log = ignore

        let additionalArgs = additionalMSBuildProps |> List.map (Dotnet.ProjInfo.Inspect.MSBuild.MSbuildCli.Property)

        let result =
            project
            |> Dotnet.ProjInfo.Inspect.getProjectInfo log (msbuildExec projDir) gp additionalArgs

        match result with
        | Result.Ok (Inspect.GetResult.Properties props) -> props |> Map.ofList
        | _ -> Map.empty

    let getAssemblyName (project : string) =
        //printfn "project %A" <| getProjInfo [] project
        
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
        match a with
        | Some a' -> a'
        | None -> b
        

    let getExecutable project path =
        let exe = getAssemblyName project |?  (getProjectName project) |> sprintf "%s.exe"

        let exePath = Path.Combine(path, exe)

        if File.Exists exePath |> not then 
            failwithf "No exe founds %s" exePath

        exePath             


    let setupCloseSignalers () =
        Console.CancelKeyPress.Add(fun _ ->
            Message.eventX "Handling CancelKeyPress..." LogLevel.Info
            |> logger.logSync
            Shell.killAllCreatedProcesses()
        )

        System.Runtime.Loader.AssemblyLoadContext.Default.add_Unloading(fun ctx ->
            Message.eventX "Handling AssemblyLoadContext Unloading..." LogLevel.Info
            |> logger.logSync
            Shell.killAllCreatedProcesses()
        )




    [<EntryPoint>]
    let main argv =

        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
        let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet-mono", errorHandler = errorHandler)
        let results = parser.Parse(argv)

       
        match results.TryGetResult<@ LoggerLevel @> with
        | Some ll ->
            let logaryLevel=
                ll
                |> LogLevel.ofString
            logger <- Log.create logaryLevel "dotnet-mono"
        | None -> ()

        Message.eventX "arguments parsed: {args}" LogLevel.Debug
        |> setField "args" results
        |> logger.logSync


        setupCloseSignalers ()

        Message.eventX "dotnet-mono current process id: {id}" LogLevel.Info
        |> Message.setField "id" (Process.GetCurrentProcess()).Id
        |> logger.logSync




        let project = 
            match results.TryGetResult <@ Project @> with
            | Some p when p.EndsWith("proj") -> p
            | Some p -> getProjectFile p
            | _ -> getDefaultProject ()

        Message.eventX "Project file found: {project}" LogLevel.Debug
        |> setField "project" project
        |> logger.logSync

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


        Message.eventX "FrameworkPathOverride: {FrameworkPathOverride}" LogLevel.Debug
        |> setField "FrameworkPathOverride" frameworkPathOverride
        |> logger.logSync

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


        if not <| results.Contains <@ No_Build @> then
            dotnetBuild [
                (if results.Contains <@ No_Restore @> then"--no-restore" else String.Empty) 
                sprintf "--configuration %s" configuration
                runtimeArgs
                sprintf "--framework %s" framework
                project
            ] envVars
         
        
        let buildChunkOutputPath = projectRoot @@ "bin" @@ configuration @@ framework @@ runtimePath
        let exe = buildChunkOutputPath |> getExecutable project
        mono buildChunkOutputPath monoOptions exe programOptions envVars

        0
