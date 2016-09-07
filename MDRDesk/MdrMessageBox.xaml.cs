using System;
using System.Collections.Generic;
using System.Linq;
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

public enum EnumPredefinedButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
}

public enum EnumDialogResults
{
    None = 0,
    Ok = 1,
    Cancel = 2,
    Yes = 3,
    No = 4,
    Button1 = 1,
    Button2 = 2,
    Button3 = 3,
}

namespace MDRDesk
{
    /// <summary>
    /// Interaction logic for MdrMessageBox.xaml
    /// </summary>
    public partial class MdrMessageBox : Window
    {
        public MdrMessageBox()
        {
            InitializeComponent();
        }

        private void CustomMessagBox_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Property offering the caption of the Window (the Title)
        /// </summary>
        public string Caption
        {
            get { return TbCaption.Text; }
            set { TbCaption.Text = value; }
        }

        /// <summary>
        /// Property showing a header in the instructions section
        /// </summary>
        public string InstructionHeading
        {
            get { return TbInstructionHeading.Text; }
            set { TbInstructionHeading.Text = value; }
        }

        /// <summary>
        /// Property showing the instructions
        /// </summary>
        public string InstructionText
        {
            get { return TbInstructionText.Text; }
            set { TbInstructionText.Text = value; }
        }

        /// <summary>
        /// Property showing the details
        /// </summary>
        public string DeatilsText
        {
            get { return DetailsText.Text; }
            set { DetailsText.Text = value; }
        }

        public void SetButtonsPredefined(EnumPredefinedButtons buttons)
        {
            Button1.Visibility = Visibility.Collapsed;
            Button1.Tag = EnumDialogResults.None;
            Button2.Visibility = Visibility.Collapsed;
            Button2.Tag = EnumDialogResults.None;
            Button3.Visibility = Visibility.Collapsed;
            Button3.Tag = EnumDialogResults.None;

            switch (buttons)
            {
                case EnumPredefinedButtons.Ok:
                    Button1.Visibility = Visibility.Visible;
                    Button1.Content = "Ok";
                    Button1.Tag = EnumDialogResults.Ok;
                    break;
                case EnumPredefinedButtons.OkCancel:
                    Button1.Visibility = Visibility.Visible;
                    Button1.Content = "Cancel";
                    Button1.Tag = EnumDialogResults.Cancel;

                    Button2.Visibility = Visibility.Visible;
                    Button2.Content = "Ok";
                    Button2.Tag = EnumDialogResults.Ok;
                    break;
                case EnumPredefinedButtons.YesNo:
                    Button1.Visibility = Visibility.Visible;
                    Button1.Content = "No";
                    Button1.Tag = EnumDialogResults.No;

                    Button2.Visibility = Visibility.Visible;
                    Button2.Content = "Yes";
                    Button2.Tag = EnumDialogResults.Yes;
                    break;
                case EnumPredefinedButtons.YesNoCancel:
                    Button1.Visibility = Visibility.Visible;
                    Button1.Content = "Cancel";
                    Button1.Tag = EnumDialogResults.Cancel;

                    Button2.Visibility = Visibility.Visible;
                    Button2.Content = "No";
                    Button2.Tag = EnumDialogResults.No;

                    Button3.Visibility = Visibility.Visible;
                    Button3.Content = "Yes";
                    Button3.Tag = EnumDialogResults.Yes;
                    break;
            }
        }
    }
}
