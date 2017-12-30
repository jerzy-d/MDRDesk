using System.Windows;
using System.Windows.Controls;

namespace MDRDesk
{
	public class CloseableTabItem : TabItem
	{
		static CloseableTabItem()
		{
			//This OverrideMetadata call tells the system that this element wants to provide a style that is different than its base class.
			//This style is defined in themes\generic.xaml
			DefaultStyleKeyProperty.OverrideMetadata(typeof(CloseableTabItem),
				new FrameworkPropertyMetadata(typeof(CloseableTabItem)));
		}

		public static readonly RoutedEvent CloseTabEvent =
			EventManager.RegisterRoutedEvent("CloseTab", RoutingStrategy.Bubble,
				typeof(RoutedEventHandler), typeof(CloseableTabItem));

		public event RoutedEventHandler CloseTab
		{
			add { AddHandler(CloseTabEvent, value); }
			remove { RemoveHandler(CloseTabEvent, value); }
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			Button closeButton = base.GetTemplateChild("PART_Close") as Button;
			if (closeButton != null)
				closeButton.Click += new System.Windows.RoutedEventHandler(closeButton_Click);
 			var contextMenu = new ContextMenu();
			MenuItem menuItem = new MenuItem() { Header = "Change Tab Header/Title" };
            menuItem.Click += ChangeTabHeader;
            contextMenu.Items.Add(menuItem);
            this.Header = new ContentControl
            {
                Content = this.Header.ToString(),
                ContextMenu = contextMenu
            };
		}

		private void ChangeTabHeader(object sender, RoutedEventArgs e)
		{
			InputStringDlg dlg = new InputStringDlg("Enter new header/title. Headers longer that 32 characters will be truncated.", this.Header.ToString()) { Title = "Change Tab Header", Owner = Window.GetWindow(this) };
			var dlgResult = dlg.ShowDialog();
			if (dlg.DialogResult != null && (!dlg.DialogResult.Value || string.IsNullOrWhiteSpace(dlg.Answer))) return;
			var str = dlg.Answer.Trim();
			dlg.Close();
			dlg = null;
			if (string.IsNullOrWhiteSpace(str)) return;
			if (str.Length > 32)
				str = str.Substring(0, 31) + '\u2026';
			this.Header = str;
		}

		void closeButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var grid = this.Content as Grid;
			((MainWindow)Application.Current.MainWindow).ClearTabItem(grid);
			this.RaiseEvent(new RoutedEventArgs(CloseTabEvent, this));
		}

        public void Close()
        {
            var grid = this.Content as Grid;
            ((MainWindow)Application.Current.MainWindow).ClearTabItem(grid);
            this.RaiseEvent(new RoutedEventArgs(CloseTabEvent, this));
        }
    }
}
