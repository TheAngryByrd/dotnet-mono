# dotnet-mono

[![NuGet Badge](https://img.shields.io/nuget/vpre/dotnet-mono.svg)](https://www.nuget.org/packages/dotnet-mono/)

## What is dotnet-mono?
This a [dotnet tool](https://docs.microsoft.com/en-us/dotnet/core/tools/?tabs=netcore2x) that allows you to run executables created by the new dotnet core sdk project format that target full framework (net45-net462) on osx/linux.

## Why would you create this?
Since RC4 of the dotnet tooling, `dotnet run` [does not run](https://github.com/dotnet/cli/issues/6043) executables [created from mono](https://github.com/dotnet/sdk/issues/335).  This tools sets to resolve that.

### Why not just `mono myapp.exe`?
Because I really like [dotnet watch](https://github.com/aspnet/DotNetTools/tree/dev/src/Microsoft.DotNet.Watcher.Tools) which watches for changes in files and reruns the specific dotnet command you give.  Since `dotnet run` doesn't run mono apps, I wanted a way to get continuous feedback when targeting mono as an executable.

## How to install

Add this element your csproj/fsproj/vbproj.

```
  <ItemGroup>
    <DotNetCliToolReference Include="dotnet-mono" Version="*" />
  </ItemGroup>
```

or use [paket to add clitool](https://fsprojects.github.io/Paket/nuget-dependencies.html#Special-case-CLI-tools) via paket.depedencies 

```
clitool dotnet-mono
```


### Options

```
$ dotnet mono --help

Much like dotnet-run you can specify dotnet mono [options] [[--] <additional arguments>...]] to pass arguments to the underlying program.
USAGE: dotnet-mono [--help] [--project <project>] [--framework <framework>] [--runtime <runtime>] [--inferruntime] [--configuration <configuration>] [--restore] [--no-restore]
                   [--frameworkpathoverride=<frameworkPathOverride>] [--monooptions=<monoOptions>] [--programoptions=<programOptions>] [--loggerlevel <logLevel>] [--no-build] [--purge-system-net-http]

OPTIONS:

    --project, -p <project>
                          (Optional) Specify path to proj file.  Will default to current directory.
    --framework, -f <framework>
                          (Mandatory) Specify a framework.  Most likely net462.  List available here: https://docs.microsoft.com/en-us/nuget/schema/target-frameworks
    --runtime, -r <runtime>
                          (Optional) Specify a runtime. List available here: https://github.com/dotnet/docs/blob/master/docs/core/rid-catalog.md.  You will probably either need to run dotnet restore
                          properly with runtime or pass --restore.
    --inferruntime        (Optional) Try to run explicitly on the current runtime. You will probably either need to run dotnet restore properly with runtime or pass --restore.
    --configuration, -c <configuration>
                          (Optional) Specify a configuration. (Debug|Release|Others) Will default to Debug
    --restore             (Optional) Will attempt dotnet restore
    --no-restore          (Optional) Will pass --no-restore to dotnet build.
    --frameworkpathoverride=<frameworkPathOverride>
                          (Optional) Set FrameworkPathOverride as Environment Variable or as argument.  It will try to infer based on known good locations on osx/linux.
    --monooptions, -mo=<monoOptions>
                          (Optional) Flags to be passed to mono.
    --programoptions, -po=<programOptions>
                          (Optional) Flags to be passed to running exe.
    --loggerlevel <logLevel>
                          (Optional) LogLevel for dotnet-mono defaults to Info (Verbose|Debug|Info|Warn|Error|Fatal)
    --no-build            (Optional) Will attempt to skip dotnet build.
    --purge-system-net-http
                          (Optional) Mono has issues with HttpClient noted here: https://github.com/dotnet/corefx/issues/19914 ...This will attempt to resolve them.
    --help                display this list of options.

```

### Example Usage

Currently dotnet tooling must be used from the same directory you put the reference in your project files.

```
cd src/MyCoolMonoApp/
dotnet mono -f net462  -mo="--arch=64 --debug" -po="--help"
```

or with the `dotnet watch` tool to constantly rebuild/run your mono app
```
dotnet watch mono -f net462  -mo="--arch=64 --debug" -po="--help"
```


# FAQ

#### When should I use dotnet-mono?
When you want to be able to run mono applications (your project must produce an exe) with the dotnet cli tooling and not have to figure out where your executable was output.


#### Can I just dotnet build?
If you set `export FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.5/` in your `build.sh`or in your `.bashrc` or `.bash_profile` you can just do `dotnet build` without this tool. See: https://github.com/dotnet/netcorecli-fsc/wiki/.NET-Core-SDK-1.0.1#using-net-framework-as-targets-framework-the-osxunix-build-fails Additionally you can set other project properties see https://github.com/dotnet/sdk/issues/335#issuecomment-368669050. 


#### How do I run my tests or how about a `dotnet test` equivalent?
Unfortunately this is in a [very](https://github.com/Microsoft/vstest/issues/1284) [poor](https://github.com/Microsoft/vstest/issues/445#issuecomment-290723152) [state](https://github.com/dotnet/cli/issues/3073) for mono.  If you're using `xunit`, I added mono support to [dotnet-xunit](https://www.nuget.org/packages/dotnet-xunit/2.3.1) and you can use that dotnet tool to run the tests instead.  If you're using `Expecto` since it's just a console app you can run it.  I'm not sure about other test runners.


----


### A word on FrameworkPathOverride

*Now* this tool will attempt to resolve `FrameworkPathOverride` for you.  If these defaults don't work (because of specific folder paths) you can still used the workaround below.


To workaround https://github.com/dotnet/sdk/issues/335 you'll need to  set `FrameworkPathOverride` environment variable to use .net framework assemblies installed by mono

  Find where `.../mono/4.5/mscorlib.dll` is on your machine and set `FrameworkPathOverride` as an environment variable

  - Best overall method

    ```
    export FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.5/
    ```
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
