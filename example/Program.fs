// Learn more about F# at http://fsharp.org

open System
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

[<EntryPoint>]
let main argv =
    let client = new System.Net.Http.HttpClient()
    let result = client.GetAsync("https://api.ipify.org/?format=json").Result
    result.Content.ReadAsStringAsync().Result |> printfn "result: %A " 

    if Type.GetType("Mono.Runtime") <> null then
        printfn "Hello World from F# on mono"
    elif Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader") <> null then
        printfn "Hello World from F# on core"
    else
        printfn "Hello World from F# on full"
    let app =
        choose [
            OK "Hello World!"
        ] 
    startWebServer defaultConfig app
    0 // return an integer exit code
