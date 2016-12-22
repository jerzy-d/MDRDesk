File
	Open Report File...
		Open and display report file, the text files with MDRDesk's custom formatting.
		Some query results tabs can generate these reports, see FileReport or FileReportShort.
	Exit
		Exits application.
Dump
	Crash Dump Required Dac...
	Add Dac File...
	TryOpenCrashDump
	Dump Local Process...
		Shows dialog with the list of the machine running processes.
		NOTE: this list is not updated, so new procceses started after the dialog is shown are not listed.
		It uses procdump.exe from Microsoft Sysinternals Suite (https://technet.microsoft.com/en-us/sysinternals/bb842062.aspx).
		Download it from Sysinternal site if you do not already have one.
		It is the best utility to create crash dump process, in my opinion.
		The default command is: "<path>/procdump.exe /ma <process id> <output folder>".

		1. Text box with "/ma" string.
			This is default procdump.exe options (our favorite), can be changed.
			The next text box on the same line display selected process.
			It will display a path to a dump after one is created, 
			this will be visible when "index after creating" is not checked.
		2. "Output Folder..." button.
			The folder where the procdump.exe output should go (crash dump file).
			The text box on the same line shows MDRDesk's dumps folder path.
			By clicking on the button you can select one of its subfolders, or any folder of your choosing.
		3. "Procdump Path..." button.
			The path to procdump.exe has to be known, it is an item in MDRDesk's application config file,
			ex.: key="procdumpfolder" value="C:\bin\SysinternalsSuite".
			If app config does not have valid entry for it, click on "Procdump Path..." button, and select procdump.exe file.
			The text box on the same line will display the procdump.exe path.
		4. "Create Dump" button.
			Invokes procdump.exe with our arguments (see above).
			If all is successful the path to the dump is displayed in the top/rigth text box,
			or if "index after creating" is checked the dialog will close and MDRDesk will index the dump.
		5. "index after creating" check box.
			If this is checked the dialog will close after successful dump generation, and MDRDesk will index the new dump.
			If not checked, you can generate several dumps as the dialog will stay open.
		6. "Close" button.
			This just closes the dialog.
			NOTE. It will uncheck "index after creating" button, so no farther actions are taken.

Index
	Create Dump Index...
	Open Dump Index...
	Show Loaded Modules Infos
	Show Finalizer Queue
	Show Roots
	Show WeakReference Instances
	Type Sizes Information
	Type Base Sizes Information
	String Usage
	String Usage with GC Generations
	Get Instance Information
		Parent References
		N Parents Reference
		All Parents Reference
		Generation Histogram
		Instance Hierarchy Walk
	Type Sizes Comparison (with other dump)...
	Strings Comparison (with other dump)...
	Recent Indices
Ad-hoc Queries
		Some queries can be done against any crash dump, including the currently opened indexed one.
		These queries are using Microsoft.Diagnostics.Runtime and have no access to extra index information.
		The dumps are apened and closed after displaying results.
		Used for testing, but became a permanent feature as it might help to compare some crash dumps properties.
	String Usage...
	Type Count...
	Collection Content
		Array...

or></Separator>
Name="AhqInstanceRefs" Header="Instance Reference Map">
uItem x:Name="AhqCreateInstanceRefs" Header="Create Instance Reference Map..." Click="AhqCreateInstanceRefsClicked"/>
uItem x:Name="AhqOpenInstanceRefs" Header="Open Instance Reference Map..." Click="AhqOpenInstanceRefsClicked"/>
->

ader="{Binding ElementName=CbAdHocDump, Path=SelectedItem}" >


</Separator>
:Name="RecentAdhocMenuItem" Header="Recent Dumps"/>

FileReport (WriteToHistory_32x.png Visual Studio icon)
FileReportShort (WriteToHistory_16x.png Visual Studio icon)
Settings (Settings_32x.png Visual Studio icon)
ForceGC (ClearWindowContent_16x.png Visual Studio icon)
	Forces GC collection (including LOH compaction) of  MRDDesk application.
	The results are displayed in status bar.
ProcessMemory
	Displays current and max working set sizes of MRDDesk application.
	The values are updated every 5 sec.
LbCurrentAdHocDump
