// TRIP CHANGE WORKSHOP

using System;
using System.Text.Json;
using TripChangeWorkshop;

public class TripChange
{
    public string type { get; set; }
    public LatLong current_location { get; set; }
    public string username {get; set;} // 3) add remaining fields from docs (username, route_success, load_id)
    public string load_id {get; set;}
    public bool route_success {get; set;}
    public TripPolicyResult[] trip_policy_results {get; set;} // 7.c) add trip policy result to TripChange model
}

public class Program
{
    static public void Main(string[] args)
    {
        MessageClient client = new MessageClient(System.Environment.GetEnvironmentVariable("ELEOS_API_KEY"));
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        Database database = new Database();
        database.initializeLoads();

        app.MapGet("/services", (HttpContext context) => {
          var host = context.Request.Host.Value;
          string services = @$"
          Enter the follwing urls into their appropriate boxes in the Service Config page... </br></br>
          <b>Authentication Service:</b> https://{host}/authenticate <br/><br/>
          <b>Load Service:</b> https://{host}/loads <br/><br/>
          <b>TODO Service:</b> https://{host}/todos <br/><br/>
          <b>Trip Change Service:</b> https://{host}/tripchanges
          ";
          return services;
        });
      
        app.MapGet("/authenticate/{api_token}", () =>
        {
            return database.retrieveUser();
        });

        app.MapGet("/loads", () =>
        {
            return database.getLoads();
        });

        app.MapGet("/todos", () =>
        {
            return database.getToDos();
        });

        app.MapGet("/tripchanges", () =>
        {
            // 2) return the trip changes we are store (for deubugging)
            return database.getTripChanges();
        });

        app.MapPut("/tripchanges/{uuid}", (TripChange tripChange) =>
              {
                  // 1) store trip change
                  database.storeTripChange(tripChange);

                  // 7) get trip policy off trip change
                  //   7.a) add trip policy result to trip change model (top of the file) 
                  //   7.b) disable/enable nav on the load if policy is violated/passed 
                  //   7.c) send load update to force refresh the load
                  //     TripPolicyResult tripPolicy = tripChange.trip_policy_results.FirstOrDefault(x => x.code == "MAX-MILES");

                  TripPolicyResult tripPolicy = tripChange.trip_policy_results.FirstOrDefault(x => x.code == "MAX-MILES");
                  if (tripPolicy != null) {
                    if(tripPolicy.violation == true){
                      database.updatePreventNavigation(tripChange.load_id, true);
                    }
                    else {
                      database.updatePreventNavigation(tripChange.load_id, false);
                    }

                    client.sendLoadUpdate(tripChange.username, tripChange.load_id);
                  }
              

                  switch(tripChange.type){ // 4) add switch statement and move-stop/change-stop-location case
                    case "move-stop":
                    case "change-stop-location":
                      client.sendMessage(tripChange.username, "We received your load update. Please follow up with dispatch.");
                      break;
                    case "issue-report": // 5) add switch statement and issue-report case
                      ToDo t = new ToDo("Issue Report Follow-up", "Please describe the issue you had during navigation", "2024-11-30T22:30:00.0000+00:00", "ISSUE-REPORT");
                      database.storeToDo(t);
                      break;
                    case "mute-policy": // (OPTIONAL) add case for "mute-policy" and re-enable navigation on load
                    database.updatePreventNavigation(tripChange.load_id, false);
                    client.sendLoadUpdate(tripChange.username, tripChange.load_id);
                    break;
                  }

              }
          );

        app.Run();
    }
}
