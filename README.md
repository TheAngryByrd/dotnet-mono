# dotnet-mono

## What?
Able to run executables created by dotnet core from full framework on osx/linux

## Why?
In RC4 `dotnet run` _does not_ run executables created from mono.  This tools sets to resolve that.

## How
To workaround https://github.com/dotnet/sdk/issues/335 you'll need to  set `FrameworkPathOverride` environment variable to use .net framework assemblies installed by mono

  Find where `.../mono/4.5/mscorlib.dll` is on your machine and set `FrameworkPathOverride` as an environment variable
  - OSX (assuming mono install with xamarin studio): 

    ```
    export FrameworkPathOverride=/Library/Frameworks/Mono.framework/Versions/4.6.2/lib/mono/4.5/
    ```
  - OSX (assuming mono installed with brew): 

    ```
    export FrameworkPathOverride=/usr/local/Cellar/mono/4.6.2.16/lib/mono/4.5/
    ```
  - Debian: 

    ```
    export FrameworkPathOverride=/usr/lib/mono/4.5/
    ``` 
  
  ### Options
  ```
 USAGE: dotnet-mono [--help] [--project <project>] [--framework <framework>] [--runtime <runtime>]
                   [--configuration <configuration>] [--restore] [--monooptions=<monoOptions>]
                   [--programoptions=<programOptions>]

OPTIONS:

    --project, -p <project>
                          (Optional) Specify path to proj file.  Will default to current directory.
    --framework, -f <framework>
                          (Mandatory) Specify a framework.  Most likely net462.  List available here:
                          https://docs.microsoft.com/en-us/nuget/schema/target-frameworks
    --runtime, -r <runtime>
                          (Optional) Specify a runtime. It will attempt to infer if missing.  List available here:
                          https://github.com/dotnet/docs/blob/master/docs/core/rid-catalog.md
    --configuration, -c <configuration>
                          (Optional) Specify a configuration. (Debug|Release) Will default to debug
    --restore             (Optional) Will attempt dotnet restore
    --monooptions, -mo=<monoOptions>
                          (Optional) Flags to be passed to mono.
    --programoptions, -po=<programOptions>
                          (Optional) Flags to be passed to running exe.
    --help                display this list of options.
  ```
  ### Example Usage
  ```
  dotnet mono -f net462  -mo="--arch=64 --debug" -po="--help"
  ```