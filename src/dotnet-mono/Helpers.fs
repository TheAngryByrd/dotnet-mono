
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
open DotnetMono.Logging
open DotnetMono.Logging.Types
open Chessie.ErrorHandling


module Environment =
    let getEnvironmentVariable =
        Environment.GetEnvironmentVariable
        >> Option.ofObj
//Thanks FAKE
module Shell =

    let logger = LogProvider.getLoggerByName("DotnetMono.Shell")
    
    type internal ConcurrentBag<'T> with
        member internal this.Clear() = 
            while not(this.IsEmpty) do
                this.TryTake() |> ignore
    let startedProcesses = ConcurrentBag()

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

    let kill (proc : Process) = 
        logger.info(
            Log.setMessage "Trying to kill: {ProcessName} - {ProcessId}"
            >> Log.addParameter proc.ProcessName
            >> Log.addParameter proc.Id
        )
        try 
            proc.Kill()
            logger.info(
                Log.setMessage "Killed: {ProcessName} - {ProcessId}"
                >> Log.addParameter proc.ProcessName
                >> Log.addParameter proc.Id
            )
        with exn -> 
            logger.error(
                Log.setMessage "Could not kill process {processName} (Id = {id})" 
                >> Log.addParameter proc.ProcessName
                >> Log.addParameter proc.Id
                >> Log.addExn exn
            )


    let dateWithinTolerance (date1 : DateTime) date2 (tolerance : TimeSpan) =
        let diff = date1 - date2
        diff <= tolerance
    let killAllCreatedProcesses() =
    
        logger.info(
            Log.setMessage "Killing all processes that are created by dotnet-mono and are still running."
        )

        let traced = ref false
        for pid, startTime in startedProcesses do
            try
                let proc = Process.GetProcessById pid
                // process IDs may be reused by the operating system so we need
                // to make sure the process is indeed the one we started
                if dateWithinTolerance proc.StartTime startTime (TimeSpan.FromSeconds 0.5) && not proc.HasExited then
                    try 
                        if not !traced then

                            traced := true
                            kill proc
                    with exn -> 
                        logger.error(
                            Log.setMessage "Killing {ProcessName} - {ProcessId} failed"
                            >> Log.addParameter proc.ProcessName
                            >> Log.addParameter proc.Id
                            >> Log.addExn exn
                        )                   
            with exn -> ()
        startedProcesses.Clear()
    let start (proc : Process) = 
        // try
        //     System.Console.OutputEncoding <- System.Text.Encoding.UTF8
        // with exn ->
        //     printfn "Failed setting UTF8 console encoding, ignoring error... %s." exn.Message

        if proc.StartInfo.FileName.ToLowerInvariant().EndsWith(".exe") then
            proc.StartInfo.Arguments <- "--debug \"" + proc.StartInfo.FileName + "\" " + proc.StartInfo.Arguments
            proc.StartInfo.FileName <- "mono"
        proc.Start() |> ignore
        startedProcesses.Add(proc.Id, proc.StartTime) |> ignore
        logger.info(
            Log.setMessage "Started: {ProcessName} {Arguments} - {ProcessId}"
            >> Log.addParameter proc.StartInfo.FileName
            >> Log.addParameter proc.StartInfo.Arguments
            >> Log.addParameter proc.Id
        )


    let startAndWait (proc : Process) =
        start proc
        proc.WaitForExit()

        logger.info(
            Log.setMessage "Ended: {App} - {ProcessId}"
            >> Log.addParameter proc.StartInfo.FileName
            >> Log.addParameter proc.Id
        )


    let ExecProcessWithLambdas configProcessStartInfoF (timeOut : TimeSpan) silent errorF messageF = 
        use proc = new Process()
        proc.StartInfo.UseShellExecute <- false
        configProcessStartInfoF proc.StartInfo
        if isNullOrEmpty proc.StartInfo.WorkingDirectory |> not then 
            if Directory.Exists proc.StartInfo.WorkingDirectory |> not then 
                failwithf "Start of process %s failed. WorkingDir %s does not exist." proc.StartInfo.FileName 
                    proc.StartInfo.WorkingDirectory
        if silent then 
            // proc.StartInfo.StandardOutputEncoding <- Encoding.UTF8
            // proc.StartInfo.StandardErrorEncoding  <- Encoding.UTF8
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

    let joinArgs args =
        args
        |> String.concat " "

    let execProcessAndReturnMessages' workingDir argsList program =
        ExecProcessAndReturnMessages (
            fun psi ->
                psi.FileName <- program
                psi.Arguments <- joinArgs argsList
                psi.WorkingDirectory <- workingDir
        ) (TimeSpan.FromSeconds(5.))

    
    type ProcessFailure = { exitcode : int }

    let executeOrFail (program : string) (argsList : string list) workingdir envVars=
        let psi = 
            ProcessStartInfo(
                FileName = program, 
                Arguments = joinArgs argsList, 
                // RedirectStandardOutput = true,
                //UseShellExecute = false,
                WorkingDirectory = workingdir)
        envVars
        |> Seq.iter(fun (kvp : DictionaryEntry) ->
            try
                psi.Environment.Add(kvp.Key |> string,kvp.Value |> string)
            with _ -> ()
        )
        use proc = new Process(StartInfo =psi)
        startAndWait proc
        if proc.ExitCode <> 0 then
            fail <| { exitcode = proc.ExitCode }
            // failwithf "%s failed with exit code %d" program proc.ExitCode
        else
            ok ()

    let dotnet args =
        executeOrFail "dotnet" args null
    
    let dotnetRestore args =
        dotnet ("restore" :: args)
    let dotnetBuild args =
        dotnet ("build" :: args)

    let mono workingDir options program programOptions =
        executeOrFail 
            "mono" 
            [
                options
                program
                programOptions
            ]
            workingDir
    let which program =
        let result =  execProcessAndReturnMessages' "" [program] "which"
        result.Messages |> Seq.head
        
    let (@@) path1 path2 = IO.Path.Combine(path1,path2)
    let inferFrameworkPathOverride () =
        let monoLocation = which "mono" |>  IO.FileInfo
        let frameworkOverride = monoLocation.Directory.Parent.FullName @@ "lib/mono/4.5/"
        Some frameworkOverride

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


module Dictionary =
    let addIfSome key valueOpt (dictionary : IDictionary<_,_>) =
        match valueOpt with
        | Some x ->
            dictionary.Add(key,x)
        | None ->
            ()
