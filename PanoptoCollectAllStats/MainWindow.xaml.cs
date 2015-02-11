using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Timers;
using System.Windows;
using System.Windows.Forms;

namespace PanoptoCollectAllStats
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static bool selfSigned = true; // Target server is a self-signed server
        private static bool hasBeenInitialized = false;
        private static System.Timers.Timer timer;

        public MainWindow()
        {
            InitializeComponent();

            if (selfSigned)
            {
                // For self-signed servers
                EnsureCertificateValidation();
            }
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

            object[] args = new object[] { UserID.Text, UserPassword.Password };

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
            string userName = args[0] as string;
            string password = args[1] as string;


            string errorMessage = null;
            string statsFound = ManagementWrapper.GetAllSessionStats(userName, password, out errorMessage);

            // Write out the results
            string fileName = "Stats_" + DateTime.Now.ToString("dd_MM_yyyy_hh_mm") + ".csv";
            System.IO.File.WriteAllLines(@"C:\temp\" + fileName, new List<string> { statsFound });


            // Handle overall status
            bgw.ReportProgress(100, 1 + "~ Stats query complete.");

        }

        private void WriteToFile(string statsFound)
        {
            
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

        //========================= Needed to use self-signed servers

        /// <summary>
        /// Ensures that our custom certificate validation has been applied
        /// </summary>
        public static void EnsureCertificateValidation()
        {
            if (!hasBeenInitialized)
            {
                ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(CustomCertificateValidation);
                hasBeenInitialized = true;
            }
        }

        /// <summary>
        /// Ensures that server certificate is authenticated
        /// </summary>
        private static bool CustomCertificateValidation(object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors error)
        {
            return true;
        }
    }
}
