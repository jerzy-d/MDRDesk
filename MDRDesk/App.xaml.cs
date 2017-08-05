using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClrMDRIndex;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
//		AppDomain
//		// somewhere early in App.xaml.cs
//		AppDomain.CurrentDomain.UnhandledException += (s, e) => LogUnhandledException((Exception) e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

//DispatcherUnhandledException += (s, e) => 

//	LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

//		TaskScheduler.UnobservedTaskException += (s, e) => 

//	LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");



//		// ... and the actual logging (depends on your logging library)
//		private void LogUnhandledException(Exception exception, string @event)
//		{
//			_log.Exception(exception)
//				.Data("Event", @event)
//				.Fatal("Unhandled exception");

//			// wait until the logmanager has written the entry
//			_log.LogManager.FlushEntriesAsOf(DateTimeOffset.Now.AddSeconds(1));
//		}

		public bool DoHandle { get; set; }
		private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{

			if (this.DoHandle)
			{
                //Handling the exception within the UnhandledException handler.
                if (e.Exception != null)
                {
                    GuiUtils.ShowError(Utils.GetExceptionErrorString(e.Exception, "[APPLICATION WILL CLOSE] "), GuiUtils.MainWindowInstance);
                }
                else
                {
                    MessageBox.Show("Application is going to close! ", "Exception caught.");
                }
                e.Handled = false;
			}
			else
			{
                //If you do not set e.Handled to true, the application will close due to crash.
                if (e.Exception != null)
                {
                    GuiUtils.ShowError(Utils.GetExceptionErrorString(e.Exception, "[APPLICATION WILL CLOSE] "), GuiUtils.MainWindowInstance);
                }
                else
                {
                    MessageBox.Show("Application is going to close! ", "Uncaught Exception");
                }
                e.Handled = false;
			}
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
			System.Threading.Tasks.TaskScheduler.UnobservedTaskException +=
				new EventHandler<UnobservedTaskExceptionEventArgs>(Task_UnobservedException);
		}

		void Task_UnobservedException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			MessageBox.Show(e.Exception.Message, "Uncaught Thread Exception", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = e.ExceptionObject as Exception;
			MessageBox.Show(ex.Message, "Uncaught Thread Exception", MessageBoxButton.OK, MessageBoxImage.Error);
		}

	}
}
