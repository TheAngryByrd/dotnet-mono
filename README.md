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
  OPTIONS:

    --project, -p <project>
                          specify a project.  Will default to current folder
    --framework, -f <framework>
                          specify a framework.
    --runtime, -r <runtime>
                          specify a runtime.
    --configuration, -c <configuration>
                          specify a configuration. Will default to debug
    --restore             will attempt dotnet restore
    --monooptions, -mo=<monoOptions>
                          mono flags
    --programoptions, -po=<programOptions>
                          program flags
    --help                display this list of options.
  ```
  ### Usage
  ```
  dotnet mono -f net462  -mo="--arch=64 --debug" -po="--help"
  ```