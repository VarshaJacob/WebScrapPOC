using IngredientBlazor.Data;
using IngredientBlazor.Data.Options;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<BingSearchService>();
builder.Services.AddSingleton<WebScrapervice>();
builder.Services.AddSingleton<OpenAiService>();

var openAiOptions = new OpenAiOptions();
builder.Configuration.GetSection("OpenAi").Bind(openAiOptions);
builder.Services.AddSingleton(openAiOptions);

var bingSearchOptions = new BingSearchOptions();
builder.Configuration.GetSection("BingSearch").Bind(bingSearchOptions);
builder.Services.AddSingleton(bingSearchOptions);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
