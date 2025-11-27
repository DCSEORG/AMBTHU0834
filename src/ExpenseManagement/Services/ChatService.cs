using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.Text.Json;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
    bool IsConfigured { get; }
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;
    private readonly string? _endpoint;
    private readonly string? _deploymentName;
    private readonly string? _managedIdentityClientId;
    private readonly bool _isConfigured;

    public bool IsConfigured => _isConfigured;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        
        _endpoint = _configuration["OpenAI:Endpoint"];
        _deploymentName = _configuration["OpenAI:DeploymentName"];
        _managedIdentityClientId = _configuration["ManagedIdentityClientId"];
        
        _isConfigured = !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_deploymentName);
        
        if (!_isConfigured)
        {
            _logger.LogWarning("Azure OpenAI is not configured. Chat will return dummy responses.");
        }
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        if (!_isConfigured)
        {
            return new ChatResponse
            {
                Success = true,
                Message = "**GenAI Services Not Deployed**\n\nThe Azure OpenAI services have not been configured for this application.\n\nTo enable AI-powered chat functionality:\n1. Run the `deploy-with-chat.sh` script to deploy GenAI resources\n2. This will create Azure OpenAI and AI Search services\n3. The chat interface will then be fully functional\n\nIn the meantime, you can still use the standard expense management features!"
            };
        }

        try
        {
            // Create credential based on configuration
            Azure.Core.TokenCredential credential;
            if (!string.IsNullOrEmpty(_managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", _managedIdentityClientId);
                credential = new ManagedIdentityCredential(_managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new AzureOpenAIClient(new Uri(_endpoint!), credential);
            var chatClient = client.GetChatClient(_deploymentName);

            // Build messages list with function definitions - use OpenAI.Chat.ChatMessage
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            // Add history if present
            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    if (msg.Role == "user")
                        messages.Add(new UserChatMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            messages.Add(new UserChatMessage(request.Message));

            // Define available functions
            var tools = GetFunctionTools();

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            // Function calling loop
            var response = await chatClient.CompleteChatAsync(messages, options);
            
            while (response.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Process tool calls
                var toolCalls = response.Value.ToolCalls;
                messages.Add(new AssistantChatMessage(toolCalls));

                foreach (var toolCall in toolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                    messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                response = await chatClient.CompleteChatAsync(messages, options);
            }

            return new ChatResponse
            {
                Success = true,
                Message = response.Value.Content[0].Text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return new ChatResponse
            {
                Success = false,
                Error = $"Error communicating with AI service: {ex.Message}",
                Message = "Sorry, I encountered an error processing your request. Please try again."
            };
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management System. You help users manage their expenses, view reports, and answer questions about the system.

You have access to the following functions to interact with the expense database:
- get_all_expenses: Retrieves all expenses, optionally filtered by a search term
- get_pending_expenses: Retrieves expenses pending approval
- get_dashboard_stats: Gets summary statistics (total expenses, pending approvals, approved amount)
- get_categories: Gets available expense categories
- create_expense: Creates a new expense entry
- approve_expense: Approves a submitted expense (manager action)
- reject_expense: Rejects a submitted expense (manager action)

When users ask about expenses, always use the appropriate function to get real data.
Format lists and data in a readable way using markdown:
- Use **bold** for important information
- Use numbered lists (1., 2., etc.) for ordered items
- Use bullet points (- or *) for unordered lists
- Use line breaks for readability

Be helpful, professional, and concise in your responses. All amounts are in GBP (£).";
    }

    private List<ChatTool> GetFunctionTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_all_expenses",
                "Retrieves all expenses from the database, optionally filtered by a search term",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filter"": {
                            ""type"": ""string"",
                            ""description"": ""Optional search term to filter expenses by category, description, or user name""
                        }
                    }
                }")),
            ChatTool.CreateFunctionTool(
                "get_pending_expenses",
                "Retrieves all expenses that are pending approval",
                BinaryData.FromString(@"{""type"": ""object"", ""properties"": {}}")),
            ChatTool.CreateFunctionTool(
                "get_dashboard_stats",
                "Gets dashboard statistics including total expenses, pending approvals, and approved amounts",
                BinaryData.FromString(@"{""type"": ""object"", ""properties"": {}}")),
            ChatTool.CreateFunctionTool(
                "get_categories",
                "Gets the list of available expense categories",
                BinaryData.FromString(@"{""type"": ""object"", ""properties"": {}}")),
            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense entry in the system",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""amount"": {
                            ""type"": ""number"",
                            ""description"": ""The expense amount in GBP""
                        },
                        ""categoryId"": {
                            ""type"": ""integer"",
                            ""description"": ""The category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)""
                        },
                        ""expenseDate"": {
                            ""type"": ""string"",
                            ""description"": ""The date of the expense in ISO format (YYYY-MM-DD)""
                        },
                        ""description"": {
                            ""type"": ""string"",
                            ""description"": ""Description of the expense""
                        }
                    },
                    ""required"": [""amount"", ""categoryId"", ""expenseDate""]
                }")),
            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves a submitted expense (manager action)",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""expenseId"": {
                            ""type"": ""integer"",
                            ""description"": ""The ID of the expense to approve""
                        }
                    },
                    ""required"": [""expenseId""]
                }")),
            ChatTool.CreateFunctionTool(
                "reject_expense",
                "Rejects a submitted expense (manager action)",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""expenseId"": {
                            ""type"": ""integer"",
                            ""description"": ""The ID of the expense to reject""
                        }
                    },
                    ""required"": [""expenseId""]
                }"))
        };
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments) ?? new Dictionary<string, JsonElement>();

            switch (functionName)
            {
                case "get_all_expenses":
                    var filter = args.TryGetValue("filter", out var filterVal) ? filterVal.GetString() : null;
                    var expenses = await _expenseService.GetAllExpensesAsync(filter);
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = e.FormattedAmount,
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.StatusName,
                        e.Description
                    }));

                case "get_pending_expenses":
                    var pending = await _expenseService.GetPendingExpensesAsync();
                    return JsonSerializer.Serialize(pending.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = e.FormattedAmount,
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.Description
                    }));

                case "get_dashboard_stats":
                    var stats = await _expenseService.GetDashboardStatsAsync();
                    return JsonSerializer.Serialize(new
                    {
                        stats.TotalExpenses,
                        stats.PendingApprovals,
                        ApprovedAmount = $"£{stats.ApprovedAmount:N2}",
                        stats.ApprovedCount
                    });

                case "get_categories":
                    var categories = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories.Select(c => new { c.CategoryId, c.CategoryName }));

                case "create_expense":
                    var dto = new ExpenseCreateDto
                    {
                        Amount = args["amount"].GetDecimal(),
                        CategoryId = args["categoryId"].GetInt32(),
                        ExpenseDate = DateTime.Parse(args["expenseDate"].GetString() ?? DateTime.Today.ToString("yyyy-MM-dd")),
                        Description = args.TryGetValue("description", out var desc) ? desc.GetString() : null
                    };
                    var created = await _expenseService.CreateExpenseAsync(dto);
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        expenseId = created.ExpenseId,
                        message = $"Expense created successfully with ID {created.ExpenseId}"
                    });

                case "approve_expense":
                    var approveId = args["expenseId"].GetInt32();
                    var approved = await _expenseService.ApproveExpenseAsync(approveId, 2); // Manager ID 2
                    return JsonSerializer.Serialize(new
                    {
                        success = approved,
                        message = approved ? $"Expense {approveId} approved successfully" : $"Failed to approve expense {approveId}"
                    });

                case "reject_expense":
                    var rejectId = args["expenseId"].GetInt32();
                    var rejected = await _expenseService.RejectExpenseAsync(rejectId, 2); // Manager ID 2
                    return JsonSerializer.Serialize(new
                    {
                        success = rejected,
                        message = rejected ? $"Expense {rejectId} rejected" : $"Failed to reject expense {rejectId}"
                    });

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
