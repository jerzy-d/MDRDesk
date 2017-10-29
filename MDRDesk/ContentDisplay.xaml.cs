using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ClrMDRIndex;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for ContentDisplay.xaml
	/// </summary>
	public partial class ContentDisplay : Window, IValueWindow
    {
		private int _id;
		private bool _wordWrapped;
		private InstanceValue _instanceValue;
        private bool _locked;
        private ValueWindows.WndType _wndType;
        private Image _lockedImg, _unlockedImg;

        public int Id => _id;
        public bool Locked => _locked;
        public ValueWindows.WndType WndType => _wndType;

        public ContentDisplay(int id, string description, InstanceValue instVal, bool locked = true)
		{
            _wndType = ValueWindows.WndType.Content;
            _id = id;
			InitializeComponent();
			_wordWrapped = true;
			_instanceValue = instVal;
            _lockedImg = new Image();
            _lockedImg.Source = ValueWindows.LockedImage.Source;
            _unlockedImg = new Image();
            _unlockedImg.Source = ValueWindows.UnlockedImage.Source;
            UpdateInstanceValue(instVal, description);
            _locked = locked;
            LockBtn.Content = locked ? _lockedImg : _unlockedImg;
        }

        public void UpdateInstanceValue(InstanceValue instVal, string descr)
        {
            ContentInfo.Text = descr;
            ContentValue.Text = instVal.Value.FullContent;
        }


        private void WordWrapButtonClicked(object sender, RoutedEventArgs e)
		{
			if (_wordWrapped)
			{
				ContentValue.TextWrapping = TextWrapping.NoWrap;
				_wordWrapped = false;
			}
			else
			{
				ContentValue.TextWrapping = TextWrapping.Wrap;
				_wordWrapped = true;
			}

		}
		public void Window_Closing(object sender, CancelEventArgs e)
		{
            var wndtask = Task.Factory.StartNew(() => ValueWindows.RemoveWindow(_id, _wndType));
        }
        private void ButtonHelpClicked(object sender, RoutedEventArgs e)
        {
            ValueWindows.ShowHelpWindow(Setup.HelpFolder + System.IO.Path.DirectorySeparatorChar + @"\Documentation\ValueWindows.md");
        }

        private void AddHotKeys()
        {
            try
            {
                RoutedCommand firstSettings = new RoutedCommand();
                firstSettings.InputGestures.Add(new KeyGesture(Key.F1));
                CommandBindings.Add(new CommandBinding(firstSettings, ButtonHelpClicked));
            }
            catch (Exception err)
            {
                //handle exception error
            }
        }

        private void LockBtnClicked(object sender, RoutedEventArgs e)
        {
            if (_locked)
            {
                _locked = false;
                LockBtn.Content = _unlockedImg;
                ValueWindows.ChangeMyLock(_id, _wndType, false);
            }
            else
            {
                _locked = true;
                LockBtn.Content = _lockedImg;
                ValueWindows.ChangeMyLock(_id, _wndType, true);
            }
        }
}
}
