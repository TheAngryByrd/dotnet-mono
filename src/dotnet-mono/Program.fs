namespace DotnetMono
open System
open System.Collections
open System.Diagnostics
open System.IO
open Argu
open Shell
open Logary.Facade
open Logary.Facade.Message
open ProcessLogging
open System.Xml.Linq
open Argu
open System.Globalization
open System.Collections.Generic
open Chessie.ErrorHandling
open Microsoft.Build.Execution

module SystemNetHttpFixer =

    let appendCorrectBindingRedirect version (xd : XDocument) =
        xd.Descendants()
        |> Seq.tryFind(fun x ->
            x.Name.LocalName = "runtime"
            )
        |> Option.iter(fun runtimeElement ->
            let redirect = 
                XElement.Parse 
                <| sprintf
                            """<assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Net.Http" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-999.999.999.999" newVersion="%s" />
      </dependentAssembly>
    </assemblyBinding>""" version
            runtimeElement.Add(redirect)

            Message.eventX "Setting assemblyBinding for {dll} to {version}" LogLevel.Debug
            |> Message.setField "dll" "System.Net.Http"
            |> Message.setField "version" version
            |> logger.logSync
        )

    // Broken on .netcore ??
    // let systemNetHttpGACVersion () =
    //     try
    //         let monoGAC = Environment.GetEnvironmentVariable "FrameworkPathOverride" |> DirectoryInfo
    //         let systemNetHttp =
    //             monoGAC.GetFiles ("*.dll")
    //             |> Seq.find(fun fi -> fi.Name = "System.Net.Http.dll")
    //         let asm = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath systemNetHttp.FullName
    //         let ver = asm.GetName().Version.ToString()
    //         // printfn "ver: %A" ver
    //         ver
    //     with e ->
    //         eprintfn "%A" e
    //         "4.0.0.0"

    let reconfigureAssemblyRedirect (xd : XDocument) =
        let ele =
            xd.Descendants()
            |> Seq.tryFind(fun x ->
                x.Name.LocalName = "assemblyIdentity" && x.Attributes() |> Seq.exists (fun attr -> attr.Value="System.Net.Http")
                )

        match ele with
        | Some assemblyIdentity ->
            let nodeToDelete = assemblyIdentity.Parent.Parent
            Message.eventX "Deleting node {node}" LogLevel.Debug
            |> Message.setField "node" (nodeToDelete.ToString())
            |> logger.logSync
            nodeToDelete.Remove()
        | None -> ()

        appendCorrectBindingRedirect ("4.0.0.0") xd
        
    let deleteBadSystemNetHttp (exePath : FileInfo) =
        exePath.Directory.GetFiles("*.dll")
        |> Seq.tryFind(fun fi -> fi.Name = "System.Net.Http.dll")
        |> Option.iter(fun fi -> 
            Message.eventX "Deleting {file}" LogLevel.Debug
            |> Message.setField "file" fi.FullName
            |> logger.logSync
            fi.Delete())
 


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
        | Purge_System_Net_Http
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
                    | Purge_System_Net_Http -> "(Optional) Mono has issues with HttpClient noted here: https://github.com/dotnet/corefx/issues/19914 ...This will attempt to resolve them."

    type Errors =
    | NoFrameworkPathOverrideFound
    | NoRunnableExecutableFound
    | TooManyProjectsInDirectory of string array
    | NoProjectsFoundInDirectory
    | DotnetRestoreFailure of ProcessFailure
    | DotnetBuildFailure of ProcessFailure
    | MonoExecuteFailure of ProcessFailure

    let getProjectFile directory =
        let projectFiles = Directory.GetFiles(directory, "*.*proj");

        if projectFiles.Length = 0 then    
            fail NoProjectsFoundInDirectory
        
        elif projectFiles.Length > 1 then   
            TooManyProjectsInDirectory projectFiles |> fail
        else
            projectFiles 
            |> Seq.head
            |> ok

    let getDefaultProject () =
        let directory = Directory.GetCurrentDirectory()
        getProjectFile directory

    let setupCloseSignalers () =
        Console.CancelKeyPress.Add(fun _ ->
            Message.eventX "Handling CancelKeyPress..." LogLevel.Info
            |> logger.logSync
            Shell.killAllCreatedProcesses()
        )

        System.Runtime.Loader.AssemblyLoadContext.Default.add_Unloading(fun _ ->
            Message.eventX "Handling AssemblyLoadContext Unloading..." LogLevel.Info
            |> logger.logSync
            Shell.killAllCreatedProcesses()
        )
        
    let splitArgs args =
        let argus =
            args
            |> Array.takeWhile(fun x -> x <> "--")
        let additionalArgs =
            args
            |> Array.skipWhile(fun x -> x <> "--")
            |> (fun a -> if Array.tryHead a = Some "--" then Array.tail a else a)
        argus,additionalArgs

    // https://github.com/dotnet/cli/blob/master/src/dotnet/commands/dotnet-run/RunCommand.cs
    let globalProperties configuration framework runtime = 
        let props = Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        props.Add("EnableDefaultItems","false")
        props.Add("Configuration",configuration)
        props.Add("TargetFramework",framework)
        props.Add("MSBuildExtensionsPath",IO.Path.GetDirectoryName(Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH")))
        props |> Dictionary.addIfSome "RuntimeIdentifier" runtime
        props

    let main' argv = trial {
        let argus,additional = splitArgs argv

        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
        let parser = 
            ArgumentParser.Create<CLIArguments>(
                programName = "dotnet-mono", 
                helpTextMessage =  "Much like dotnet-run you can specify dotnet mono [options] [[--] <additional arguments>...]] to pass arguments to the underlying program.",
                errorHandler = errorHandler)
                
        let results = parser.Parse(argus)
       
        match results.TryGetResult<@ LoggerLevel @> with
        | Some ll ->
            let logaryLevel =
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

        let! project = 
            match results.TryGetResult <@ Project @> with
            | Some p when p.EndsWith("proj") -> p |> ok
            | Some p -> getProjectFile p
            | _ -> getDefaultProject ()

        Message.eventX "Project file found: {project}" LogLevel.Debug
        |> setField "project" project
        |> logger.logSync


        let! frameworkPathOverride =
            match Environment.getEnvironmentVariable "FrameworkPathOverride" with
            | Some fpo -> ok fpo
            | None -> 
                match results.TryGetResult <@ FrameworkPathOverride @> with
                | Some fpo -> ok fpo
                | None -> 
                    match inferFrameworkPathOverride() with
                    | Some fpo -> ok fpo
                    | None -> 
                        parser.PrintUsage() |> printfn "%s"
                        fail NoFrameworkPathOverrideFound


        Message.eventX "FrameworkPathOverride: {FrameworkPathOverride}" LogLevel.Debug
        |> setField "FrameworkPathOverride" frameworkPathOverride
        |> logger.logSync

        let envVars = 
            Environment.GetEnvironmentVariables()
            |> Seq.cast<DictionaryEntry> 
            |> Seq.append([DictionaryEntry("FrameworkPathOverride",frameworkPathOverride)])

        let framework = results.GetResult <@ Framework @>
        let runtime = 
            match results.TryGetResult <@ Runtime @> with
            | Some r -> Some r
            | None -> 
                match results.TryGetResult <@ InferRuntime @> with
                | Some _ -> inferRuntime () |> Some
                | None -> None

        let runtimeArgs =
            match runtime with
            | Some r ->  sprintf "--runtime %s" r
            | None -> String.Empty
        let configuration = results.GetResult (<@ Configuration @>, defaultValue="Debug")
        let monoOptions = results.GetResult (<@ MonoOptions @>, defaultValue="")

        let programOptionDefault = 
            match String.Join(" ", additional) with
            | x when String.IsNullOrEmpty(x) -> String.Empty
            | x -> x
            
        let programOptions = results.GetResult (<@ ProgramOptions @>, defaultValue=programOptionDefault)

        let globalProperties = globalProperties configuration framework runtime

        globalProperties
        |> Seq.iter(fun p -> 
                Message.eventX "globalProperties {key}={value}"  LogLevel.Verbose
                |> Message.setField "key"  p.Key
                |> Message.setField "value" p.Value
                |> logger.logSync
        )

        let proj = ProjectInstance(project, globalProperties, null)


        proj.Properties
        |> Seq.iter(fun p -> 
                Message.eventX "Project properties {key}={value}"  LogLevel.Verbose
                |> Message.setField "key"  p.Name
                |> Message.setField "value" p.EvaluatedValue
                |> logger.logSync
        )

        let! runExeLocation =  
            match proj.GetPropertyValue("RunCommand") with
            | null | "" ->
                fail NoRunnableExecutableFound
            | runCommand -> ok runCommand

        Message.eventX "Run Location : {RunCommand}" LogLevel.Debug
        |> Message.setField "RunCommand" runExeLocation
        |> logger.logSync
        
       
        if results.Contains <@ Restore @> then
            do! dotnetRestore [
                    runtimeArgs
                    project
                ] envVars
                |> Trial.mapFailure (List.map DotnetRestoreFailure)


        if not <| results.Contains <@ No_Build @> then
            do! dotnetBuild [
                    (if results.Contains <@ No_Restore @> then"--no-restore" else String.Empty) 
                    sprintf "--configuration %s" configuration
                    runtimeArgs
                    sprintf "--framework %s" framework
                    project
                ] envVars
                |> Trial.mapFailure (List.map DotnetBuildFailure)


        if results.Contains <@Purge_System_Net_Http@> then
            runExeLocation
            |> FileInfo
            |> SystemNetHttpFixer.deleteBadSystemNetHttp 

            let fixConfig () =
                let configLocation =
                    runExeLocation
                    |> sprintf "%s.config"

                let xd = XDocument.Load(configLocation)
                
                SystemNetHttpFixer.reconfigureAssemblyRedirect xd
                configLocation
                |> File.Create 
                |> xd.Save
            fixConfig ()

        do! mono (IO.Path.GetDirectoryName runExeLocation) monoOptions runExeLocation programOptions envVars
            |> Trial.mapFailure (List.map MonoExecuteFailure)
    }


    [<EntryPoint>]
    let main argv =
        let result = main' argv
        let exitCode =
            match result with
            | Ok((),msgs) ->
                0
            | Bad errs ->
                let err = errs |> Seq.head
                match err with
                | NoFrameworkPathOverrideFound ->
                    Message.eventX "Could not find FrameworkPathOverride in Environemnt, Argument, or Inferring" LogLevel.Error
                    |> logger.logSync
                    1
                | NoRunnableExecutableFound ->
                    Message.eventX "No runnable executable was found. Ensure your project outputs an executable." LogLevel.Error
                    |> logger.logSync
                    2
                | TooManyProjectsInDirectory projs ->
                    Message.eventX "Too many projects found : {projs}.  Please use --project and pass the project you wish to use" LogLevel.Error
                    |> Message.setField "projs" projs
                    |> logger.logSync
                    3
                | NoProjectsFoundInDirectory ->
                    Message.eventX "No project files found." LogLevel.Error
                    |> logger.logSync
                    4
                | DotnetRestoreFailure processFailure -> 
                    Message.eventX "Dotnet restore failure exitcode : {exitcode} ." LogLevel.Error
                    |> Message.setField "exitcode" processFailure.exitcode
                    |> logger.logSync
                    5
                | DotnetBuildFailure processFailure -> 
                    Message.eventX "Dotnet build failure exitcode : {exitcode} ." LogLevel.Error
                    |> Message.setField "exitcode" processFailure.exitcode
                    |> logger.logSync
                    6
                | MonoExecuteFailure processFailure -> 
                    Message.eventX "mono failure exitcode : {exitcode} ." LogLevel.Error
                    |> Message.setField "exitcode" processFailure.exitcode
                    |> logger.logSync
                    7
                

        exitCode
