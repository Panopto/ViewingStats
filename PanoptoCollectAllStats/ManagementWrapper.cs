using PanoptoCollectAllStats.SessionManagement;
using PanoptoCollectAllStats.UserManagement;
using PanoptoCollectAllStats.UsageReporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

/// <summary>
/// Sample C# that uses the Panopto PublicAPI
/// This sample shows how to get all viewing stats for a site through the Panopto PublicAPI
/// </summary>
namespace PanoptoCollectAllStats
{

    public class ManagementWrapper
    {
        // Number of total sessions may be large and may not complete within reasonable time.
        // As this is demonstration purpose, we limit the maximum sesion mumber with some constant.
        private const int CapSessions = 100;

        private const int MaxPerPage = 25;
        private const int SegmentCount = 100;
        private static Dictionary<string, string> AllUserIds = new Dictionary<string, string>();
        private static DateTime EndDate = DateTime.UtcNow;
        private static DateTime BeginDate = EndDate - TimeSpan.FromDays(30);

        private const string SessionManagementEndpointFormat = "https://{0}/Panopto/PublicAPI/4.6/SessionManagement.svc";
        private const string UsageReportingEndpointFormat = "https://{0}/Panopto/PublicAPI/4.6/UsageReporting.svc";
        private const string UserManagementEndpointFormat = "https://{0}/Panopto/PublicAPI/4.6/UserManagement.svc";

        /// <summary>
        /// Get stats for designated session
        /// </summary>
        /// <param name="serverName">Server name</param>
        /// <param name="authUserKey">User name</param>
        /// <param name="authPassword">Password</param>
        /// <param name="sessionID">Session ID</param>
        /// <param name="sessionName">Session name</param>
        /// <param name="folderName">Folder name</param>
        /// <param name="sessionLength">Session length</param>
        /// <returns>String containing stats for given session</returns>
        public static string GetAllStatsForSession(string serverName, string authUserKey, string authPassword, Guid sessionID, string sessionName, string folderName, double? sessionLength)
        {
            string sessionStats = String.Empty;
            List<DetailedUsageResponseItem> allResponsesForSession = new List<DetailedUsageResponseItem>();

            // Get detailed stats
            // Permissions for user management
            UsageReporting.AuthenticationInfo usageAuth = new UsageReporting.AuthenticationInfo()
            {
                UserKey = authUserKey,
                Password = authPassword
            };

            // Sanitize the session name for a CSV
            if (sessionName.Contains(","))
            {
                sessionName = sessionName.Replace(",", "");
            }
            if (folderName.Contains(","))
            {
                folderName = folderName.Replace(",", "");
            }

            UsageReportingClient urc = new UsageReportingClient(
                new BasicHttpsBinding(), new EndpointAddress(string.Format(UsageReportingEndpointFormat, serverName)));
            UsageReporting.Pagination pagination = new UsageReporting.Pagination();
            pagination.MaxNumberResults = MaxPerPage;
            pagination.PageNumber = 0;

            try
            {
                // Get all detailed usage and store in allResponsesForSession
                DetailedUsageResponse usageResponse = urc.GetSessionDetailedUsage(
                    usageAuth,
                    sessionID,
                    pagination,
                    BeginDate,
                    EndDate);

                if (usageResponse != null
                    && usageResponse.TotalNumberResponses > 0)
                {
                    foreach (DetailedUsageResponseItem responseItem in usageResponse.PagedResponses)
                    {
                        allResponsesForSession.Add(responseItem);
                    }

                    if (usageResponse.TotalNumberResponses > usageResponse.PagedResponses.Length)
                    {
                        int totalPages = usageResponse.TotalNumberResponses / MaxPerPage;
                        // Get more data
                        for (int page = 1; page < totalPages; page++)
                        {
                            pagination.PageNumber = page;
                            usageResponse = urc.GetSessionDetailedUsage(
                                usageAuth,
                                sessionID,
                                pagination,
                                BeginDate,
                                EndDate);

                            foreach (DetailedUsageResponseItem responseItem in usageResponse.PagedResponses)
                            {
                                allResponsesForSession.Add(responseItem);
                            }
                        }
                    }

                    // If we have any stats, collate them by user
                    if (allResponsesForSession.Count > 0)
                    {
                        Dictionary<Guid, DateTime> userViewDates;
                        Dictionary<Guid, bool[]> userStatsCollection = CollateStatsByUser(allResponsesForSession, sessionLength, out userViewDates);
                        foreach (KeyValuePair<Guid, bool[]> userStat in userStatsCollection)
                        {
                            sessionStats += PrintStatsData(
                                serverName,
                                authUserKey,
                                authPassword,
                                userStat,
                                sessionID,
                                sessionName,
                                folderName,
                                userViewDates[userStat.Key]);
                        }

                    }

                }
                else
                {
                    string noneFormat = "{0}, {1}, none,,, {2}\n";
                    sessionStats = String.Format(noneFormat, sessionID.ToString(), sessionName, folderName);
                }
            }
            catch (Exception)
            {
            }


            return sessionStats;
        }

        /// <summary>
        /// Collate stats for session by user
        /// </summary>
        /// <param name="allResponsesForSession">All stats response for session</param>
        /// <param name="sessionLength">Session length</param>
        /// <param name="lastViews">User's last view dates for session</param>
        /// <returns>Dictionary of user and their viewing stats for session</returns>
        private static Dictionary<Guid, bool[]> CollateStatsByUser(List<DetailedUsageResponseItem> allResponsesForSession, double? sessionLength, out Dictionary<Guid, DateTime> lastViews)
        {
            // Map usernames to segments watched.
            Dictionary<Guid, bool[]> userStats = new Dictionary<Guid, bool[]>();
            lastViews = new Dictionary<Guid, DateTime>();

            if (sessionLength.HasValue)
            {
                double sessionSegmentLength = sessionLength.Value / SegmentCount;

                foreach (DetailedUsageResponseItem responseItem in allResponsesForSession)
                {
                    if (userStats.ContainsKey(responseItem.UserId))
                    {
                        // sanity checks 
                        if (responseItem.StartPosition < sessionLength
                            && responseItem.SecondsViewed > 0
                            && responseItem.SecondsViewed < sessionLength
                            && (responseItem.StartPosition + responseItem.SecondsViewed) < sessionLength)
                        {
                            int firstSegement = Convert.ToInt32(Math.Truncate(responseItem.StartPosition / sessionSegmentLength));
                            int lastSegment = Convert.ToInt32(Math.Truncate((responseItem.StartPosition + responseItem.SecondsViewed) / sessionSegmentLength));

                            for (int idx = firstSegement; idx < lastSegment; idx++)
                            {
                                userStats[responseItem.UserId][idx] = true;
                            }
                        }

                        // Check for a more recent view
                        if (responseItem.Time > lastViews[responseItem.UserId])
                        {
                            lastViews[responseItem.UserId] = responseItem.Time;
                        }
                    }
                    else
                    {
                        bool[] userSegments = new bool[SegmentCount];
                        // sanity checks 
                        if (responseItem.StartPosition < sessionLength
                            && responseItem.SecondsViewed > 0
                            && responseItem.SecondsViewed < sessionLength
                            && (responseItem.StartPosition + responseItem.SecondsViewed) < sessionLength)
                        {
                            int firstSegement = Convert.ToInt32(Math.Truncate(responseItem.StartPosition / sessionSegmentLength));
                            int lastSegment = Convert.ToInt32(Math.Truncate((responseItem.StartPosition + responseItem.SecondsViewed) / sessionSegmentLength));

                            for (int idx = firstSegement; idx < lastSegment; idx++)
                            {
                                userSegments[idx] = true;
                            }
                        }
                        userStats.Add(responseItem.UserId, userSegments);
                        lastViews.Add(responseItem.UserId, responseItem.Time);
                    }
                }
            }
            return userStats;
        }

        /// <summary>
        /// Generate a string with stats with given data
        /// </summary>
        /// <param name="serverName">Server name</param>
        /// <param name="authUserKey">User name</param>
        /// <param name="authPassword">Password</param>
        /// <param name="userStat">User stat for session</param>
        /// <param name="sessionID">Session ID</param>
        /// <param name="sessionName">Session name</param>
        /// <param name="folderName">Folder name</param>
        /// <param name="lastViewed">User's last viewed date for session</param>
        /// <returns>String containing stats for session for given user</returns>
        private static string PrintStatsData(
            string serverName,
            string authUserKey, 
            string authPassword,
            KeyValuePair<Guid, bool[]> userStat,
            Guid sessionID,
            string sessionName,
            string folderName,
            DateTime lastViewed)
        {
            string userId = userStat.Key.ToString();
            string username = userId;
            if (AllUserIds.ContainsKey(userId))
            {
                username = AllUserIds[userId];
            }
            else
            {
                // Get the username.
                username = GetUserNameFromId(serverName, authUserKey, authPassword, userStat.Key);
                // Save it for later.
                AllUserIds[userId] = username;
            }

            // Session ID, Session Name, Username, % Viewed, Last View Date, Folder Name
            string usageFormat = "{0}, {1}, {2} , {3}, {4}, {5}\n";
            bool[] segments = userStat.Value;
            return String.Format(
                usageFormat,
                sessionID.ToString(),
                sessionName,
                username,
                segments.Count(c => c).ToString(),
                lastViewed,
                folderName);
        }

        /// <summary>
        /// Get user name from user id
        /// </summary>
        /// <param name="serverName">Server Name</param>
        /// <param name="authUserKey">User name</param>
        /// <param name="authPassword">Password</param>
        /// <param name="userGuid">user id</param>
        /// <returns>User name for given user id</returns>
        private static string GetUserNameFromId(string serverName, string authUserKey, string authPassword, Guid userGuid)
        {
            string username = String.Empty;

            UserManagement.AuthenticationInfo auth = new UserManagement.AuthenticationInfo();
            auth.UserKey = authUserKey;
            auth.Password = authPassword;

            UserManagementClient umc = new UserManagementClient(
                new BasicHttpsBinding(), new EndpointAddress(string.Format(UserManagementEndpointFormat, serverName)));
            User[] users = umc.GetUsers(auth, new Guid[] { userGuid });

            if (users != null
                && users.Length >= 1
                && !String.IsNullOrWhiteSpace(users[0].UserKey))
            {
                username = users[0].UserKey;
            }

            return username;
        }

        /// <summary>
        /// Get stats for all sessions user has access to.
        /// </summary>
        /// <param name="serverName">Server name</param>
        /// <param name="authUserKey">User name</param>
        /// <param name="authPassword">Password</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>String containing stats of all sessions</returns>
        public static string GetAllSessionStats(string serverName, string authUserKey, string authPassword, out string errorMessage)
        {
            string sessionStats = "Session ID, Session Name, Username, % Viewed, Last View Date, Folder Name\n";
            //List<Guid> sessionIds = new List<Guid>();
            errorMessage = "Query in progress.";

            if (!String.IsNullOrWhiteSpace(serverName) && !String.IsNullOrWhiteSpace(authUserKey) && !String.IsNullOrWhiteSpace(authPassword))
            {
                try
                {
                    // Permissions for user management
                    SessionManagement.AuthenticationInfo sessionAuth = new SessionManagement.AuthenticationInfo()
                    {
                        UserKey = authUserKey,
                        Password = authPassword
                    };

                    SessionManagementClient smc = new SessionManagementClient(
                        new BasicHttpsBinding(), new EndpointAddress(string.Format(SessionManagementEndpointFormat, serverName)));
                    ListSessionsRequest request = new ListSessionsRequest();

                    SessionManagement.Pagination pagination = new SessionManagement.Pagination();
                    pagination.MaxNumberResults = MaxPerPage;
                    pagination.PageNumber = 0;
                    request.Pagination = pagination;

                    // Query newer sessions first.
                    request.SortBy = SessionSortField.Date;
                    request.SortIncreasing = false;

                    int totalPages = int.MaxValue; // not set yet

                    while (request.Pagination.PageNumber < totalPages)
                    {
                        ListSessionsResponse sessionsResponse = smc.GetSessionsList(sessionAuth, request, null);

                        Session[] sessions = sessionsResponse.Results;
                        foreach (Session session in sessions)
                        {
                            sessionStats += GetAllStatsForSession(serverName, authUserKey, authPassword, session.Id, session.Name, session.FolderName, session.Duration);
                        }

                        // This is examined at the first call.
                        if (totalPages == int.MaxValue)
                        {
                            int maxEntries = Math.Min(sessionsResponse.TotalNumberResults, CapSessions);
                            totalPages = (maxEntries + MaxPerPage - 1) / MaxPerPage;
                        }

                        request.Pagination.PageNumber++;
                    }
                }

                catch (Exception e)
                {
                    errorMessage = e.Message;
                }

            }
            else
            {
                errorMessage = "Please enter servername, username, and password.";
            }

            return sessionStats;
        }
    }
}
