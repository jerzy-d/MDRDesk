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
		}

		void closeButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var grid = this.Content as Grid;
			((MainWindow)Application.Current.MainWindow).ClearTabItem(grid);

			this.RaiseEvent(new RoutedEventArgs(CloseTabEvent, this));
		}
	}
}
