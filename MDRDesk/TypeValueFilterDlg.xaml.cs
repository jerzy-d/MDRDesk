using System.Windows;
using ClrMDRIndex;
using System.Diagnostics;
using System.Windows.Documents;

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
                    case ClrElementKind.Enum:
                        TypeValueDescr.Inlines.Add(new Run("Enum format is integral value of enumeration."));
                        break;
                    case ClrElementKind.Free:
                    case ClrElementKind.Guid:
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
                    case ClrElementKind.SystemObject:
                    case ClrElementKind.Interface:
                    case ClrElementKind.Abstract:
                    case ClrElementKind.System__Canon:
                         break;
                }
            }
            else
            {
                switch (TypeExtractor.GetStandardKind(kind))
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
                    case ClrElementKind.String:
                        break;
                    case ClrElementKind.Object:
                        TypeValueDescr.Inlines.Add(new Run("Enter address in hex format: "));
                        TypeValueDescr.Inlines.Add(new Run("0x000083599c5498") { FontWeight = FontWeights.Bold });
                        TypeValueDescr.Inlines.Add(new Run(", leading zeros are not necessary."));
                        break;
                    default:
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
