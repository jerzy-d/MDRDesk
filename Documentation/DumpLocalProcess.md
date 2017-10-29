#### MDRDesk
[Up](../README.md)  *or*  [Main Menu](../Documentation/MainMenu.md)
## Dump Local Process
The application uses procdump.exe from Microsoft Sysinternals Suite (https://technet.microsoft.com/en-us/sysinternals/bb842062.aspx).
Download it from Sysinternal site if you do not already have one.
It is the best utility to create crash dump process, in my opinion.
The default command is: "<path>*/procdump.exe /ma* <process id> <output folder>".
##### Text box with "/ma" string
This is default procdump.exe options (our favorite), can be changed.
The next text box on the same line display selected process.
It will display a path to a dump after one is created, 
this will be visible when "index after creating" is not checked.
##### "Output Folder..." button.
The folder where the procdump.exe output should go (crash dump file).
The text box on the same line shows MDRDesk's dumps folder path.
By clicking on the button you can select one of its subfolders, or any folder of your choosing.
##### "Procdump Path..." button.
The path to procdump.exe has to be known, it is an item in MDRDesk's application config file,
ex.: key="procdumpfolder" value="C:\bin\SysinternalsSuite".
If app config does not have valid entry for it, click on "Procdump Path..." button, and select procdump.exe file.
The text box on the same line will display the procdump.exe path.
##### "Create Dump" button.
Invokes procdump.exe with our arguments (see above).
If all is successful the path to the dump is displayed in the top/rigth text box,
or if "index after creating" is checked the dialog will close and MDRDesk will index the dump.
##### "index after creating" check box.
If this is checked the dialog will close after successful dump generation, and MDRDesk will index the new dump.
If not checked, you can generate several dumps as the dialog will stay open.
##### "Close" button.
This just closes the dialog.
NOTE. It will uncheck "index after creating" button, so no farther actions are taken.
##### NOTE
The process list is not updated. New procceses started after the dialog is shown are not listed.

