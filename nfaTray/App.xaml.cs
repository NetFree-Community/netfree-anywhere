using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Security.Permissions;
using System.ServiceModel;
using System.Windows.Forms;

namespace nfaTray
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {

        private void Application_Startup(object sender, StartupEventArgs e1)
        {

            new MainWindow();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {

        }

    }
}
