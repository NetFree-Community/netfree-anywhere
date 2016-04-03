using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NfaSetup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        enum page
        {
            reqErrors = 0,
            wellcom = 1,
            guarantyLess = 2,
            license = 3,
            install = 4,
            status = 5
        }

        public MainWindow()
        {
            InitializeComponent();


            TabControlWiz.Tag = Visibility.Hidden;
            TabControlWiz.SelectedIndex = (int)page.wellcom;

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "t")
            {
                Navigate(page.install);
                new Setup(StatusChange, Finish);
            }


        }

        void Navigate(page page)
        {
            TabControlWiz.SelectedIndex = (int)page;
        }

        void NavigateNext()
        {
            TabControlWiz.SelectedIndex += 1;
        }

        private void disInvoke(Action action)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, action);
        }

        private void Finish(bool isSuccc, string error)
        {
            disInvoke(() =>
            {
                prgStatus.Visibility = Visibility.Hidden;
                brnClose.Visibility = Visibility.Visible;

                tbStatus.IsEnabled = false;

                if (isSuccc)
                    succsMsgTb.Visibility = Visibility.Visible;
                else
                {
                    errorMsgTb.Visibility = Visibility.Visible;
                    tbStatus.Text += error;
                }
            });
        }

        private void StatusChange(string status)
        {
            disInvoke(() =>
            {
                tbStatus.Text += status + "\n";
            });
        }



        private void Install_OnClick(object sender, RoutedEventArgs e)
        {
            Navigate(page.install);
            new Setup(StatusChange, Finish);
        }

        private void Next_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateNext();
        }

        private void Next_OnClick1(object sender, RoutedEventArgs e)
        {
            NavigateNext();
            UpdateLayout();
            txtAgree.Focus();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }



        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtAgree.Text.Contains("אני מסכים") || txtAgree.Text.ToLower().Contains("i agree"))
            {
                agreeSection.Visibility = Visibility.Hidden;
                ContniueToLicensBtn.Visibility = Visibility.Visible;
            }
        }

    }
}
