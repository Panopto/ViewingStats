using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Timers;
using System.Windows;

namespace PanoptoCollectAllStats
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static bool hasBeenInitialized = false;
        private static System.Timers.Timer timer;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Creates a timer that calls method to get stats after short interval to
        /// allow for UI changes
        /// </summary>
        /// <param name="sender">object that calls this method</param>
        /// <param name="e">arguments</param>
        private void Create_Click(object sender, RoutedEventArgs e)
        {
            LockAllFields();
            Status.Content = "Quering for stats...";
            timer = new System.Timers.Timer(100);
            timer.Elapsed += DispatchUpload;

            timer.Start();
        }

        /// <summary>
        /// Disabling all fields
        /// </summary>
        private void LockAllFields()
        {
            UserID.IsEnabled = false;
            UserPassword.IsEnabled = false;
            Query.IsEnabled = false;
        }

        /// <summary>
        /// Enabling all fields that are originally enabled
        /// </summary>
        private void FreeAllFields()
        {
            UserID.IsEnabled = true;
            UserPassword.IsEnabled = true;
            Query.IsEnabled = true;
        }

        /// <summary>
        /// Calls the process stats method
        /// </summary>
        private void DispatchUpload(Object source, ElapsedEventArgs e)
        {
            timer.Stop();

            Action uploadMethod = ProcessBackground;
            Dispatcher.BeginInvoke(uploadMethod);
        }

        /// <summary>
        /// Sets up BackgroundWorker for stats collection in background and starts BackgroundWorker
        /// </summary>
        private void ProcessBackground()
        {
            BackgroundWorker bgw = new BackgroundWorker();
            bgw.DoWork += new DoWorkEventHandler(ProcessStatsRequest);
            bgw.ProgressChanged += new ProgressChangedEventHandler(CreationStatus);
            bgw.WorkerReportsProgress = true;
            bgw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CreationComplete);

            object[] args = new object[] { ServerName.Text, UserID.Text, UserPassword.Password };

            bgw.RunWorkerAsync(args);
        }

        /// <summary>
        /// Make the request for stats
        /// </summary>
        /// <param name="sender">BackgroundWorker Object</param>
        /// <param name="e">Arguments necessary to get the stats</param>
        private void ProcessStatsRequest(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgw = sender as BackgroundWorker;

            // Variables needed to create user
            object[] args = e.Argument as object[];
            string serverName = args[0] as string;
            string userName = args[1] as string;
            string password = args[2] as string;

            string errorMessage = null;
            string statsFound = ManagementWrapper.GetAllSessionStats(serverName, userName, password, out errorMessage);

            string filePath = WriteToFile(statsFound);

            // Handle overall status
            bgw.ReportProgress(100, 1 + "~ Stats query complete. Wrote to " + filePath);

        }

        /// <summary>
        /// Write stats to a file in the current directory.
        /// </summary>
        /// <param name="statsFound">Stats to write</param>
        /// <returns>Full path of written file.</returns>
        private string WriteToFile(string statsFound)
        {
            // Write out the results
            string fileFullPath = string.Format("{0}\\Stats_{1:yyyy-MM-dd-HH-mm}.csv", Directory.GetCurrentDirectory(), DateTime.UtcNow);
            System.IO.File.WriteAllLines(fileFullPath, new List<string> { statsFound });

            return fileFullPath;
        }

        /// <summary>
        /// Updates status of user creation to UI
        /// </summary>
        /// <param name="sender">BackgroundWorker Object</param>
        /// <param name="e">Arguments used to update status</param>
        private void CreationStatus(object sender, ProgressChangedEventArgs e)
        {
            int errorCode = Convert.ToInt16((e.UserState as string).Split('~')[0]);
            string msg = (e.UserState as string).Split('~')[1];

            Status.Content = msg;
        }

        /// <summary>
        /// Free fields in UI
        /// </summary>
        /// <param name="sender">BackgoundWorker Object</param>
        /// <param name="e">Arguments to be used</param>
        private void CreationComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            FreeAllFields();
        }
    }
}
