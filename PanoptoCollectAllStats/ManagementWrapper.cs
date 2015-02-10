using PanoptoCollectAllStats.SessionManagement;
using PanoptoCollectAllStats.UserManagement;
using PanoptoCollectAllStats.UsageReporting;
using System;
using System.Collections.Generic;

/// <summary>
/// Sample C# that uses the Panopto PublicAPI
/// This sample shows how to get all viewing stats for a site through the Panopto PublicAPI
/// </summary>
namespace PanoptoCollectAllStats
{

    public class ManagementWrapper
    {
        private static int MaxPerPage = 25;
        private static Dictionary<string, string> AllUserIds = new Dictionary<string, string>();

        public static string GetAllStatsForSession(string authUserKey, string authPassword, Guid sessionID, string sessionName, string folderName, double? sessionLength)
        {
            string sessionStats = String.Empty;

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

            DetailedUsageResponse usageResponse = urc.GetSessionDetailedUsage(
                usageAuth, 
                sessionID, 
                pagination, 
                new DateTime(2015, 1, 1), // Years ago
                currentTime.AddSeconds(-1)); // Today

            if (   usageResponse != null
                && usageResponse.TotalNumberResponses > 0)
            {
                foreach (DetailedUsageResponseItem responseItem in usageResponse.PagedResponses)
                {
                    string userId = responseItem.UserId.ToString();
                    string username = userId;
                    if (AllUserIds.ContainsKey(userId))
                    {
                        username = AllUserIds[userId];
                    }
                    else
                    {
                        // Get the username.
                        username = GetUserNameFromId(authUserKey, authPassword, responseItem.UserId);
                    }

                    // Session ID, Session Name, username, start position, length, date, Folder
                    string usageFormat = "{0}, {1}, {2} , {3}, {4}, {5}, {6}, {7} \n";
                    string individualStatsString = String.Format(
                        usageFormat,
                        sessionID.ToString(),
                        sessionName,
                        username,
                        responseItem.StartPosition,
                        responseItem.SecondsViewed,
                        responseItem.SecondsViewed / sessionLength * 100,
                        responseItem.Time,
                        folderName);
                    sessionStats += individualStatsString;
                }

                if (usageResponse.TotalNumberResponses > usageResponse.PagedResponses.Length)
                {
                    // Get more data

                }

            }
            else
            {
                string noneFormat = "{0}, {1}, none,,,,, {2}, \n";

                sessionStats = String.Format(noneFormat, sessionID.ToString(), sessionName, folderName);
            }

            return sessionStats;
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
            string sessionStats = "Session ID, Session Name, Username, Start Position (sec), Length (sec), % Viewed, Date Viewed, Folder,\n";
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
                        //sessionIds.Add(sessionGuid);
                        sessionStats += GetAllStatsForSession(authUserKey, authPassword, session.Id, session.Name, session.FolderName, session.Duration);
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

            //return sessionIds;
            return sessionStats;
        }
    }
}
