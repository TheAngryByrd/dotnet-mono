
module dotnet.mono.Tests

open System
open Expecto 
open System.Xml.Linq
open DotnetMono
open DotnetMono.Main
open DotnetMono

module Expect =
    let stringNotContains (subject : string) (substring : string) message =
      if (subject.Contains(substring)) then
        Tests.failtestf "%s. Expected subject string '%s' to not contain substring '%s'."
                        message subject substring

let configWithRedirectAlready = 
    """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Runtime" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.1.1.0" newVersion="4.1.1.0" />
      </dependentAssembly>
    </assemblyBinding>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Security.Cryptography.X509Certificates" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.1.1.0" newVersion="4.1.1.0" />
      </dependentAssembly>
    </assemblyBinding>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.IO" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.1.1.0" newVersion="4.1.1.0" />
      </dependentAssembly>
    </assemblyBinding>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="FSharp.Core" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.4.1.0" newVersion="4.4.1.0" />
      </dependentAssembly>
    </assemblyBinding>

    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Net.Http" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-999.999.999.999" newVersion="4.2.0.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>"""


let deleteAssemblyRedirect =
    testCase "Modify Assembly Redirect for System.Net.Http.dll" <| fun () -> 
        let configWithRedirectAlreadyXDocument = configWithRedirectAlready |> XDocument.Parse

        DotnetMono.SystemNetHttpFixer.reconfigureAssemblyRedirect configWithRedirectAlreadyXDocument

        // Look for specific strings?
        let expected =
            """<assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Net.Http" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-999.999.999.999" newVersion="4.0.0.0" />
      </dependentAssembly>
    </assemblyBinding>"""

        let notExpected =
            """<assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Net.Http" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-999.999.999.999" newVersion="4.2.0.0" />
      </dependentAssembly>
    </assemblyBinding>"""

        let actual = configWithRedirectAlreadyXDocument.ToString()

        Expect.stringContains actual expected "Should modify assembly binding"
        Expect.stringNotContains actual notExpected "Should modify assembly binding"
        ()


let configWithoutRedirect = 
    """<?xml version="1.0" encoding="utf-8"?>
    <configuration>
      <runtime>
        <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
          <dependentAssembly>
            <assemblyIdentity name="System.Runtime" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
            <bindingRedirect oldVersion="0.0.0.0-4.1.1.0" newVersion="4.1.1.0" />
          </dependentAssembly>
        </assemblyBinding>
        <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
          <dependentAssembly>
            <assemblyIdentity name="System.Security.Cryptography.X509Certificates" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
            <bindingRedirect oldVersion="0.0.0.0-4.1.1.0" newVersion="4.1.1.0" />
          </dependentAssembly>
        </assemblyBinding>
        <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
          <dependentAssembly>
            <assemblyIdentity name="System.IO" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
            <bindingRedirect oldVersion="0.0.0.0-4.1.1.0" newVersion="4.1.1.0" />
          </dependentAssembly>
        </assemblyBinding>
        <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
          <dependentAssembly>
            <assemblyIdentity name="FSharp.Core" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
            <bindingRedirect oldVersion="0.0.0.0-4.4.1.0" newVersion="4.4.1.0" />
          </dependentAssembly>
        </assemblyBinding>
      </runtime>
    </configuration>"""


let appendAssemblyRedirect =
    testCase "Append Assembly Redirect for System.Net.Http.dll" <| fun () ->
        let configWithoutRedirectXDocument = configWithoutRedirect |> XDocument.Parse


        DotnetMono.SystemNetHttpFixer.reconfigureAssemblyRedirect configWithoutRedirectXDocument

        // XML Equality?

        // Look for specific strings?
        let expected =
            """<assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Net.Http" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-999.999.999.999" newVersion="4.0.0.0" />
      </dependentAssembly>
    </assemblyBinding>"""

        let actual = configWithoutRedirectXDocument.ToString()

        Expect.stringContains actual expected "Contains new assembly binding"
        
     


let deleteDllTest = 
    testCase "Delete System.Net.Http.dll" <| fun () -> ()
    

[<Tests>]
let SystemNetHttpPurgeTests =
    testList "SystemNetHttpPurgeTests" [
        deleteDllTest
        appendAssemblyRedirect
        deleteAssemblyRedirect
    ]

type DoubleDashParmaters =
  {
    Argv : string array
    ExpectedBeforeDoubleDash : string array
    ExpectedAfterDoubleDash : string array
  }
    with
      static member Create (args : string) before after = 
        {Argv = args.Split(" ");  ExpectedBeforeDoubleDash = before; ExpectedAfterDoubleDash =after}
      
let doubleDashTestGenerator =
  [
    DoubleDashParmaters.Create "--help" [|"--help"|] [||]
    DoubleDashParmaters.Create "--help -- --logger-level Warn" [|"--help"|] [|"--logger-level"; "Warn"|]
    DoubleDashParmaters.Create "-- --real-parameter -- --help" [||] [|"--real-parameter"; "--" ; "--help"|]
    DoubleDashParmaters.Create "--framework net462 -- --real-parameter -- --help" [|"--framework"; "net462"|] [|"--real-parameter"; "--" ; "--help"|]
    
  ]
  |> Seq.mapi (fun index item ->
      testCase (sprintf "double dash - %i" index) <| fun () ->
            let actual1, actual2 = Main.splitArgs item.Argv
            Expect.sequenceEqual actual1 item.ExpectedBeforeDoubleDash ""
            Expect.sequenceEqual actual2 item.ExpectedAfterDoubleDash ""
  )



[<Tests>]
let ArgumentParsingTests =
    testList "Argument Parsing Tests" [
      yield! doubleDashTestGenerator
    ]


// Set MSBUILD_EXE_PATH
// Too painful to set programatically
// [<Tests>]
let MsBuildTests = 
  testList "MSBuild tests" [
    testCase "RunCommand with Runtime" <| fun () ->
      let projPath = "testProj1/testProj1.fsproj"
      let props = globalProperties "Debug" "net461" (Some "osx-x64")
      let result = Microsoft.Build.Evaluation.Project(projPath, props, null)
      let runCmd = result.GetPropertyValue("RunCommand") |> string

      Expect.stringEnds runCmd "/testProj1/bin/Debug/net461/osx-x64/Foobar.exe" "Should have configuration, targetframework, and runtime in run path"
    testCase "RunCommand without Runtime" <| fun () ->
      let projPath = "testProj1/testProj1.fsproj"
      let props = globalProperties "Debug" "net461" None
      let result = Microsoft.Build.Evaluation.Project(projPath, props, null)
      let runCmd = result.GetPropertyValue("RunCommand") |> string

      Expect.stringEnds runCmd "/testProj1/bin/Debug/net461/Foobar.exe" "Should have configuration, and targetframework in run path"
  ]