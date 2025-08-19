using System;
using System.Windows;

namespace WinControlBot
{
    /// <summary>
    /// Entry point for the WPF application
    /// </summary>
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                app.Dispatcher.UnhandledException += (s, e) => 
                {
                    System.Windows.Forms.MessageBox.Show($"Critical error: {e.Exception.Message}\n\nDetails: {e.Exception.StackTrace}", 
                                "Error", 
                                System.Windows.Forms.MessageBoxButtons.OK, 
                                System.Windows.Forms.MessageBoxIcon.Error);
                    e.Handled = true;
                };
                app.Run();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Critical error starting application: {ex.Message}\n\nDetails: {ex.StackTrace}", 
                            "Startup Error", 
                            System.Windows.Forms.MessageBoxButtons.OK, 
                            System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}