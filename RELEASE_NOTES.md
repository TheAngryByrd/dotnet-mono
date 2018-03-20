### 0.5.2-alpha001 - 2018-03-06
* BUGFIX: Bump Microsoft.Build nuget to work with dotnet 2.1.101 sdk https://github.com/TheAngryByrd/dotnet-mono/pull/39

### 0.5.1 - 2018-03-06
* BUGFIX: Don't be too aggressive with filtering out -- https://github.com/TheAngryByrd/dotnet-mono/pull/37

### 0.5.0 - 2018-03-05
* FEATURE:Match dotnet run with additional argument syntax (https://github.com/TheAngryByrd/dotnet-mono/pull/35)

### 0.4.0 - 2018-02-27
* FEATURE: Better error handling (https://github.com/TheAngryByrd/dotnet-mono/pull/34)

### 0.3.4 - 2018-01-12
* BUGFIX: Fix msbuild errors (https://github.com/TheAngryByrd/dotnet-mono/pull/28)

### 0.3.3 - 2018-01-12
* BUGFIX: Add Purge System.Net.Http.dll (https://github.com/TheAngryByrd/dotnet-mono/pull/26)

### 0.3.2 - 2017-12-11
* MINOR: Support Platform and potentially other build locations (https://github.com/TheAngryByrd/dotnet-mono/pull/24)

### 0.3.1 - 2017-11-21
* REVERT: [Revert using proj-info](https://github.com/TheAngryByrd/dotnet-mono/pull/21)

#### 0.3.0 - 2017-10-27
* FEATURE: Added --no-restore and --no-build flags (https://github.com/TheAngryByrd/dotnet-mono/pull/19)

#### 0.2.1 - 2017-10-03.
* BUGFIX: added prefercliruntime (https://github.com/TheAngryByrd/dotnet-mono/pull/17)

#### 0.2.0 - 2017-09-26
* FEATURE: netcoreapp2.0 support (https://github.com/TheAngryByrd/dotnet-mono/pull/15)
* MINOR: target lowest FSharp.Core possible : 4.1.17

#### 0.1.6 - 2017-09-04
* MINOR: Better logging (https://github.com/TheAngryByrd/dotnet-mono/pull/13)

#### 0.1.5 - 2017-07-19
* MINOR: Better finding exe logic (https://github.com/TheAngryByrd/dotnet-mono/pull/9)

#### 0.1.4 - 2017-06-18
* BUGFIX: Don't force --restore to be able to run xplat independently of runtime (https://github.com/TheAngryByrd/dotnet-mono/pull/2)
* BUGFIX: Fix child process killing bug (https://github.com/TheAngryByrd/dotnet-mono/pull/5)
* MINOR: Infer FrameworkPathOverride using `which mono` (https://github.com/TheAngryByrd/dotnet-mono/pull/6)

#### 0.1.2 - 2017-05-04
* MINOR: Fix nuget information

#### 0.1.1 - 2017-05-04
* BUGFIX: Use Runtime output folder when running

#### 0.1.0 - 2017-02-04
* Initial release