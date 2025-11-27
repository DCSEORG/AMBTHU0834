using ExpenseManagement.Models;
using ExpenseManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Expense Management API", Version = "v1" });
});

// Register application services
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// API Endpoints
var apiGroup = app.MapGroup("/api");

// Expenses API
apiGroup.MapGet("/expenses", async (IExpenseService service, string? filter) =>
{
    try
    {
        var expenses = await service.GetAllExpensesAsync(filter);
        return Results.Ok(expenses);
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("GetExpenses").WithOpenApi();

apiGroup.MapGet("/expenses/pending", async (IExpenseService service) =>
{
    try
    {
        var expenses = await service.GetPendingExpensesAsync();
        return Results.Ok(expenses);
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("GetPendingExpenses").WithOpenApi();

apiGroup.MapGet("/expenses/{id}", async (IExpenseService service, int id) =>
{
    try
    {
        var expense = await service.GetExpenseByIdAsync(id);
        return expense is not null ? Results.Ok(expense) : Results.NotFound();
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("GetExpenseById").WithOpenApi();

apiGroup.MapPost("/expenses", async (IExpenseService service, ExpenseCreateDto dto) =>
{
    try
    {
        var expense = await service.CreateExpenseAsync(dto);
        return Results.Created($"/api/expenses/{expense.ExpenseId}", expense);
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("CreateExpense").WithOpenApi();

apiGroup.MapPut("/expenses/{id}", async (IExpenseService service, int id, ExpenseUpdateDto dto) =>
{
    try
    {
        dto.ExpenseId = id;
        var result = await service.UpdateExpenseAsync(dto);
        return result ? Results.NoContent() : Results.NotFound();
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("UpdateExpense").WithOpenApi();

apiGroup.MapPost("/expenses/{id}/submit", async (IExpenseService service, int id) =>
{
    try
    {
        var result = await service.SubmitExpenseAsync(id);
        return result ? Results.Ok(new { message = "Expense submitted successfully" }) : Results.NotFound();
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("SubmitExpense").WithOpenApi();

apiGroup.MapPost("/expenses/{id}/approve", async (IExpenseService service, int id, int reviewerId = 2) =>
{
    try
    {
        var result = await service.ApproveExpenseAsync(id, reviewerId);
        return result ? Results.Ok(new { message = "Expense approved successfully" }) : Results.NotFound();
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("ApproveExpense").WithOpenApi();

apiGroup.MapPost("/expenses/{id}/reject", async (IExpenseService service, int id, int reviewerId = 2) =>
{
    try
    {
        var result = await service.RejectExpenseAsync(id, reviewerId);
        return result ? Results.Ok(new { message = "Expense rejected" }) : Results.NotFound();
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("RejectExpense").WithOpenApi();

apiGroup.MapDelete("/expenses/{id}", async (IExpenseService service, int id) =>
{
    try
    {
        var result = await service.DeleteExpenseAsync(id);
        return result ? Results.NoContent() : Results.NotFound();
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("DeleteExpense").WithOpenApi();

// Categories API
apiGroup.MapGet("/categories", async (IExpenseService service) =>
{
    try
    {
        var categories = await service.GetCategoriesAsync();
        return Results.Ok(categories);
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("GetCategories").WithOpenApi();

// Statuses API
apiGroup.MapGet("/statuses", async (IExpenseService service) =>
{
    try
    {
        var statuses = await service.GetStatusesAsync();
        return Results.Ok(statuses);
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("GetStatuses").WithOpenApi();

// Users API
apiGroup.MapGet("/users", async (IExpenseService service) =>
{
    try
    {
        var users = await service.GetUsersAsync();
        return Results.Ok(users);
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("GetUsers").WithOpenApi();

// Dashboard Stats API
apiGroup.MapGet("/dashboard/stats", async (IExpenseService service) =>
{
    try
    {
        var stats = await service.GetDashboardStatsAsync();
        return Results.Ok(stats);
    }
    catch (DatabaseException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
}).WithName("GetDashboardStats").WithOpenApi();

// Chat API
apiGroup.MapPost("/chat", async (IChatService chatService, ChatRequest request) =>
{
    var response = await chatService.SendMessageAsync(request);
    return Results.Ok(response);
}).WithName("Chat").WithOpenApi();

apiGroup.MapGet("/chat/status", (IChatService chatService) =>
{
    return Results.Ok(new { configured = chatService.IsConfigured });
}).WithName("ChatStatus").WithOpenApi();

app.Run();
