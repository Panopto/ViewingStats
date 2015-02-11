using PanoptoCollectAllStats.SessionManagement;
using PanoptoCollectAllStats.UserManagement;
using PanoptoCollectAllStats.UsageReporting;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Sample C# that uses the Panopto PublicAPI
/// This sample shows how to get all viewing stats for a site through the Panopto PublicAPI
/// </summary>
namespace PanoptoCollectAllStats
{

    public class ManagementWrapper
    {
        private static int MaxPerPage = 25;
        private static int SegmentCount = 100;
        private static Dictionary<string, string> AllUserIds = new Dictionary<string, string>();

        public static string GetAllStatsForSession(string authUserKey, string authPassword, Guid sessionID, string sessionName, string folderName, double? sessionLength)
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

            UsageReportingClient urc = new UsageReportingClient();
            UsageReporting.Pagination pagination = new UsageReporting.Pagination();
            pagination.MaxNumberResults = MaxPerPage;
            pagination.PageNumber = 0;
            DateTime currentTime = DateTime.Now;

            try
            {
                DetailedUsageResponse usageResponse = urc.GetSessionDetailedUsage(
                    usageAuth,
                    sessionID,
                    pagination,
                    new DateTime(2015, 1, 1), // Needs a good start date picker
                    currentTime.AddDays(-1)); // Yesterday

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
                                new DateTime(2015, 1, 1), // Years ago
                                currentTime.AddHours(-10)); // Today

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
            catch (Exception e)
            {
            }


            return sessionStats;
        }

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

        private static string PrintStatsData(
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
                username = GetUserNameFromId(authUserKey, authPassword, userStat.Key);
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

        private static string GetUserNameFromId(string authUserKey, string authPassword, Guid userGuid)
        {
            string username = String.Empty;

            UserManagement.AuthenticationInfo auth = new UserManagement.AuthenticationInfo();
            auth.UserKey = authUserKey;
            auth.Password = authPassword;

            UserManagementClient umc = new UserManagementClient();
            User[] users = umc.GetUsers(auth, new Guid[] { userGuid });

            if (users != null
                && users.Length >= 1
                && !String.IsNullOrWhiteSpace(users[0].UserKey))
            {
                username = users[0].UserKey;
            }

            return username;
        }

        public static string GetAllSessionStats(string authUserKey, string authPassword, out string errorMessage)
        {
            string sessionStats = "Session ID, Session Name, Username, % Viewed, Last View Date, Folder Name\n";
            //List<Guid> sessionIds = new List<Guid>();
            errorMessage = "Query in progress.";

            if (!String.IsNullOrWhiteSpace(authUserKey) && !String.IsNullOrWhiteSpace(authPassword))
            {
                try
                {
                    // Permissions for user management
                    SessionManagement.AuthenticationInfo sessionAuth = new SessionManagement.AuthenticationInfo()
                    {
                        UserKey = authUserKey,
                        Password = authPassword
                    };

                    SessionManagementClient smc = new SessionManagementClient();
                    ListSessionsRequest request = new ListSessionsRequest();
                    SessionManagement.Pagination pagination = new SessionManagement.Pagination();
                    pagination.MaxNumberResults = MaxPerPage;
                    pagination.PageNumber = 0;

                    request.Pagination = pagination;
                    ListSessionsResponse sessionsResponse = smc.GetSessionsList(sessionAuth, request, null);

                    Session[] sessions = sessionsResponse.Results;
                    foreach (Session session in sessions)
                    {
                        sessionStats += GetAllStatsForSession(authUserKey, authPassword, session.Id, session.Name, session.FolderName, session.Duration);
                    }

                    if (sessionsResponse.TotalNumberResults > MaxPerPage)
                    {
                        int totalPages = sessionsResponse.TotalNumberResults / MaxPerPage;
                        for (int page = 1; page < totalPages; page++)
                        {
                            request.Pagination.PageNumber = page;

                            sessionsResponse = smc.GetSessionsList(sessionAuth, request, null);

                            sessions = sessionsResponse.Results;
                            foreach (Session session in sessions)
                            {
                                sessionStats += GetAllStatsForSession(authUserKey, authPassword, session.Id, session.Name, session.FolderName, session.Duration);
                            }
                        }
                    }
                }

                catch (Exception e)
                {
                    errorMessage = e.Message;
                }

            }
            else
            {
                errorMessage = "Please enter username and password.";
            }

            return sessionStats;
        }
    }
}
