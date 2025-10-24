using System;
using System.Windows.Forms;

namespace GeneralaGame
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // WinForms bootstrap
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
