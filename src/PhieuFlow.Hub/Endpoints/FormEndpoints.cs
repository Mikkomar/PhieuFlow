namespace PhieuFlow.Hub.Endpoints;

// Aggregates the form-related route groups. Each group lives in its own file.
public static class FormEndpoints
{
    public static void MapFormEndpoints(this WebApplication app)
    {
        app.MapFormManagementEndpoints();
        app.MapFormVersionEndpoints();
        app.MapFormPublishEndpoints();
        app.MapPublishedFormEndpoints();
        app.MapFormSubmissionEndpoints();
    }
}
