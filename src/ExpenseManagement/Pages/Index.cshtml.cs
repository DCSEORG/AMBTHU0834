using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IExpenseService _expenseService;

    public DashboardStats Stats { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();

    public IndexModel(ILogger<IndexModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Stats = await _expenseService.GetDashboardStatsAsync();
            var allExpenses = await _expenseService.GetAllExpensesAsync();
            RecentExpenses = allExpenses.OrderByDescending(e => e.ExpenseDate).Take(10).ToList();
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error loading dashboard");
            ViewData["ErrorMessage"] = ex.Message;
            ViewData["ErrorLocation"] = "IndexModel.OnGetAsync";
            // Stats and RecentExpenses will use defaults
        }
    }
}
