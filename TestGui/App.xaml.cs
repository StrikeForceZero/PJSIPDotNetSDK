using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace TestGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

#if RELEASE
            Current.DispatcherUnhandledException += new System.Windows.Threading.DispatcherUnhandledExceptionEventHandler(Current_DispatcherUnhandledException);
#endif
        }

        private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Prevent default unhandled exception processing
            /*e.Handled = true;
            MessageBox.Show(e.Exception.Message);
            MessageBox.Show(e.Exception.StackTrace);*/
        }

        public App()
        {
            this.MainWindow = new MainWindow();
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            this.MainWindow.ShowDialog();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            RunSingleInstance();
        }

        static void RunSingleInstance()
        {
            bool createdNew = true;
            using (Mutex mutex = new Mutex(true, Assembly.GetExecutingAssembly().GetType().GUID.ToString(), out createdNew))
            {
                if (createdNew)
                {
                    var application = new App();
                    application.InitializeComponent();
                    application.Run();
                }
                else
                {
                    Process current = Process.GetCurrentProcess();
                    foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (process.Id != current.Id)
                        {
                            SetForegroundWindow(process.MainWindowHandle);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets foreground window
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
