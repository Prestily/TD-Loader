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
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TD_Loader.Classes;

namespace TD_Loader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool checkedNKH = false;
        bool finishedLoading = false;
        public static bool doingWork = false;
        public static bool exit = false;
        public static string workType = "";
        public static MainWindow instance;
        public static Mods_UserControl mods_User;
        public static Plugins_UserControl plugin_User;
        public MainWindow()
        {
            InitializeComponent();
            instance = this;

            Startup();
            BTD5_Image.IsMouseDirectlyOverChanged += BTD5_Image_IsMouseDirectlyOverChanged;
            BTDB_Image.IsMouseDirectlyOverChanged += BTDB_Image_IsMouseDirectlyOverChanged;
            BMC_Image.IsMouseDirectlyOverChanged += BMC_Image_IsMouseDirectlyOverChanged;

            Main.Closing += Main_Closing;
            JetReader.FinishedStagingMods += JetReader_FinishedStagingMods;
        }

        private void Startup()
        {
            Settings.LoadSettings();

            mods_User = new Mods_UserControl();
            var tab = new TabItem();
            tab.Header = "     Mods     ";
            tab.Padding = new Thickness(5);
            tab.FontSize = 25;
            tab.Content = mods_User;//new Mods_UserControl();
            Main_TabController.Items[1] = tab;
            
            if(NKHook.CanUseNKH())
                CreatePluginsTab();



            new Thread(() => {
                UpdateHandler update = new UpdateHandler();
                update.HandleUpdates();
            }).Start();
            
        }
        private void CreatePluginsTab()
        {
            plugin_User = new Plugins_UserControl()
            {
                isPlugins = true
            };

            var tab2 = new TabItem();
            tab2.Header = "   Plugins   ";
            tab2.Padding = new Thickness(5);
            tab2.FontSize = 25;
            tab2.Content = plugin_User;
            Main_TabController.Items[2] = tab2;
        }

        private void FinishedLoading()
        {
            bool dirNotFound = false;
            if (!Guard.IsStringValid(Settings.game.GameDir))
            {
                dirNotFound = true;
            }

            switch (Settings.game.GameName)
            {
                case "BTD5":
                        BTD5_Image.Source = new BitmapImage(new Uri("Resources/btd5.png", UriKind.Relative));
                    break;
                case "BTDB":
                        BTDB_Image.Source = new BitmapImage(new Uri("Resources/btdb 2.png", UriKind.Relative));
                    break;
                case "BMC":
                        BMC_Image.Source = new BitmapImage(new Uri("Resources/bmc.png", UriKind.Relative));
                    break;
                default:
                    dirNotFound = true;
                    break;
            }

            //if (Settings.settings.GameName != null && Settings.settings.GameName != "")
            if(!dirNotFound)
            {
                GameHandling();
            }
            else
            {
                Settings.settings.GameName = "";
                Settings.game = null;
                Settings.SaveSettings();
            }
        }
        private async void GameHandling()
        {
            if(Settings.game == null)
                return;

            if(Settings.settings.GameName == "BTD5")
            {
                Plugins_Tab.Visibility = Visibility.Visible;
                LaunchGrid.MinHeight = 155;
            }
            else
            {
                Plugins_Tab.Visibility = Visibility.Collapsed;
                LaunchGrid.MinHeight = 120;
            }

            Log.Output("Game: " + Settings.settings.GameName);
            doingWork = true;
            workType = "Initializing mod loader for game";
            Settings.SetGameFile();
            Settings.SaveSettings();         


            //
            //Check game dir
            bool error = false;
            string gameD = Settings.game.GameDir;
            if (Guard.IsStringValid(gameD))
            {
                if (!Directory.Exists(gameD))
                {
                    if(Directory.Exists(Settings.game.GameBackupDir))
                    {
                        Log.Output("The saved game directory couldnt be found! However, there was a backup of the" +
                            " game files. Copying game files to saved game directory");
                        FileIO.CopyDirsAndContents(Settings.game.GameBackupDir, Settings.game.GameDir);
                        return;
                    }

                    error = true;
                    Log.Output("The saved game directory couldnt be found!");
                    MessageBox.Show("Game Directory Found!");
                    Log.Output("Game Directory Found!");
                }
            }
            else
                error = true;

            if (error)
            {
                MessageBox.Show("Some setup is required before you can use mods with this game. Please be patient and read the following messages to " +
                    "make sure it sets up properly. This will take up to 2 minutes");
                string dir = Game.SetGameDir(Settings.settings.GameName);
                if (!Guard.IsStringValid(dir))
                {
                    Log.Output("Something went wrong... Failed to aquire game directory...");
                    ResetGamePictures();
                    return;
                }
                Settings.game.GameDir = dir;
                Settings.SaveGameFile();
            }

            if (!Guard.IsStringValid(Settings.game.GameDir))
            {
                Log.Output("Something went wrong... Failed to aquire game directory...");
                ResetGamePictures();
                return;
            }


            //
            //Check for Game Updated
            //Get Game Version if it wasnt 
            if (!Guard.IsStringValid(Settings.game.GameVersion))
            {
                Settings.game.GameVersion = Game.GetVersion(Settings.settings.GameName);
                Settings.SaveGameFile();
                Settings.SaveSettings();
            }
            else
            {
                string version = Game.GetVersion(Settings.settings.GameName);
                if (version != Settings.game.GameVersion)
                {
                    workType = "Aquiring backup files";
                    MessageBox.Show("Game has been updated... Reaquiring files...");
                    Log.Output("Game has been updated... Reaquiring files...");
                    string backupdir = Settings.game.GameBackupDir;
                    if (backupdir == "" || backupdir == null)
                        Game.CreateBackupDir(Settings.settings.GameName);

                    await Game.CreateBackupAsync(Settings.settings.GameName);
                    Log.Output("Done making backup");

                    Settings.game.GameVersion = version;
                    Settings.SaveGameFile();

                    if (Settings.settings.GameName == "BTDB")
                    {
                        Settings.settings.DidBtdbUpdate = true;
                        Settings.SaveSettings();

                        Zip original = new Zip(Settings.settings.BTDBBackupDir + "\\Assets\\data.jet");
                        Thread thread = new Thread(delegate () { original.GetPassword(); });
                        thread.Start();
                    }
                }
            }



            //
            //Check Mods Dir
            string modsDir = Settings.game.ModsDir;
            if((Settings.settings.GameName != "" && Settings.settings.GameName != null) && (modsDir == "" || modsDir == null))
                Game.SetModsDir(Settings.settings.GameName);


            //
            //Check Backup
            bool valid = Game.VerifyBackup(Settings.settings.GameName);
            if(!valid)
            {
                workType = "Creating backup";
                string backupdir = Settings.game.GameBackupDir;
                if (backupdir == "" || backupdir == null)
                    Game.CreateBackupDir(Settings.settings.GameName);

                await Game.CreateBackupAsync(Settings.settings.GameName);
                Log.Output("Done making backup");
            }

            //
            //Clear mods list
            Mods_UserControl.instance.PopulateMods(Settings.settings.GameName);
            Mods_UserControl.instance.Mods_TextBlock.Text = Settings.settings.GameName + " Mods";
            //
            //Done
            doingWork = false;
            workType = "";
        }
        public static async Task Wait(int time)
        {
            await Task.Delay(time);
        }

        //
        //Main events
        //
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
            Environment.Exit(1);
        }


        //
        //UI events
        //
        private void ResetGamePictures()
        {
            workType = "";  //We're doing this to clear the last opperation, if it wasnt cleared
            BTD5_Image.Source = new BitmapImage(new Uri("Resources/btd5_not loaded.png", UriKind.Relative));
            BTDB_Image.Source = new BitmapImage(new Uri("Resources/btdb 2_not loaded.png", UriKind.Relative));
            BMC_Image.Source = new BitmapImage(new Uri("Resources/bmc_not loaded.png", UriKind.Relative));
        }
        private void BMC_Image_IsMouseDirectlyOverChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Settings.game == null)
                Settings.SetGameFile();

            if (Settings.game.GameName != "BMC")
            {
                if (!BMC_Image.IsMouseOver)
                    BMC_Image.Source = new BitmapImage(new Uri("Resources/bmc_not loaded.png", UriKind.Relative));
                else
                    BMC_Image.Source = new BitmapImage(new Uri("Resources/bmc.png", UriKind.Relative));
            }
        }
        private void BTDB_Image_IsMouseDirectlyOverChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Settings.game == null)
                Settings.SetGameFile();

            if (Settings.game.GameName != "BTDB")
            {
                if (!BTDB_Image.IsMouseOver)
                    BTDB_Image.Source = new BitmapImage(new Uri("Resources/btdb 2_not loaded.png", UriKind.Relative));
                else
                    BTDB_Image.Source = new BitmapImage(new Uri("Resources/btdb 2.png", UriKind.Relative));
            }                
        }
        private void BTD5_Image_IsMouseDirectlyOverChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Settings.game == null)
                Settings.SetGameFile();

            if (Settings.game.GameName != "BTD5")
            {
                if (!BTD5_Image.IsMouseOver)
                    BTD5_Image.Source = new BitmapImage(new Uri("Resources/btd5_not loaded.png", UriKind.Relative));
                else
                    BTD5_Image.Source = new BitmapImage(new Uri("Resources/btd5.png", UriKind.Relative));
            }
        }
        private void BTD5_Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Guard.IsDoingWork(workType))
                return;

            if (Settings.game.GameName != "BTD5")
            {
                ResetGamePictures();
                Settings.settings.GameName = "BTD5";
                BTD5_Image.Source = new BitmapImage(new Uri("Resources/btd5.png", UriKind.Relative));

                GameHandling();

                if(!checkedNKH)
                {
                    if(!File.Exists(Environment.CurrentDirectory + "\\NKHook5-Injector.exe"))
                    {
                        if(File.Exists(NKHook.nkhEXE))
                            File.Copy(NKHook.nkhEXE, Environment.CurrentDirectory + "\\NKHook5-Injector.exe");
                    }

                    NKHook nkh = new NKHook();
                    nkh.DoUpdateNKH();
                    nkh.DoUpdateTowerPlugin();
                }
            }
        }
        private void BTDB_Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Guard.IsDoingWork(workType))
                return;

            if (Settings.settings.GameName != "BTDB")
            {
                ResetGamePictures();
                Settings.settings.GameName = "BTDB";
                BTDB_Image.Source = new BitmapImage(new Uri("Resources/btdb 2.png", UriKind.Relative));

                GameHandling();
            }

        }
        private void BMC_Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Guard.IsDoingWork(workType))
                return;

            if (Settings.settings.GameName != "BMC")
            {
                ResetGamePictures();
                Settings.settings.GameName = "BMC";
                BMC_Image.Source = new BitmapImage(new Uri("Resources/bmc.png", UriKind.Relative));

                GameHandling();
            }
        }
        private void Mods_Tab_MouseDown(object sender, MouseButtonEventArgs e)
        {
            
            //Main_TabController.Items.Add()
        }


        private void Launch_Button_Clicked(object sender, RoutedEventArgs e)
        {
            if(Guard.IsDoingWork(workType))
            {
                return;
            }

            if (mods_User.SelectedMods_ListBox.Items.Count > 0)
            {
                MessageBox.Show("Beginning to merge mods. Please wait, this will take up to 5 seconds per mod. Bigger mods could take up to a minute or 2. The program is not frozen...");
                MessageBox.Show("If you have issues with your mods, please try chaning the load order");

                doingWork = true;
                workType = "Beginning to merge mods";

                Settings.game.LoadedMods = mods_User.modPaths;
                Settings.SaveGameFile();
                Settings.SaveSettings();

                JetReader jet = new JetReader();
                jet.DoWork();
            }
            else
            {
                Log.Output("You chose to play with no mods... Launching game");

                new Thread(() => 
                {
                    Thread t = new Thread(Game.ResetGameFiles);
                    t.Start();
                    t.Join();
                    LaunchGame();
                }).Start();
            }
        }
        private void JetReader_FinishedStagingMods(object sender, EventArgs e)
        {
            LaunchGame();
        }
        private void LaunchGame()
        {
            if (!Guard.IsStringValid(Settings.game.GameName))
            {
                MessageBox.Show("Failed to get game name for game. Unable to launch");
                return;
            }

            if (NKHook.CanUseNKH())
            {
                if(plugin_User.SelectedPlugins_ListBox.Items.Count > 0)
                {
                    Log.Output("Plugins are enabled. Launching NKHook");
                    Process.Start(NKHook.nkhEXE);
                    return;
                }
            }
            Process.Start(Settings.game.GameDir + "\\" + Settings.game.ExeName);
        }
    }
}
