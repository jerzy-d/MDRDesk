using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MDRDeskInstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            string[] args = e.Args;
            bool install = false;
            if (args != null && args.Length > 0)
            {
                string arg = args[0] != null ? args[0].ToLower() : string.Empty;
                if (arg.IndexOf("install") >= 0)
                    install = true;
            }

            MDRDeskInstaller.MainWindow.Install = install;
        }
    }
}
