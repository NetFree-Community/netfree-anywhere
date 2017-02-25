using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using Ionic.Zip;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.ExceptionServices;

namespace NfaSetup
{
    class Setup
    {


        static string ServiceName = "NetFreeAnywhere";
        static string ServiceDisplayName = "NetFreeAnywhere";
        static string PathDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NetFree\NetFreeAnywhere");
        static string PathOpenVpn = Path.Combine(PathDir, @"openvpn\openvpn.exe");
        static string PathTapInstaller32 = Path.Combine(PathDir, @"openvpn\driver\tapinstall.exe");
        static string PathTapInstaller64 = Path.Combine(PathDir, @"openvpn\driver\tapinstall-64.exe");
        static string PathTapInstaller = Path.Combine(PathDir, @"openvpn\driver\tap-windows-9.21.2.exe");       
        static string PathTray = Path.Combine(PathDir, "nfaTray.exe");

        Action<string> status;
        Action<bool, string> finish;

        public Setup(Action<string> _status, Action<bool, string> _finish)
        {
            status = _status;
            finish = _finish;

            new Thread(() =>
           {
               try
               {
                   PrepedInstall();
                   SetupFiles();
                   Install();
                   Start();
                   finish(true, null);
               }
               catch (Exception ex)
               {
                   ErrorLog(ex);
                   finish(false, ex.Message);
               }
           }).Start();
        }



        private ServiceState CheckStatus()
        {
            status("בודק אם השירות קיים...");
            return ServiceInstaller.GetServiceStatus(ServiceName);
        }

        private void StopOld()
        {
            status("עוצר שירות קיים...");
            ServiceInstaller.Uninstall(ServiceName);
        }


        private void PrepedInstall()
        {
            ServiceState curr_status = CheckStatus();

            if (curr_status != ServiceState.NotFound)
                if (curr_status == ServiceState.Running)
                    StopOld();


            proccesClose("nfaTray");
        }

        private void SetupFiles()
        {
            status("יוצר ספריה...");
            Directory.CreateDirectory(PathDir);


            ExtractFile();
        }

        void proccesClose(string name)
        {
            foreach (var item in Process.GetProcessesByName(name))
            {
                try
                {
                    item.Kill();
                    item.WaitForExit(100);
                }
                catch
                {
                }
                finally
                {
                    if (item != null) item.Close();
                }
            }
        }





        private void WriteSetupSelfFile()
        {
            var FullPath = Path.Combine(PathDir, "NetFreeAnywhereSetup.exe");
            var CurrFile = Process.GetCurrentProcess().MainModule.FileName;
            if (CurrFile == FullPath)
                return;
            else
            {
                try
                {
                    status("מעתיק קובץ התקנה...");
                    File.WriteAllBytes(FullPath, File.ReadAllBytes(CurrFile));
                }
                catch (Exception ex)
                {
                    status("קובץ ההתקנה לא הועתק.");
                    ErrorLog(ex);
                }
            }
        }

        private void ExtractFile()
        {
            status("מחלץ קבצים...");

            using (ZipFile zip = ZipFile.Read(new MemoryStream(Properties.Resources.nfa)))
            {
                zip.ExtractAll(PathDir, ExtractExistingFileAction.OverwriteSilently);
            }
        }

        public static bool CheckVpnAdapter()
        {
            int i = 0;
            var StartInfo = new ProcessStartInfo();
            StartInfo.FileName = PathOpenVpn;
            StartInfo.Arguments = " --show-adapters";
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.UseShellExecute = false;
            StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            StartInfo.CreateNoWindow = true;
            Process p = Process.Start(StartInfo);
            p.BeginOutputReadLine();
            string sOut = "";
            p.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                Console.WriteLine(e.Data);
                if (e.Data != null){
                    i = i + 1;
                    sOut += e.Data;
                }
            };
            p.WaitForExit();

            return (i > 1);
        }

 
        [DllImport("shell32.dll")]
        private static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner, [Out]StringBuilder lpszPath, int nFolder, bool fCreate);

        static string GetSystemDirectory()
        {
            StringBuilder path = new StringBuilder(260);
            SHGetSpecialFolderPath(IntPtr.Zero, path, 0x0029, false);
            return path.ToString();
        }

        private void InstallTap()
        {

            if (CheckVpnAdapter()) return;

            Process p = new Process();
            p.StartInfo.FileName = PathTapInstaller;
            p.StartInfo.Arguments = " /S "; // " install \"" + pathTap + "\" tap0901 ";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.BeginOutputReadLine();
            string sOut = "";
            p.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                Console.WriteLine("TapInstaller:" + e.Data);
                sOut = sOut + e.Data + Environment.NewLine;
            };
            p.WaitForExit();
            if (sOut.Contains("failed"))
            {
                throw new Exception("failed InstallTap");
            }
        }

        private void InstallTrayFile()
        {
            var path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            RegistryKey key = Registry.LocalMachine.OpenSubKey(path, true);
            key.SetValue("NetFree Anywhere Tray", PathTray);
            key.Close();
        }

        private void Install()
        {
            InstallTrayFile();
            InstallTap();
            status("מתקין שירות...");
            ServiceInstaller.InstallService(ServiceName, ServiceDisplayName, Path.Combine(PathDir, "nfaService.exe"), ServiceBootFlag.AutoStart, ServiceType.Win32OwnProcess);
        }

        private void Start()
        {
            status("מפעיל שירות...");
            ServiceInstaller.StartService(ServiceName);
            Thread.Sleep(1000);
            Process.Start(PathTray).Dispose();
        }


        private void ErrorLog(Exception ex)
        {
            var message = ex.ToString();
            try
            {
                File.AppendAllText("error.txt", message + "\n\n");
            }
            finally
            {

            }
        }

    }

}
