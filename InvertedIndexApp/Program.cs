using InvertedIndexApp;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IndexService>();

var app = builder.Build();

app.MapGet("/is-indexed", (IndexService indexService) => indexService.IsIndexed);

app.MapPost("/index", ([FromQuery] int threadsCount, IndexService indexService) => new
{
    duration = indexService.Index()
});

app.MapGet("/query", ([FromQuery] string word, IndexService indexService) => {
    var result = indexService.Query(word);
    if (result is null) return Results.NotFound();
    return Results.Ok(result);
});

app.Run();
