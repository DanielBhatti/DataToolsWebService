using Eli.Data.DataSourceComparison;
using Eli.Data.Predictor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<Comparator>();
builder.Services.AddTransient<DataTypePredictor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapOpenApi();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
