// Learn more about F# at http://fsharp.org

open System

[<EntryPoint>]
let main argv =
    if Type.GetType("Mono.Runtime") <> null then
        printfn "Hello World from F#! on mono"
    else
        printfn "Hello World from F#! on core?"
    //Threading.Thread.Sleep(100000)
    0 // return an integer exit code
