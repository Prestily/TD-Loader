﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TD_Loader.Classes;
using BTD_Backend;
using BTD_Backend.Web;
using BTD_Backend.NKHook5;
using BTD_Backend.Persistence;
using TD_Loader.UserControls;
using BTD_Backend.Game;
using System.Windows.Media;

namespace TD_Loader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public event ProcFinished_BoolEventHandler FinishedGameHandling;

        bool checkedNKH = false;
        bool finishedLoading = false;
        public static bool doingWork = false;
        public static bool exit = false;
        public static bool resetGameFiles = false;
        public static string workType = "";
        public static MainWindow instance;
        public static Mods_UserControl mods_User;
        public static Plugins_UserControl plugin_User;

        public MainWindow()
        {
            InitializeComponent();
            instance = this;
            Startup();
        }

        private void Log_MessageLogged(object sender, Log.LogEvents e)
        {
            if (e.UseMsgBox)
                MessageBox.Show(e.Message);
            else
            {
                OutputLog.Dispatcher.BeginInvoke((Action)(() =>
                {
                    OutputLog.AppendText(e.Message);
                    OutputLog.ScrollToEnd();
                }));
            }

            if (TempSettings.Instance.ConsoleFlash && OutputLog.Visibility == Visibility.Collapsed)
                blinkTimer.Start();
        }

        private void Startup()
        {
            Log.MessageLogged += Log_MessageLogged;
            JetReader.FinishedStagingMods += JetReader_FinishedStagingMods;
            FinishedGameHandling += MainWindow_FinishedGameHandling;
            GamesList.GameChanged += GamesList_GameChanged;

            Log.Output("Program initializing...");

            /*Settings.LoadSettings();
            if (Settings.game == null)
                Settings.SetGameFile(Settings.settings.GameName);*/

            if (TempSettings.Instance.LastGame == GameType.None)
            {
                Mods_Tab.Visibility = Visibility.Collapsed;
                return;
            }

            SessionData.CurrentGame = TempSettings.Instance.LastGame;
            SessionData.LoadedMods = TempSettings.Instance.LastUsedMods;
            var args = new GamesList.GameListEventArgs();
            GamesList.Instance.OnGameChanged(args);

            

            //removed for cleanup
            //
            //ResetGamePictures();
            //CreateModsTab();
            //ShowHidePlugins();

            //Enable later
            /*UpdateHandler update = new UpdateHandler()
            {
                GitApiReleasesURL = "https://api.github.com/repos/TDToolbox/TD-Loader/releases",
                ProjectExePath = Environment.CurrentDirectory + "\\TD Loader.exe",
                InstallDirectory = Environment.CurrentDirectory,
                ProjectName = "TD Loader",
                UpdatedZipName = "TD Loader.zip"
            };
            BgThread.AddToQueue(() => update.HandleUpdates(false));*/
        }

        private void GamesList_GameChanged(object sender, GamesList.GameListEventArgs e)
        {
            if (SessionData.CurrentGame != GameType.BTD5)
                Plugins_Tab.Visibility = Visibility.Collapsed;
            else
                Plugins_Tab.Visibility = Visibility.Visible;
        }

        private void FinishedLoading()
        {
            string tdloaderDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\TD Loader";
            UserData.MainProgramExePath = Environment.CurrentDirectory + "\\TD Loader.exe";
            UserData.MainProgramName = "TD Loader";
            UserData.MainSettingsDir = tdloaderDir;
            UserData.UserDataFilePath = tdloaderDir + "\\userdata.json";

            BgThread.AddToQueue(() =>
            {
                UserData.LoadUserData();
                UserData.SaveUserData();
            });



            /*if (!Guard.IsStringValid(Settings.game.GameDir) || !Guard.IsStringValid(Settings.game.GameName))
            {
                Settings.settings.GameName = "";
                Settings.SaveSettings();
                Settings.game = null;
                return;
            }

            GameHandling();*/
        }
        private void GameHandling()
        {
            if (Settings.game == null)
                return;

            Settings.SaveGameFile();
            doingWork = true;

            Log.Output("Game: " + Settings.game.GameName);
            workType = "Initializing mod loader for " + Settings.game.GameName;
            ShowHidePlugins();
            new Thread(() =>
            {
                Game.CloseGameIfOpen(Settings.game.GameName);
                Game.ValidateGameDir();
                Game.WasGameUpdated();

                if (!Guard.IsStringValid(Settings.game.ModsDir))
                    Game.SetModsDir(Settings.game.GameName);

                Game.ValidateBackup();

                NKHook5Manager.HandleUpdates();

                doingWork = false;
                workType = "";

                if (FinishedGameHandling != null)
                    FinishedGameHandling(this, new ProcFinished_BoolEventArgs(true));
                
            }).Start();
        }
        private void MainWindow_FinishedGameHandling(object source, ProcFinished_BoolEventArgs e)
        {
            /*Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                ShowHidePlugins();
                Mods_UserControl.instance.PopulateMods(Settings.game.GameName);
                Mods_UserControl.instance.Mods_TextBlock.Text = Settings.game.GameName + " Mods";

                
                CreatePluginsTab();
            }));
            */
        }

        //
        //Main events
        //
        public static async Task Wait(int time)
        {
            await Task.Delay(time);
        }
        private void Main_Activated(object sender, EventArgs e)
        {
            if (finishedLoading == false)
            {
                finishedLoading = true;
                FinishedLoading();
            }
        }
        private void Main_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.SaveSettings();
            Process.GetCurrentProcess().Kill();
        }

        private void JetReader_FinishedStagingMods(object sender, EventArgs e)
        {
            Game.LaunchGame();
        }



        //
        //UI events
        //
        private void CreateModsTab()
        {
            mods_User = new Mods_UserControl();
            var tab = new TabItem();
            tab.Header = "     Mods     ";
            tab.Padding = new Thickness(5);
            tab.FontSize = 25;
            tab.Content = mods_User;
            Main_TabController.Items[1] = tab;
        }
        private void CreatePluginsTab()
        {
            plugin_User = new Plugins_UserControl()
            {
                isPlugins = true
            };
            Plugins_Tab.Content = plugin_User;
        }
        private void ShowHidePlugins()
        {
            /*if (Settings.game.GameName == "BTD5" && Settings.game != null)
            {
                Plugins_Tab.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { Plugins_Tab.Visibility = Visibility.Visible; }));
                LaunchGrid.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { LaunchGrid.MinHeight = 195; }));
            }
            else
            {
                Plugins_Tab.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { Plugins_Tab.Visibility = Visibility.Collapsed; }));
                LaunchGrid.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { LaunchGrid.MinHeight = 165; }));
            }*/
        }
        private void ResetGameFiles_CB_Click(object sender, RoutedEventArgs e)
        {
            resetGameFiles = !resetGameFiles;
        }

        private void ConsoleColapsed(object sender, RoutedEventArgs e)
        {
            if (OutputLog.Visibility == Visibility.Collapsed)
            {
                OutputLog.Visibility = Visibility.Visible;
                CollapseConsole_Button.Content = "Hide Console";
            }
            else
            {
                OutputLog.Visibility = Visibility.Collapsed;
                CollapseConsole_Button.Content = "Show Console";
            }
        }

        private void CollapseGamesList_Button_Click(object sender, RoutedEventArgs e)
        {
           /* if (GameScrollViewer.Visibility == Visibility.Collapsed)
            {
                GameScrollViewer.Visibility = Visibility.Visible;
                CollapseGamesList_Button.Content = "Hide Games";
            }
            else
            {
                GameScrollViewer.Visibility = Visibility.Collapsed;
                CollapseGamesList_Button.Content = "Show Games";
            }*/
        }

        


        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            ToolBar toolBar = sender as ToolBar;
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
                overflowGrid.Visibility = Visibility.Collapsed;

            var mainPanelBorder = toolBar.Template.FindName("MainPanelBorder", toolBar) as FrameworkElement;
            if (mainPanelBorder != null)
                mainPanelBorder.Margin = new Thickness();
        }

        private void Launch_Button_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(TempSettings.Instance.GetModsDir(SessionData.CurrentGame)))
            {
                Log.Output("Error! You can't launch yet because you need to set a mods directory for your selected game");
                return; 
            }

            Launcher.Launch(SessionData.CurrentGame);
        }

        private void OpenSettingsDir_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(TempSettings.Instance.MainSettingsDir))
            {
                Directory.CreateDirectory(TempSettings.Instance.MainSettingsDir);
                TempSettings.SaveSettings();
                UserData.SaveUserData();
            }

            Process.Start(TempSettings.Instance.MainSettingsDir);
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            string settingsPath = TempSettings.Instance.MainSettingsDir + "\\" + TempSettings.Instance.settingsFileName;

            if (!File.Exists(settingsPath))
                TempSettings.SaveSettings();

            Process.Start(settingsPath);
        }


        // The timer's Tick event.
        private bool BlinkOn = false;
        private int blinkCount = 0;
        private void timer_Tick(object sender, EventArgs e)
        {
            var consoleButtonColor = new SolidColorBrush(Color.FromArgb(255, 221, 221, 221));
            var consoleDarkButtonColor = new SolidColorBrush(Color.FromArgb(255, 62, 62, 62));

            if (blinkCount >= 3)
            {
                BlinkOn = false;
                blinkCount = 0;
                CollapseConsole_Button.Background = consoleButtonColor;
                CollapseConsole_Button.Foreground = Brushes.Black;
                blinkTimer.Stop();
                return;
            }

            if (BlinkOn)
            {
                CollapseConsole_Button.Foreground = Brushes.Black;
                CollapseConsole_Button.Background = consoleButtonColor;
            }
            else
            {
                CollapseConsole_Button.Background = consoleDarkButtonColor;
                CollapseConsole_Button.Foreground = Brushes.White;
            }

            BlinkOn = !BlinkOn;
            blinkCount++;
        }

        DispatcherTimer blinkTimer = new DispatcherTimer();
        private void Main_Loaded(object sender, RoutedEventArgs e)
        {
            blinkTimer.Tick += timer_Tick;
            blinkTimer.Interval = new TimeSpan(0, 0, 0, 0, 350);
        }

        private void OpenModsDirHandling()
        {
            if (!Directory.Exists(TempSettings.Instance.MainSettingsDir))
            {
                Directory.CreateDirectory(TempSettings.Instance.MainSettingsDir);
                TempSettings.SaveSettings();
                UserData.SaveUserData();
            }
        }

        private void OpenBTD6_ModDir_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenModsDirHandling();
            TempSettings.LoadSettings();

            string dir = TempSettings.Instance.GetModsDir(GameType.BTD6);
            if (String.IsNullOrEmpty(dir))
                return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            Process.Start(dir);
        }

        private void OpenBTD5_ModDir_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenModsDirHandling();

            string dir = TempSettings.Instance.GetModsDir(GameType.BTD5);
            if (String.IsNullOrEmpty(dir))
                return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Process.Start(dir);
        }

        private void OpenBTDB_ModDir_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenModsDirHandling();

            string dir = TempSettings.Instance.GetModsDir(GameType.BTDB);
            if (String.IsNullOrEmpty(dir))
                return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Process.Start(dir);
        }

        private void OpenBMC_ModDir_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenModsDirHandling();

            string dir = TempSettings.Instance.GetModsDir(GameType.BMC);
            if (String.IsNullOrEmpty(dir))
                return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Process.Start(dir);
        }

        private void Discord_Button_Click(object sender, RoutedEventArgs e) => Process.Start("https://discord.gg/jj5Q7mA");
    }
}
