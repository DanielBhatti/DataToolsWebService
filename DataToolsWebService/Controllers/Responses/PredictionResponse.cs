namespace DataToolsWebService.Controllers.Responses;

public sealed record class PredictionResponse
{
    public required string Field { get; init; }
    public required DataType DataType { get; init; }
}