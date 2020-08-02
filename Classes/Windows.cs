﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTD_Backend;

namespace TD_Loader.Classes
{
    class Windows
    {
        public static void CloseWindow(string windowMainTitle)
        {
            var openWindowProcesses = System.Diagnostics.Process.GetProcesses()
        .Where(p => p.MainWindowHandle != IntPtr.Zero && p.ProcessName != "explorer");

            foreach (var a in openWindowProcesses)
            {
                //if (a.MainWindowTitle == windowMainTitle)
                if (a.MainWindowTitle.Contains(windowMainTitle))
                {
                    Log.Output("Closing validator window");
                    a.CloseMainWindow();
                }
            }
        }
    }
}
