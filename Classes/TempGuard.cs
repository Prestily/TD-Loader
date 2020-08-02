﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTD_Backend;

namespace TD_Loader.Classes
{
    class TempGuard
    {
        public static bool IsDoingWork(string errorMessage)
        {
            if (MainWindow.doingWork)
            {
                Log.Output("Cant do that! Doing something else.\nCurrent Process: " + errorMessage);
                Log.Output("Cant do that! Doing something else.\nCurrent Process: " + errorMessage, true);
                return true;
            }
            else
                return false;
        }
    }
}
