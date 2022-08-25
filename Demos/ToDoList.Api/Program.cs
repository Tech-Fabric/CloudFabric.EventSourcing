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


var eventStore = new PostgresqlEventStore(builder.Configuration.GetConnectionString("Default"), "events");
builder.Services.AddScoped<IEventStore>(sp => eventStore);

#region User Accounts Projections
builder.Services.AddScoped<AggregateRepository<UserAccount>>();
builder.Services.AddScoped<AggregateRepository<UserAccountEmailAddress>>();

var userAccountsEventStoreObserver = new PostgresqlEventStoreEventObserver(eventStore);
var userAccountsProjectionRepository = new PostgresqlProjectionRepository<UserAccountsProjectionItem>(builder.Configuration.GetConnectionString("Default"));

var userAccountsProjectionsEngine = new ProjectionsEngine();
userAccountsProjectionsEngine.SetEventsObserver(userAccountsEventStoreObserver);
userAccountsProjectionsEngine.AddProjectionBuilder(new UserAccountsProjectionBuilder(userAccountsProjectionRepository));
#endregion

#region Task Lists Projections
builder.Services.AddScoped<AggregateRepository<TaskList>>();

var taskListsEventStoreObserver = new PostgresqlEventStoreEventObserver(eventStore);
var taskListsProjectionRepository = new PostgresqlProjectionRepository<TaskListProjectionItem>(builder.Configuration.GetConnectionString("Default"));
builder.Services.AddScoped<IProjectionRepository<TaskListProjectionItem>>((sp) => taskListsProjectionRepository);

var taskListsProjectionsEngine = new ProjectionsEngine();
taskListsProjectionsEngine.SetEventsObserver(taskListsEventStoreObserver);
taskListsProjectionsEngine.AddProjectionBuilder(new TaskListsProjectionBuilder(taskListsProjectionRepository));
#endregion

#region Task Projections
builder.Services.AddScoped<AggregateRepository<ToDoList.Domain.Task>>();

var tasksEventStoreObserver = new PostgresqlEventStoreEventObserver(eventStore);
var tasksProjectionRepository = new PostgresqlProjectionRepository<TaskProjectionItem>(builder.Configuration.GetConnectionString("Default"));
builder.Services.AddScoped<IProjectionRepository<TaskProjectionItem>>((sp) => tasksProjectionRepository);

var tasksProjectionsEngine = new ProjectionsEngine();
tasksProjectionsEngine.SetEventsObserver(tasksEventStoreObserver);
tasksProjectionsEngine.AddProjectionBuilder(new TasksProjectionBuilder(tasksProjectionRepository));
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
var initScope = app.Services.CreateScope();
//var eventStore = initScope.ServiceProvider.GetRequiredService<IEventStore>();
await eventStore.Initialize();
#endregion

await taskListsProjectionsEngine.StartAsync(Environment.MachineName);

app.Run();
