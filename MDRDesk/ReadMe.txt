File
	Open Report File...
	Exit
Dump
	Crash Dump Required Dac...
	Add Dac File..."
	TryOpenCrashDump
	Recent Files
		Recent Dumps
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

                <MenuItem x:Name="FileReport" HorizontalContentAlignment="Center" HorizontalAlignment="Center" Click="FileReportClicked">
                    <MenuItem.Icon>
                            <Image Source="./Images/WriteToHistory_32x.png" ToolTip="Write detailed report, from current tab." ToolTipService.HasDropShadow="True" Height="32" Width="32" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="16,0,0,0"></Image>
                    </MenuItem.Icon>
                </MenuItem>
                <MenuItem x:Name="FileReportShort" Background="LightBlue" HorizontalContentAlignment="Center" HorizontalAlignment="Center" Click="FileReportShortClicked">
                    <MenuItem.Icon>
                            <Image Source="./Images/WriteToHistory_16x.png" ToolTip="Write short report, from current tab." ToolTipService.HasDropShadow="True" Height="16" Width="16" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="16,0,0,0"></Image>
                    </MenuItem.Icon>
                </MenuItem>
                <MenuItem x:Name="Settings" HorizontalContentAlignment="Center" HorizontalAlignment="Center" Click="SettingsClicked">
                    <MenuItem.Icon>
                            <Image Source="./Images/Settings_32x.png" ToolTip="MDR Desk settings/configuration" ToolTipService.HasDropShadow="True" Height="32" Width="32" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="16,0,0,0"></Image>
                    </MenuItem.Icon>
                </MenuItem>
                    <MenuItem x:Name="ForceGC" Background="LightBlue" HorizontalContentAlignment="Center" HorizontalAlignment="Center" Click="ForceGCClicked">
                    <MenuItem.Icon>
                         <Image Source="./Images/ClearWindowContent_16x.png" ToolTip="Force GC" ToolTipService.HasDropShadow="True" Height="16" Width="16" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="16,0,0,0"></Image>
                    </MenuItem.Icon>
                </MenuItem>
                    <!--
                <Label x:Name="LbCurrentAdHocDump" HorizontalAlignment="Right" Width="Auto" VerticalAlignment="Center" Content="{Binding ElementName=CbAdHocDump, Path=SelectedItem}"></Label>
                -->
             </Menu>
