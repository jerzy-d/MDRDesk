using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ClrMDRIndex;
using Microsoft.Diagnostics.Runtime;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for HexView.xaml
	/// </summary>
	public partial class HexView
	{
		private enum DisplayMode
		{
			JustNumbers,
			Ascii,
			Unicode,
		}

		private const int MinBufSize = 32;
		private const int MaxBufSize = 1024*32;

		private int _id;
		private ConcurrentDictionary<int, Window> _wndDct;
		private ClrtDump _clrtDump;
		private ClrHeap _heap;
		private ulong _currentAddr;
		private byte[] _bytes;
		private int _colCount = 8;
		private int _wordLenght = 4;
		private int _byteBufSize = 2048;
		private DisplayMode _displayMode = DisplayMode.JustNumbers;

		public HexView(int id, ConcurrentDictionary<int, Window> wndDct, ClrtDump clrtDump, ulong addr)
		{
			_id = id;
			_wndDct = wndDct;
			_clrtDump = clrtDump;
			_currentAddr = Utils.RealAddress(addr);
			// round down to 8 boundary
			_currentAddr = (_currentAddr/8UL)*8UL;

			InitializeComponent();

			WordSizeLabel.Content = "word size: " + _wordLenght;
			ColumnCountLabel.Content = "col count: " + _colCount;
			_heap = _clrtDump.Heap;
			_bytes = new byte[_byteBufSize];

			_wndDct.TryAdd(id, this);
		}

		public bool Init( out string error)
		{
			if (!ReadMemory(out error))
			{
				return false;
			}
			DisplayModePlain.IsChecked = true; // this will populate HexViewContent.Text
			DisplayAddressRange();
			DisplayBufferSize();
			return true;
		}

		private string MemoryStringPlain(byte[] bytes, ulong addr)
		{
			StringBuilder sb = new StringBuilder(4095);
			string wordFormat = "x" + (_wordLenght*2).ToString();
			int col = 0;
			ulong curAddr = addr;
			int offfset = 0;
			for (int i = 0, icnt = bytes.Length; i < icnt; i += _wordLenght, ++col)
			{
				if (col == 0)
				{
					sb.Append(string.Format("{0:x14} | ", curAddr));
					curAddr += (ulong) (_colCount*_wordLenght);
				}
				string valStr = null;
				switch (_wordLenght)
				{
					case 1:
						valStr = bytes[offfset].ToString(wordFormat);
						break;
					case 2:
						valStr = BitConverter.ToUInt16(bytes, offfset).ToString(wordFormat);
						break;
					case 4:
						valStr = BitConverter.ToUInt32(bytes, offfset).ToString(wordFormat);
						break;
					case 8:
						valStr = BitConverter.ToUInt64(bytes, offfset).ToString(wordFormat);
						break;
					case 16:
						break;
				}
				offfset += _wordLenght;
				sb.Append(valStr).Append(" ");
				if (col < _colCount) continue;
				sb.AppendLine();
				col = -1;
			}
			if (col <= 0) return sb.ToString();

			// did not go to the end of the last line
			//
			string padding = new string('?', _wordLenght*2);
			while (col <= _colCount)
			{
				sb.Append(padding).Append(" ");
				++col;
			}
			sb.AppendLine();
			return sb.ToString();
		}

		private string MemoryString()
		{
			switch (_displayMode)
			{
				case DisplayMode.Unicode:
					return MemoryStringUnicode(_bytes, _currentAddr);
				case DisplayMode.Ascii:
					return MemoryStringAscii(_bytes, _currentAddr);
				default:
					return MemoryStringPlain(_bytes, _currentAddr);
			}
		}

		private string MemoryStringAscii(byte[] bytes, ulong addr)
		{
			StringBuilder sb = new StringBuilder(4095);
			string wordFormat = "x2";
			int col = 0;
			ulong curAddr = addr;
			int offfset = 0;
			StringBuilder asb = new StringBuilder(2*_colCount);
			for (int i = 0, icnt = bytes.Length; i < icnt; ++i, ++col)
			{
				if (col == 0)
				{
					sb.Append(string.Format("{0:x14} | ", curAddr));
					curAddr += (ulong) (_colCount*_wordLenght);
				}
				string valStr = bytes[offfset].ToString(wordFormat);
				sb.Append(valStr).Append(" ");
				var asciiChar = Convert.ToChar(_bytes[offfset]);
				if (Char.IsWhiteSpace(asciiChar)) asciiChar = '.';
				asb.Append(asciiChar);
				++offfset;
				if (col < _colCount) continue;
				sb.Append(" | ").AppendLine(asb.ToString());
				asb.Clear();
				col = -1;
			}
			if (col <= 0) return sb.ToString();

			// did not go to the end of the last line
			//
			while (col <= _colCount)
			{
				sb.Append("?? ");
				asb.Append(".");
				++col;
			}
			sb.Append(" | ").AppendLine(asb.ToString());
			return sb.ToString();
		}

		private string MemoryStringUnicode(byte[] bytes, ulong addr)
		{
			StringBuilder sb = new StringBuilder(4095);
			string wordFormat = "x4";
			int col = 0;
			ulong curAddr = addr;
			int offfset = 0;
			StringBuilder asb = new StringBuilder(4*_colCount);
			for (int i = 0, icnt = bytes.Length; i < icnt; i += 2, ++col)
			{
				if (col == 0)
				{
					sb.Append(string.Format("{0:x14} | ", curAddr));
					curAddr += (ulong) (_colCount*_wordLenght);
				}
				string valStr = bytes[offfset].ToString(wordFormat);
				sb.Append(valStr).Append(" ");
				ushort charValue = bytes[offfset + 1];
				charValue <<= 8;
				charValue |= bytes[offfset];
				var unicodeChar = Convert.ToChar(charValue);
				if (Char.IsWhiteSpace(unicodeChar)) unicodeChar = '.';
				asb.Append(unicodeChar);
				offfset += 2;
				if (col < _colCount) continue;
				sb.Append(" | ").AppendLine(asb.ToString());
				asb.Clear();
				col = -1;
			}
			if (col <= 0) return sb.ToString();

			// did not go to the end of the last line
			//
			while (col <= _colCount)
			{
				sb.Append("???? ");
				asb.Append(".");
				++col;
			}
			sb.Append(" | ").AppendLine(asb.ToString());
			return sb.ToString();
		}

		public void Window_Closing(object sender, CancelEventArgs e)
		{
			_clrtDump.Dispose();
			Window wnd;
			_wndDct.TryRemove(_id, out wnd);
		}

		private void GotoNextButton_OnClick(object sender, RoutedEventArgs e)
		{
			string error;
			var lastCurrentAddr = _currentAddr;
			_currentAddr += (ulong) (_bytes.Length - _wordLenght*_colCount);
			if (!ReadMemory(out error))
			{
				MessageBox.Show(error, "FAILED TO READ MEMORY", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			_currentAddr = lastCurrentAddr;
			HexViewContent.Text = MemoryString();
			DisplayAddressRange();
		}

		private void GotoPreviousButton_OnClick(object sender, RoutedEventArgs e)
		{
			string error;
			var lastCurrentAddr = _currentAddr;
			_currentAddr -= (ulong) (_bytes.Length - _wordLenght*_colCount);
			if (!ReadMemory(out error))
			{
				MessageBox.Show(error, "FAILED TO READ MEMORY", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			_currentAddr = lastCurrentAddr;
			HexViewContent.Text = MemoryString();
			DisplayAddressRange();
		}

		private void IncWordSizeButton_OnClick(object sender, RoutedEventArgs e)
		{
			if (_wordLenght > 8) return;
			_wordLenght *= 2;
			WordSizeLabel.Content = "word size: " + _wordLenght;
		}

		private void DecWordSizeButton_OnClick(object sender, RoutedEventArgs e)
		{
			if (_wordLenght < 2) return;
			_wordLenght /= 2;
			WordSizeLabel.Content = "word size: " + _wordLenght;
		}

		private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
		{
			HexViewContent.Text = MemoryString();
			DisplayAddressRange();
		}

		private void IncColCountButton_OnClick(object sender, RoutedEventArgs e)
		{
			if (_colCount > 16) return;
			++_colCount;
			ColumnCountLabel.Content = "col count: " + _colCount;
		}

		private void DecColCountButton_OnClick(object sender, RoutedEventArgs e)
		{
			if (_colCount < 2) return;
			--_colCount;
			ColumnCountLabel.Content = "col count: " + _colCount;
		}

		private void DisplayModePlain_OnChecked(object sender, RoutedEventArgs e)
		{
			RadioButton button = sender as RadioButton;
			Debug.Assert(button != null);
			if (button.IsChecked == null || !button.IsChecked.Value) return;
			_displayMode = DisplayMode.JustNumbers;
			HexViewContent.Text = MemoryString();
		}

		private void DisplayModeAscii_OnChecked(object sender, RoutedEventArgs e)
		{
			RadioButton button = sender as RadioButton;
			Debug.Assert(button != null);
			if (button.IsChecked == null || !button.IsChecked.Value) return;
			_displayMode = DisplayMode.Ascii;
			HexViewContent.Text = MemoryString();
		}

		private void DisplayModeUnicode_OnChecked(object sender, RoutedEventArgs e)
		{
			RadioButton button = sender as RadioButton;
			Debug.Assert(button != null);
			if (button.IsChecked == null || !button.IsChecked.Value) return;
			_displayMode = DisplayMode.Unicode;
			HexViewContent.Text = MemoryString();
		}

		private void DisplayAddressRange()
		{
			ulong endAddr = _currentAddr + (ulong) _byteBufSize;
			HexViewHeaders.Text = Utils.RealAddressString(_currentAddr) + " - " + Utils.RealAddressString(endAddr);
		}

		private void DisplayBufferSize()
		{
			BufferSize.Text = _byteBufSize.ToString("##,###");
		}

		private void ChangeBufferSize_OnClick(object sender, RoutedEventArgs e)
		{
			var txt = BufferSize.Text.Trim();
			int newSize;
			if (Int32.TryParse(txt, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out newSize))
			{
				if (newSize < MinBufSize) newSize = MinBufSize;
				else if (newSize > MaxBufSize) newSize = MaxBufSize;
				if ((newSize%8) > 0)
				{
					newSize = (newSize/8)*8 + 8;
				}
				if (newSize > MaxBufSize) newSize = MaxBufSize;
				_byteBufSize = newSize;
				_bytes = new byte[_byteBufSize];
				string error;
				if (!ReadMemory(out error))
				{
					MessageBox.Show(error, "FAILED TO READ MEMORY", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
				DisplayBufferSize();
				DisplayAddressRange();
				HexViewContent.Text = MemoryString();
			}
		}

		private void ChangeAddress_OnClick(object sender, RoutedEventArgs e)
		{
			var txt = HexViewHeaders.Text.Trim();
			int endPos = 0;
			if (Utils.HasHexPrefix(txt))
				txt = txt.Substring(2);
			int txtCnt = txt.Length;
			for (; endPos < txtCnt; ++endPos)
			{
				if (!Utils.IsHexChar(txt[endPos]))
					break;
			}
			if (endPos < txtCnt && endPos > 0)
			{
				txt = txt.Substring(0, endPos);
			}

			ulong newAddr;
			ulong lastCurrentAddr = _currentAddr;
			if (ulong.TryParse(txt, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out newAddr))
			{
				if (newAddr > 0UL)
				{
					// round down to 8 boundary
					_currentAddr = (newAddr/8UL)*8UL;
				}
			}
			string error;
			if (!ReadMemory(out error))
			{
				_currentAddr = lastCurrentAddr;
				DisplayAddressRange();
				MessageBox.Show(error, "FAILED TO READ MEMORY", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			DisplayAddressRange();
			HexViewContent.Text = MemoryString();
		}

		private bool ReadMemory(out string error)
		{
			error = null;
			try
			{
				_heap.ReadMemory(_currentAddr, _bytes, 0, _byteBufSize);
				return true;
			}
			catch (Exception ex)
			{
				error = Utils.GetExceptionErrorString(ex);
				return false;
			}
		}
	}
}
