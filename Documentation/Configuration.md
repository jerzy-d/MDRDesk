# MDRDesk
[Up](../README.md)
## Configuration/Setup
##### Dac Folder...
To debug a crash dump we need a matching mscordacwks.dll.
TODO JRD -- add more stuff here.
##### Dumps Folder...
The folder in which you can keep your crash dumps. When a dump is indexed
the new directory is created (crash dump file name).map.
As we handling a lot of them we found useful keeping them in one place.
You can have subdirectories there to organize your crash data.
##### Procdump.exe Folder...
The application uses procdump.exe from Microsoft Sysinternals Suite (https://technet.microsoft.com/en-us/sysinternals/bb842062.aspx).
Download it from Sysinternal site if you do not already have one.
Enter or browse to the folder containing procdump.exe to set this property.
##### Type Display Mode
How list of types are displayed.
TODO JRD -- add more stuff here.
##### Skip indexing references
In some crash dumps (the big ones or when taken during gc collection) indexing references
can take a very long time. If you find that annoying you can skip that action by checking the yes
button. Be aware that by doing so to disable a lot of features of MDRDesk.
