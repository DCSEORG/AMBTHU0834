using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ApprovalsModel> _logger;

    public List<Expense> PendingExpenses { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public ApprovalsModel(IExpenseService expenseService, ILogger<ApprovalsModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            var pending = await _expenseService.GetPendingExpensesAsync();
            
            if (!string.IsNullOrEmpty(Filter))
            {
                pending = pending.Where(e =>
                    (e.CategoryName?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.UserName?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }
            
            PendingExpenses = pending;
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error loading pending expenses");
            ViewData["ErrorMessage"] = ex.Message;
            ViewData["ErrorLocation"] = "ApprovalsModel.OnGetAsync";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        try
        {
            await _expenseService.ApproveExpenseAsync(id, 2); // Manager ID 2
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error approving expense");
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        try
        {
            await _expenseService.RejectExpenseAsync(id, 2); // Manager ID 2
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error rejecting expense");
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage();
    }
}
