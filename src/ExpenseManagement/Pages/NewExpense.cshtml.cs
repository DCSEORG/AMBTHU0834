using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class NewExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<NewExpenseModel> _logger;

    [BindProperty]
    public ExpenseCreateDto Input { get; set; } = new()
    {
        ExpenseDate = DateTime.Today,
        UserId = 1 // Default user for demo
    };

    public List<SelectListItem> Categories { get; set; } = new();

    public NewExpenseModel(IExpenseService expenseService, ILogger<NewExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        await LoadCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return Page();
        }

        try
        {
            await _expenseService.CreateExpenseAsync(Input);
            return RedirectToPage("/Expenses");
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error creating expense");
            ViewData["ErrorMessage"] = ex.Message;
            ViewData["ErrorLocation"] = "NewExpenseModel.OnPostAsync";
            await LoadCategoriesAsync();
            return Page();
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            Categories = categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CategoryName
            }).ToList();
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error loading categories");
            ViewData["ErrorMessage"] = ex.Message;
            ViewData["ErrorLocation"] = "NewExpenseModel.LoadCategoriesAsync";
        }
    }
}
