// Learn more about F# at http://fsharp.org

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open Argu


module Shell =
    //Thanks FAKE
    let isNullOrEmpty str =
        String.IsNullOrEmpty(str)

    type ProcessResult = 
        {   ExitCode : int
            Messages : List<string>
            Errors : List<string> }
        member x.OK = x.ExitCode = 0
        static member New exitCode messages errors = 
            { ExitCode = exitCode
              Messages = messages
              Errors = errors }
    
    let start (proc : Process) = 
        try
            System.Console.OutputEncoding <- System.Text.Encoding.UTF8
        with exn ->
            printfn "Failed setting UTF8 console encoding, ignoring error... %s." exn.Message

        if proc.StartInfo.FileName.ToLowerInvariant().EndsWith(".exe") then
            proc.StartInfo.Arguments <- "--debug \"" + proc.StartInfo.FileName + "\" " + proc.StartInfo.Arguments
            proc.StartInfo.FileName <- "mono"
        proc.Start() |> ignore
        //startedProcesses.Add(proc.Id, proc.StartTime) |> ignore
    let ExecProcessWithLambdas configProcessStartInfoF (timeOut : TimeSpan) silent errorF messageF = 
        use proc = new Process()
        proc.StartInfo.UseShellExecute <- false
        configProcessStartInfoF proc.StartInfo
        if isNullOrEmpty proc.StartInfo.WorkingDirectory |> not then 
            if Directory.Exists proc.StartInfo.WorkingDirectory |> not then 
                failwithf "Start of process %s failed. WorkingDir %s does not exist." proc.StartInfo.FileName 
                    proc.StartInfo.WorkingDirectory
        if silent then 
            proc.StartInfo.StandardOutputEncoding <- Encoding.UTF8
            proc.StartInfo.StandardErrorEncoding  <- Encoding.UTF8
            proc.ErrorDataReceived.Add(fun d -> 
                if d.Data <> null then errorF d.Data)
            proc.OutputDataReceived.Add(fun d -> 
                if d.Data <> null then messageF d.Data)
            proc.StartInfo.RedirectStandardOutput <- true
            proc.StartInfo.RedirectStandardError <- true
            proc |> start
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()

            
        if timeOut = TimeSpan.MaxValue then proc.WaitForExit()
        else 
            if not <| proc.WaitForExit(int timeOut.TotalMilliseconds) then 
                try 
                    proc.Kill()
                with exn -> 
                    eprintfn "%A" 
                    <| sprintf "Could not kill process %s  %s after timeout." proc.StartInfo.FileName 
                        proc.StartInfo.Arguments
                failwithf "Process %s %s timed out." proc.StartInfo.FileName proc.StartInfo.Arguments
        // See http://stackoverflow.com/a/16095658/1149924 why WaitForExit must be called twice.
        proc.WaitForExit()
        proc.ExitCode


    let ExecProcessAndReturnMessages configProcessStartInfoF timeOut = 
        let errors = new List<_>()
        let messages = new List<_>()
        let exitCode = ExecProcessWithLambdas configProcessStartInfoF timeOut true (errors.Add) (messages.Add)
        ProcessResult.New exitCode messages errors

    let execute (program : string) (argsList : string list) workingdir =
        let args =
            argsList
            |> String.concat " "
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
        let proc = Process.Start(psi)
        proc.WaitForExit()
        if proc.ExitCode <> 0 then
            failwithf "%s failed with exit code %d" program proc.ExitCode

    let dotnet args =
        execute "dotnet" args null
    
    let dotnetRestore args =
        dotnet ["restore"; args]
    let dotnetBuild args =
        dotnet ["build" ; args]

    let mono workingDir options program programOptions =
        execute 
            "mono" 
            [
                options
                program
                programOptions
            ]
            workingDir

    let inferRuntime () = 
        let procResult = 
            ExecProcessAndReturnMessages 
                (fun psi -> 
                    psi.FileName <- "dotnet"
                    psi.Arguments <- "--info") 
                TimeSpan.MaxValue
        procResult.Messages 
        |> Seq.filter(fun s -> s.Contains("RID"))
        |> Seq.head
        |> fun s -> s.Replace("RID:","").Trim()

module Main =
    open Shell
    type CLIArguments =
        | [<AltCommandLine("-p")>] Project of project:string
        | [<AltCommandLine("-f")>] Framework of framework:string
        | [<AltCommandLine("-r")>] Runtime of runtime:string 
        | [<AltCommandLine("-c")>] Configuration of configuration:string 
        | Restore
        | [<EqualsAssignment>][<AltCommandLine("-mo")>] MonoOptions of monoOptions:string 
        | [<EqualsAssignment>][<AltCommandLine("-po")>] ProgramOptions of programOptions:string 
        with
            interface IArgParserTemplate with
                member s.Usage =
                    match s with
                    | Project _ -> "(Optional) Specify path to proj file.  Will default to current directory."
                    | Framework _ -> "(Mandatory) Specify a framework.  Most likely net462.  List available here: https://docs.microsoft.com/en-us/nuget/schema/target-frameworks"
                    | Runtime _ -> "(Optional) Specify a runtime. It will attempt to infer if missing.  List available here: https://github.com/dotnet/docs/blob/master/docs/core/rid-catalog.md"
                    | Configuration _ -> "(Optional) Specify a configuration. (Debug|Release) Will default to debug"
                    | Restore -> "(Optional) Will attempt dotnet restore"
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


    let getExecutable path =
        let exeFiles =
            IO.Directory.GetFiles(path, "*.exe")

        if exeFiles.Length = 0 then    
            failwith "No valid exes found"
        
        elif exeFiles.Length > 1 then   
            failwith "Too many exe files"
        let fi= 
            exeFiles
            |> Seq.head
            |> FileInfo
        (fi |> string, fi.Directory |> string)                
    let (@@) path1 path2 = IO.Path.Combine(path1,path2)

    [<EntryPoint>]
    let main argv =
        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
        let parser = ArgumentParser.Create<CLIArguments>(programName = "dotnet-mono", errorHandler = errorHandler)

        let results = parser.Parse(argv)
        

        let project = 
            match results.TryGetResult <@ Project @> with
            | Some p when p.EndsWith("proj") -> p
            | Some p -> getProjectFile p
            | _ -> getDefaultProject ()

        let projectRoot = (project |> IO.FileInfo).DirectoryName |> string

        let framework = results.GetResult <@ Framework @>
        let runtime = 
            match results.TryGetResult <@ Runtime @> with
            | Some r -> r
            | None -> inferRuntime ()
        let configuration = results.GetResult (<@ Configuration @>, defaultValue="Debug")
        let monoOptions = results.GetResult (<@ MonoOptions @>, defaultValue="")

        let programOptions = results.GetResult (<@ ProgramOptions @>, defaultValue="")
        if results.Contains <@ Restore @> then
            dotnetRestore (sprintf "--runtime %s %s " runtime project)
        dotnetBuild (sprintf "-c %s -r %s -f %s %s " configuration runtime framework project)
        
        let buildChunkOutputPath = projectRoot @@ "bin" @@ configuration @@ framework
        let exe, workingDir = (buildChunkOutputPath |> getExecutable)
        mono workingDir monoOptions exe programOptions


        //Microsoft.Build.Exceptions.InvalidProjectFileException: The imported project "/usr/local/share/dotnet/Sdks/FSharp.NET.Sdk/Sdk/Sdk.props" was not found. Confirm that the path in the <Import> declaration is correct, and that the file exists on disk.  /Users/jimmybyrd/Documents/GitHub/dotnetcoreplayground/rc4/rc4.fsproj
        // let globalProperties =
        //     dict[
        //         "MSBuildExtensionsPath", "/usr/local/share/dotnet/"
        //         "TargetFramework", framework
        //     ]
       
        0 // return an integer exit code
