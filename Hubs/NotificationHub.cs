using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

/*
    Client gets notified about all notifications.
    Notifications for pending and in progress are always shown.
    Success and failure are shown one by one or grouped depending on:
        If the notifications are recent (i.e., less than 10 minutes old) they are shown one by one
        Otherwise they are grouped (one for success, one for failure)

    If a succes/failure notification has been shown before it is not shown again.
    For that purpose the client needs to track when the last notification was shown.
*/
namespace WSTKNG.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task CheckForUpdate(List<string> ids)
        {
            var monitor = JobStorage.Current.GetMonitoringApi();

            // get an update on existing jobs
            var jobs = ids.Select(id => {
                var details = monitor.JobDetails(id);

                return new {
                    id = id,
                    name = details.Job.Method.Name,
                    args = details.Job.Args,
                    state = details.History.First().StateName,
                };
            });

            await Clients.Caller.SendAsync("JobUpdate", jobs);
        }
    }
}