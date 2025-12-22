using CsvHelper;
using CsvHelper.Configuration;
using Eli.Data.DataSourceComparison;
using Eli.Data.DataSourceComparison.DataSources;
using Eli.Data.DataTypes;
using Eli.Data.Predictor;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace DataToolsWebService.Controllers;

[ApiController]
[Route("datasource")]
public sealed class DataSourceController : ControllerBase
{
    private Comparator Comparator { get; }
    private DataTypePredictor Predictor { get; }

    public DataSourceController(Comparator comparator, DataTypePredictor predictor) => (Comparator, Predictor) = (comparator, predictor);

    [HttpPost("predict")]
    public async Task<ActionResult<List<PredictionResponse>>> Predict([FromForm] PredictionRequest request, CancellationToken ct)
    {
        if(request.Csv is null) return BadRequest($"{nameof(request.Csv)} is required.");
        var predictions = new List<PredictionResponse>();
        try
        {
            var dataSource = await CsvDataSource.FromUploadAsync(request.Csv, ct);
            predictions.AddRange(dataSource.FieldToCollection.Select(ftv => new PredictionResponse() { DataType = Predictor.Predict(ftv.Value), Field = ftv.Key }));
        }
        catch(Exception ex)
        {
            return BadRequest(ex.Message);
        }
        return Ok(predictions);
    }

    [HttpPost("compare")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(List<ComparisonResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<ComparisonResult>>> Compare([FromForm] CompareCsvRequest request, [FromQuery] HashSet<ComparisonResultType>? include,
    [FromQuery] HashSet<ComparisonResultType>? exclude, CancellationToken ct)
    {
        if(request.LeftCsv is null || request.LeftCsv.Length == 0) return BadRequest($"{nameof(request.LeftCsv)} is required.");
        if(request.RightCsv is null || request.RightCsv.Length == 0) return BadRequest($"{nameof(request.RightCsv)} is required.");

        var primaryKeys = NormalizePrimaryKeys(request.PrimaryKeys);
        if(primaryKeys.Count == 0) return BadRequest("At least one primary key is required.");

        try
        {
            var left = await CsvDataSource.FromUploadAsync(request.LeftCsv, ct);
            var right = await CsvDataSource.FromUploadAsync(request.RightCsv, ct);
            var results = Comparator.Compare(left, right, primaryKeys);

            if(include is { Count: > 0 }) results = results.Where(r => include.Contains(r.ComparisonResultType)).ToList();
            if(exclude is { Count: > 0 }) results = results.Where(r => !exclude.Contains(r.ComparisonResultType)).ToList();
            return Ok(results);
        }
        catch(Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static IReadOnlyCollection<string> NormalizePrimaryKeys(List<string>? primaryKeys)
    {
        if(primaryKeys is null || primaryKeys.Count == 0) return Array.Empty<string>();

        return primaryKeys
            .SelectMany(p => (p ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public sealed class CompareCsvRequest
    {
        public IFormFile? LeftCsv { get; init; }
        public IFormFile? RightCsv { get; init; }

        public List<string>? PrimaryKeys { get; init; }
    }

    public sealed class PredictionRequest
    {
        public required IFormFile Csv { get; init; }
    }

    public sealed record class PredictionResponse
    {
        public required string Field { get; init; }
        public required DataType DataType { get; init; }
    }

    private sealed class CsvDataSource : DataSource
    {
        public required string Name { get; init; }
        public DataSourceType DataSourceType { get; init; } = default!;
        public DataFormat DataFormat { get; init; } = default!;
        public required IReadOnlySet<string> HeaderKeys { get; init; }
        public required IReadOnlyCollection<IReadOnlyDictionary<string, string>> FieldToValueCollection { get; init; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> FieldToCollection =>
            FieldToValueCollection
                .SelectMany(dict => dict)
                .GroupBy(kv => kv.Key)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<string>)[.. g.Select(kv => kv.Value)]
                );

        public static async Task<CsvDataSource> FromUploadAsync(IFormFile csv, CancellationToken ct)
        {
            await using var stream = csv.OpenReadStream();
            using var reader = new StreamReader(stream);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                MissingFieldFound = null,
                DetectDelimiter = true
            };

            using var csvReader = new CsvReader(reader, config);

            await csvReader.ReadAsync();
            csvReader.ReadHeader();
            var headers = csvReader.HeaderRecord ?? throw new InvalidOperationException("CSV header row is missing.");

            var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
            var rows = new List<IReadOnlyDictionary<string, string>>();

            while(await csvReader.ReadAsync())
            {
                ct.ThrowIfCancellationRequested();

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach(var header in headers) dict[header] = csvReader.GetField(header) ?? string.Empty;
                rows.Add(dict);
            }

            return new CsvDataSource
            {
                Name = "CSV File",
                HeaderKeys = headerSet,
                FieldToValueCollection = rows
            };
        }
    }
}
