# MDRDesk
## Microsoft.Diagnostics.Runtime Gui Interface

[MDRDesk Github site](https://github.com/jerzy-d/MDRDesk)  

1. [Credits](./Documentation/Credits.md)

2. [Getting Started](./Documentation/GettingStarted.md) - A brief introduction
   to the API and how to create a CLRRuntime instance.

3. [The Main Menu](./Documentation/MainMenu.md) - Basic operations
   like enumerating AppDomains, Threads, the Finalizer Queue, etc.

### Installation

_I_ _do_ _not_ _have_ _msi_ _installer_, _and_ _not_ _planning_ _to_ _have_ _one_.
_Upgrades_ _are_ _handled_ _by_ _MDRDesk_, _in_ _the_ _main_ _menu_ : _Help_ -> _Update_ _MDRDesk_.

##### Warnig. MDRDesk requires .NET Framework 4.7.1.  
You can download it from : https://www.microsoft.com/net/download/dotnet-framework-runtime,
if it's not installed on your computer.

Create a directory on your machine in location of your choosing, say 'd:\MDRDesk'.
Optionally you can create a subfolder in this directory called 'Dumps' where you can
keep crash dumps you inspecting. We have under 'Dumps' directory subfolders for each client we supporting.
Download MDRDesk.zip, and mscordacwks.zip from the release to 'MDRDesk' folder.
Unzip both and you should be all set.
To create crash dumps of local processes from MDRDesk, you need to setup procdump.exe location.

TODO - local process crash dumps creation.
