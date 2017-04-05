using System.Windows;
using ClrMDRIndex;
using System.Diagnostics;
using System.Windows.Documents;
using Brushes = System.Windows.Media.Brushes;

namespace MDRDesk
{
	/// <summary>
	/// Interaction logic for TypeValueFilterDlg.xaml
	/// </summary>
	public partial class TypeValueFilterDlg
	{
		// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
		private readonly ClrtDisplayableType _dispType;
		// ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
		private string _value;
		public string Value => _value;
        bool _caseSensitive = false;

		public TypeValueFilterDlg(ClrtDisplayableType dispType)
		{
			InitializeComponent();
			LbTypeName.Content = dispType.TypeName;
			_dispType = dispType;
			if (_dispType.HasFilter())
			{
				TbTypeValue.Text = _dispType.Filter.FilterString;
			}
            InitFilterDescription();
        }

        private void InitFilterDescription()
        {
            var kind = _dispType.Kind;
            Debug.Assert(kind != ClrElementKind.Unknown);
            var specKind = TypeExtractor.GetSpecialKind(kind);

            if (TypeExtractor.IsString(kind))
            {
                TypeValueDescr.Visibility = Visibility.Hidden;
                TypeValeCase.IsChecked = _dispType.HasFilter() && _dispType.Filter.IsIgnoreCase();
                TypeValeRegex.IsChecked = _dispType.HasFilter() && _dispType.Filter.IsRegex();
                return;
            }

            TypeValeCase.Visibility = Visibility.Hidden;
            TypeValeRegex.Visibility = Visibility.Hidden;

            if (specKind != ClrElementKind.Unknown)
            {
                switch (specKind)
                {
                    case ClrElementKind.Exception:
                        TypeValueDescr.Inlines.Add(new Run("Enter address in hex format: "));
                        TypeValueDescr.Inlines.Add(new Run("0x000083599c5498") { FontWeight = FontWeights.Bold });
                        TypeValueDescr.Inlines.Add(new Run(", leading zeros are not necessary."));
                        break;
                    case ClrElementKind.Enum:
                        TypeValueDescr.Inlines.Add(new Run("Enum format is integral value of enumeration."));
                        break;
                    case ClrElementKind.Free:
                        TypeValueDescr.Inlines.Add(new Run("Free type should not be filtered!") { FontWeight = FontWeights.Bold, Foreground = Brushes.Red });
                        break;
                    case ClrElementKind.Guid:
                        TypeValueDescr.Inlines.Add(new Run("Guid format ex.: "));
                        TypeValueDescr.Inlines.Add(new Run("00000000-0000-0000-0000-000000000000") { FontWeight = FontWeights.Bold });
                        break;
                    case ClrElementKind.DateTime:
                        TypeValueDescr.Inlines.Add(new Run("DateTime format ex.: "));
                        TypeValueDescr.Inlines.Add(new Run("2009-06-15T13:45:30") { FontWeight = FontWeights.Bold });
                        TypeValueDescr.Inlines.Add(new Run(" -> 6/15/2009 1:45 PM"));
                        break;
                    case ClrElementKind.TimeSpan:
                        TypeValueDescr.Inlines.Add(new Run("TimeSpan format ex.: "));
                        TypeValueDescr.Inlines.Add(new Run("1:3:16:50.5 ") { FontWeight = FontWeights.Bold });
                        break;
                    case ClrElementKind.Decimal:
                        break;
                    case ClrElementKind.SystemVoid:
                        TypeValueDescr.Inlines.Add(new Run("System.Void -- unexpected type!") { FontWeight = FontWeights.Bold, Foreground = Brushes.Red });
                        break;
                    case ClrElementKind.SystemObject:
                    case ClrElementKind.Interface:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.System__Canon:
                        TypeValueDescr.Inlines.Add(new Run("Enter address in hex format: "));
                        TypeValueDescr.Inlines.Add(new Run("0x000083599c5498") { FontWeight = FontWeights.Bold });
                        TypeValueDescr.Inlines.Add(new Run(", leading zeros are not necessary."));
                        break;
                    default:
                        TypeValueDescr.Inlines.Add(new Run("Unexpected type!") { FontWeight = FontWeights.Bold, Foreground = Brushes.Red });
                        break;
                }
            }
            else
            {
                var stdKind = TypeExtractor.GetStandardKind(kind);
                switch (stdKind)
                {
                    case ClrElementKind.Boolean:
                        TypeValueDescr.Inlines.Add(new Run("Valid values for this type, are: "));
                        TypeValueDescr.Inlines.Add(new Run("true") { FontWeight = FontWeights.Bold });
                        TypeValueDescr.Inlines.Add(new Run(" or "));
                        TypeValueDescr.Inlines.Add(new Run("false") { FontWeight = FontWeights.Bold });
                        break;
                    case ClrElementKind.Class:
                    case ClrElementKind.Struct:
                    case ClrElementKind.SZArray:
                    case ClrElementKind.Array:
                        break;
                    case ClrElementKind.Object:
                        TypeValueDescr.Inlines.Add(new Run("Enter address in hex format: "));
                        TypeValueDescr.Inlines.Add(new Run("0x000083599c5498") { FontWeight = FontWeights.Bold });
                        TypeValueDescr.Inlines.Add(new Run(", leading zeros are not necessary."));
                        break;
                    case ClrElementKind.Char:
                    case ClrElementKind.Int8:
                    case ClrElementKind.UInt8:
                    case ClrElementKind.Int16:
                    case ClrElementKind.UInt16:
                    case ClrElementKind.Int32:
                    case ClrElementKind.UInt32:
                    case ClrElementKind.Int64:
                    case ClrElementKind.UInt64:
                    case ClrElementKind.Float:
                    case ClrElementKind.Double:
                    case ClrElementKind.String:
                    case ClrElementKind.Pointer:
                    case ClrElementKind.NativeInt:
                    case ClrElementKind.NativeUInt:
                    case ClrElementKind.FunctionPointer:
                        TypeValueDescr.Inlines.Add(new Run("Primitive type: " + stdKind.ToString() + ", enter in standard format."));
                        break;
                    default:
                        TypeValueDescr.Inlines.Add(new Run("Unexpected type!") {FontWeight = FontWeights.Bold, Foreground = Brushes.Red });
                        break;
                }
            }
        }

        private void DialogOkClicked(object sender, RoutedEventArgs e)
		{
			_value = TbTypeValue.Text.Trim();
            _caseSensitive = (bool)TypeValeCase.IsChecked;
            DialogResult = true;
		}
	}
}
