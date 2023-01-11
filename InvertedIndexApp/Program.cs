using InvertedIndexApp;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IndexService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/is-indexed", (IndexService indexService) => indexService.IsIndexed);

app.MapPost("/index", ([FromQuery] int? threadsCount, IndexService indexService) => new
{
    duration = indexService.Index(threadsCount ?? 1)
});

app.MapGet("/query", ([FromQuery] string word, IndexService indexService) => {
    if (!indexService.IsIndexed) return Results.BadRequest();
    var result = indexService.Query(word);
    if (result is null) return Results.NotFound();
    return Results.Ok(result);
});

app.Run();
