# dotnet-mono

[![NuGet Badge](https://img.shields.io/nuget/vpre/dotnet-mono.svg)](https://www.nuget.org/packages/dotnet-mono/)

## What?
Able to run executables created by dotnet core from full framework on osx/linux

## Why?
In RC4 `dotnet run` _does not_ run executables created from mono.  This tools sets to resolve that.

### Why not just `mono myapp.exe`?
Because I really like `dotnet watch` and wanted to run mono apps with it.

## How


*Now* this tool will attempt to resolve `FrameworkPathOverride` for you.  If these defaults don't work (because of specific folder paths) you can still used the workaround below.


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
USAGE: dotnet-mono [--help] [--project <project>] [--framework <framework>] [--runtime <runtime>] [--configuration <configuration>] [--restore]
                   [--frameworkpathoverride=<frameworkPathOverride>] [--monooptions=<monoOptions>] [--programoptions=<programOptions>]

OPTIONS:

    --project, -p <project>
                          (Optional) Specify path to proj file.  Will default to current directory.
    --framework, -f <framework>
                          (Mandatory) Specify a framework.  Most likely net462.  List available here: https://docs.microsoft.com/en-us/nuget/schema/target-frameworks
    --runtime, -r <runtime>
                          (Optional) Specify a runtime. It will attempt to infer if missing.  List available here:
                          https://github.com/dotnet/docs/blob/master/docs/core/rid-catalog.md
    --configuration, -c <configuration>
                          (Optional) Specify a configuration. (Debug|Release) Will default to debug
    --restore             (Optional) Will attempt dotnet restore
    --frameworkpathoverride=<frameworkPathOverride>
                          (Optional) Set FrameworkPathOverride as Environment Variable or as argument.  It will try to infer based on known good locations on osx/linux.
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

  or with the `dotnet watch` tool to constantly rebuild/run your mono app
  ```
  dotnet watch mono -f net462  -mo="--arch=64 --debug" -po="--help"
  ```

### Killing dotnet mono

Since `dotnet mydevtool` creates a subprocess https://github.com/dotnet/cli/issues/6122 it's best to kill this as a process group rather than just the top level parent. 

* Find dotnet mono parent process, which in the example below is 93625

```
$ ps aux | grep dotnet
myusername        93675   0.0  0.0  2434840    796 s014  S+    6:55PM   0:00.00 grep dotnet
myusername        93658   0.0  0.1   723412  21940 s010  S+    6:55PM   0:00.27 mono /Users/myusername/Documents/GitHub/dotnet-mono/example/bin/Debug/net462/example.exe
myusername        93627   0.0  0.2  4936872  26432 s010  S+    6:54PM   0:00.44 /usr/local/share/dotnet/dotnet exec --depsfile /Users/myusername/.nuget/packages/.tools/dotnet-mono/0.1.4-alpha006/netcoreapp1.0/dotnet-mono.deps.json --additionalprobingpath /Users/myusername/.nuget/packages /Users/myusername/.nuget/packages/dotnet-mono/0.1.4-alpha006/lib/netcoreapp1.0/dotnet-mono.dll -f net462
myusername        93625   0.0  0.4  4958660  60736 s010  S+    6:54PM   0:00.63 dotnet mono -f net462
```
* `kill -INT -93625`
