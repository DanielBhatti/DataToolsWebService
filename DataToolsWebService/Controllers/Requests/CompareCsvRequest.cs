namespace DataToolsWebService.Controllers.Requests;

public sealed class CompareCsvRequest
{
    public IFormFile? LeftCsv { get; init; }
    public IFormFile? RightCsv { get; init; }

    public List<string>? PrimaryKeys { get; init; }
}
