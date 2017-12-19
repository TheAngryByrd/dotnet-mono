
module dotnet.mono.Tests

open Expecto 
open System.Xml.Linq

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