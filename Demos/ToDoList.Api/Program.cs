using ToDoList.Domain;
using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using ToDoList.Api.Extensions;
using ToDoList.Services.Interfaces;
using ToDoList.Services.Interfaces.Options;
using ToDoList.Services.Implementations;
using ToDoList.Api.Middleware;
using ToDoList.Domain.Projections.UserAccounts;
using ToDoList.Domain.Projections.TaskLists;
using CloudFabric.EventSourcing.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => {
    options.Filters.Add<GlobalExceptionProblemDetailsFilter>();
});

builder.Services.AddAutoMapper(typeof(UserAccountsService));

builder.Services.AddScoped<IUserAccountsService, UserAccountsService>();
builder.Services.AddScoped<IUserAccessTokensService, UserAccessTokensService>();
builder.Services.AddScoped<ITaskListsService, TaskListsService>();
builder.Services.AddUserInfoProvider();

builder.Services.Configure<UserAccessTokensServiceOptions>(builder.Configuration.GetSection("UserAccessTokensServiceOptions"));

#region User Accounts Projections

var userEventSourcingBuilder = builder.Services.AddPostgresqlEventStore(builder.Configuration.GetConnectionString("Default"), "user-events")
    .AddRepository<AggregateRepository<UserAccount>>()
    .AddRepository<AggregateRepository<UserAccountEmailAddress>>()
    .AddPostgresqlProjections<UserAccountsProjectionItem>(
        builder.Configuration.GetConnectionString("Default"),
        typeof(UserAccountsProjectionBuilder)
    );

#endregion

#region Task Lists Projections

var taskListEventSourcingBuilder = builder.Services.AddPostgresqlEventStore(builder.Configuration.GetConnectionString("Default"), "task-list-events")
    .AddRepository<AggregateRepository<TaskList>>()
    .AddPostgresqlProjections<TaskListProjectionItem>(
        builder.Configuration.GetConnectionString("Default"),
        typeof(TaskListsProjectionBuilder)
    );

#endregion

#region Task Projections

var taskEventSourcingBuilder = builder.Services.AddPostgresqlEventStore(builder.Configuration.GetConnectionString("Default"), "task-events")
    .AddRepository<AggregateRepository<ToDoList.Domain.Task>>()
    .AddPostgresqlProjections<TaskProjectionItem>(
        builder.Configuration.GetConnectionString("Default"),
        typeof(TasksProjectionBuilder)
    );

#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

# region Database init
//var initScope = app.Services.CreateScope();
//var eventStore = initScope.ServiceProvider.GetRequiredService<IEventStore>();
//await eventStore.Initialize();
#endregion

await userEventSourcingBuilder.ProjectionsEngine.StartAsync(Environment.MachineName);
await taskListEventSourcingBuilder.ProjectionsEngine.StartAsync(Environment.MachineName);
await taskEventSourcingBuilder.ProjectionsEngine.StartAsync(Environment.MachineName);

app.Run();
