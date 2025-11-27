using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesModel> _logger;

    public List<Expense> Expenses { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public ExpensesModel(IExpenseService expenseService, ILogger<ExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Expenses = await _expenseService.GetAllExpensesAsync(Filter);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error loading expenses");
            ViewData["ErrorMessage"] = ex.Message;
            ViewData["ErrorLocation"] = "ExpensesModel.OnGetAsync";
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(id);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error submitting expense");
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error deleting expense");
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage();
    }
}
