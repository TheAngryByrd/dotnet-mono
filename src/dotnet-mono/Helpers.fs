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

module Environment =
    let getEnvironmentVariable =
        Environment.GetEnvironmentVariable
        >> Option.ofObj
//Thanks FAKE
module Shell =
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
        printfn "Trying to kill process %s (Id = %d)" proc.ProcessName proc.Id
        try 
            proc.Kill()
        with exn -> printfn "Could not kill process %s (Id = %d).%sMessage: %s" proc.ProcessName proc.Id Environment.NewLine exn.Message

    let killAllCreatedProcesses() =

        let traced = ref false
        for pid, startTime in startedProcesses do
            try
                let proc = Process.GetProcessById pid
                
                // process IDs may be reused by the operating system so we need
                // to make sure the process is indeed the one we started
                if proc.StartTime = startTime && not proc.HasExited then
                    try 
                        if not !traced then
                            printfn "%s" "Killing all processes that are created by dotnet-mono and are still running."
                            traced := true

                            printfn  "Trying to kill %s" proc.ProcessName
                            kill proc
                    with exn -> printfn "Killing %s failed with %s" proc.ProcessName exn.Message                              
            with exn -> ()
        startedProcesses.Clear()
    let start (proc : Process) = 
        try
            System.Console.OutputEncoding <- System.Text.Encoding.UTF8
        with exn ->
            printfn "Failed setting UTF8 console encoding, ignoring error... %s." exn.Message

        if proc.StartInfo.FileName.ToLowerInvariant().EndsWith(".exe") then
            proc.StartInfo.Arguments <- "--debug \"" + proc.StartInfo.FileName + "\" " + proc.StartInfo.Arguments
            proc.StartInfo.FileName <- "mono"
        proc.Start() |> ignore
        startedProcesses.Add(proc.Id, proc.StartTime) |> ignore
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

    let executeOrFail (program : string) (argsList : string list) workingdir envVars=
        let args =
            argsList
            |> String.concat " "
        let psi = 
            ProcessStartInfo(
                FileName = program, 
                Arguments = args, 
                //UseShellExecute = false,
                WorkingDirectory = workingdir)
        envVars
        |> Seq.iter(fun (kvp : DictionaryEntry) ->
            try
                psi.Environment.Add(kvp.Key |> string,kvp.Value |> string)
            with _ -> ()
        )
        let proc = Process.Start(psi)
        proc.WaitForExit()
        if proc.ExitCode <> 0 then
            failwithf "%s failed with exit code %d" program proc.ExitCode

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
    let (@@) path1 path2 = IO.Path.Combine(path1,path2)
    let inferFrameworkPathOverride () =
        let mscorlib = "mscorlib.dll"
        let possibleFrameworkPaths =
            [
                "/Library/Frameworks/Mono.framework/Versions/4.6.2/lib/mono/4.5/"
                "/usr/local/Cellar/mono/4.6.2.16/lib/mono/4.5/"
                "/usr/lib/mono/4.5/"
            ]
        possibleFrameworkPaths
        |> Seq.tryFind (fun p ->File.Exists(p @@ mscorlib))
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
