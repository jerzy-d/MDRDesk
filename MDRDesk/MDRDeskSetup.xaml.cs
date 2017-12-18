using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClrMDRIndex;
using System.Windows.Input;
using System;

// ReSharper disable InconsistentNaming
namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for MDRDeskSetup.xaml
    /// </summary>
    public partial class MDRDeskSetup
    {
        private string _dacFolder;
        private string _dumpFolder;
        private string _procdumpFolder;

        public MDRDeskSetup()
        {
            InitializeComponent();
            AddHotKeys();

            TxtBoxDumpFolder.Text = Setup.DumpsFolder;
            TxtBoxDacFolder.Text = Setup.DacFolder;
            TxtBoxProcdumpPath.Text = Setup.ProcDumpFolder;
            switch (Setup.TypesDisplayMode.ToLower())
            {
                case "namespaces":
                    TypeDisplayNamespaceClass.IsChecked = true;
                    break;
                case "types":
                    TypeDisplayClass.IsChecked = true;
                    break;
                case "fulltypenames":
                    TypeDisplayNamespace.IsChecked = true;
                    break;
                default:
                    TypeDisplayNamespace.IsChecked = true;
                    break;
            }
            if (Setup.SkipReferences) IndexInstanceReferences.IsChecked = true;
            if (Setup.SkipDeadStackObjects) IndexDeadStackObjects.IsChecked = true;
        }

        private void ButtonDacFolder_OnClick(object sender, RoutedEventArgs e)
        {
            if (GetFolder(TxtBoxDacFolder, ref _dacFolder))
                Setup.SetDacFolder(_dacFolder);
        }

        private void ButtonDumpFolder_OnClick(object sender, RoutedEventArgs e)
        {
            if (GetFolder(TxtBoxDumpFolder, ref _dumpFolder))
                Setup.SetDumpsFolder(_dumpFolder);
        }

        private void ButtonProcdump_OnClick(object sender, RoutedEventArgs e)
        {
            if (GetFolder(TxtBoxProcdumpPath, ref _procdumpFolder))
                Setup.SetProcdumpFolder(_procdumpFolder);
        }

        private bool GetFolder(TextBox txtBox, ref string folder)
        {
            string currentPath = txtBox.Text.Trim();
            if (!Directory.Exists(currentPath))
                currentPath = null;
            currentPath = GuiUtils.GetFolderPath(currentPath, null, this);
            if (Directory.Exists(currentPath))
            {
                txtBox.Text = currentPath;
                folder = currentPath;
                return true;
            }
            return false;
        }

        private bool IsTrue(RadioButton btn)
        {
            if (btn.IsChecked != null)
                return (bool)btn.IsChecked;
            return false;
        }

        private bool IsTrue(CheckBox btn)
        {
            if (btn.IsChecked != null)
                return (bool)btn.IsChecked;
            return false;
        }

        private void ButtonSaveSetup_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsTrue(TypeDisplayNamespaceClass))
                Setup.SetTypesDisplayMode("namespaces");
            else if (IsTrue(TypeDisplayClass))
                Setup.SetTypesDisplayMode("types");
            else if (IsTrue(TypeDisplayNamespace))
                Setup.SetTypesDisplayMode("fulltypenames");
            else
                Setup.SetTypesDisplayMode("namespaces");
            Setup.SetSkipIndexingRefs(IsTrue(IndexInstanceReferences));
            Setup.SetSkipDeadStackObjects(IsTrue(IndexDeadStackObjects));

            string error;
            Setup.SaveConfigSettings(out error);
            DialogResult = true;
            Close();
        }

        private void ButtonCloseSetup_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ButtonHelpClicked(object sender, RoutedEventArgs e)
        {
            ValueWindows.ShowHelpWindow(Setup.HelpFolder + Path.DirectorySeparatorChar + @"\Documentation\Configuration.md");
        }

        private void AddHotKeys()
        {
            try
            {
                RoutedCommand firstSettings = new RoutedCommand();
                firstSettings.InputGestures.Add(new KeyGesture(Key.F1));
                CommandBindings.Add(new CommandBinding(firstSettings, ButtonHelpClicked));
            }
            catch (Exception ex)
            {
                GuiUtils.ShowError(ex, this);
            }
        }
    }
}
// ReSharper restore InconsistentNaming
