open Expecto


/// Sumarry: This is the main entrypoint for the expecto tests. If you create new tests you should add them to either the appropriate child test file or create your own and add it here.
[<EntryPoint>]
let main args =
    runTestsInAssembly defaultConfig args