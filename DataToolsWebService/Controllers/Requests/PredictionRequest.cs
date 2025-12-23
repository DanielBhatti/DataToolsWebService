namespace DataToolsWebService.Controllers.Requests;

public sealed class PredictionRequest
{
    public required IFormFile Csv { get; init; }
}
