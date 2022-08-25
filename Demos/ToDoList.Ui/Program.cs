using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using ToDoList.Ui.Authentication;
using ToDoList.Ui.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddHttpClient("ServerApi",
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:60000");
    });
    // this does not work and should not work since factory creates new  scope for every handler. 
    // but we store auth state in scope since blazor server has one scope per connected client.
    //.AddHttpMessageHandler<AccessTokenMessageHandler>();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<IServiceCommunicationProvider, HttpJsonServiceCommunicationProvider>();

builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();

builder.Services.AddScoped<TokenAuthenticationStateProvider, TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());

builder.Services.AddScoped<AccessTokenMessageHandler>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<TaskListsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
