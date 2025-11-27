using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetAllExpensesAsync(string? filter = null);
    Task<List<Expense>> GetPendingExpensesAsync();
    Task<Expense?> GetExpenseByIdAsync(int id);
    Task<List<ExpenseCategory>> GetCategoriesAsync();
    Task<List<ExpenseStatus>> GetStatusesAsync();
    Task<List<User>> GetUsersAsync();
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<Expense> CreateExpenseAsync(ExpenseCreateDto dto);
    Task<bool> UpdateExpenseAsync(ExpenseUpdateDto dto);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<bool> DeleteExpenseAsync(int expenseId);
}

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;
    private readonly bool _useDummyData;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
        _useDummyData = string.IsNullOrEmpty(_connectionString);
    }

    private async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<List<Expense>> GetAllExpensesAsync(string? filter = null)
    {
        if (_useDummyData)
        {
            return GetDummyExpenses(filter);
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_GetAllExpenses @Filter", connection);
            command.Parameters.AddWithValue("@Filter", filter ?? (object)DBNull.Value);
            
            var expenses = new List<Expense>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses from database at {Method}", nameof(GetAllExpensesAsync));
            throw new DatabaseException($"Database connection error in {nameof(GetAllExpensesAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<List<Expense>> GetPendingExpensesAsync()
    {
        if (_useDummyData)
        {
            return GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_GetPendingExpenses", connection);
            
            var expenses = new List<Expense>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending expenses from database at {Method}", nameof(GetPendingExpensesAsync));
            throw new DatabaseException($"Database connection error in {nameof(GetPendingExpensesAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int id)
    {
        if (_useDummyData)
        {
            return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == id);
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_GetExpenseById @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", id);
            
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense by ID from database at {Method}", nameof(GetExpenseByIdAsync));
            throw new DatabaseException($"Database connection error in {nameof(GetExpenseByIdAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        if (_useDummyData)
        {
            return GetDummyCategories();
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_GetCategories", connection);
            
            var categories = new List<ExpenseCategory>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories from database at {Method}", nameof(GetCategoriesAsync));
            throw new DatabaseException($"Database connection error in {nameof(GetCategoriesAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        if (_useDummyData)
        {
            return GetDummyStatuses();
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_GetStatuses", connection);
            
            var statuses = new List<ExpenseStatus>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }
            return statuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses from database at {Method}", nameof(GetStatusesAsync));
            throw new DatabaseException($"Database connection error in {nameof(GetStatusesAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<List<User>> GetUsersAsync()
    {
        if (_useDummyData)
        {
            return GetDummyUsers();
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_GetUsers", connection);
            
            var users = new List<User>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString(reader.GetOrdinal("RoleName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users from database at {Method}", nameof(GetUsersAsync));
            throw new DatabaseException($"Database connection error in {nameof(GetUsersAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        if (_useDummyData)
        {
            return GetDummyStats();
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_GetDashboardStats", connection);
            
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new DashboardStats
                {
                    TotalExpenses = reader.GetInt32(reader.GetOrdinal("TotalExpenses")),
                    PendingApprovals = reader.GetInt32(reader.GetOrdinal("PendingApprovals")),
                    ApprovedAmount = reader.GetDecimal(reader.GetOrdinal("ApprovedAmount")),
                    ApprovedCount = reader.GetInt32(reader.GetOrdinal("ApprovedCount"))
                };
            }
            return new DashboardStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats from database at {Method}", nameof(GetDashboardStatsAsync));
            throw new DatabaseException($"Database connection error in {nameof(GetDashboardStatsAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<Expense> CreateExpenseAsync(ExpenseCreateDto dto)
    {
        if (_useDummyData)
        {
            var expense = new Expense
            {
                ExpenseId = new Random().Next(100, 9999),
                UserId = dto.UserId,
                CategoryId = dto.CategoryId,
                AmountMinor = (int)(dto.Amount * 100),
                ExpenseDate = dto.ExpenseDate,
                Description = dto.Description,
                StatusId = 1,
                StatusName = "Draft",
                CategoryName = GetDummyCategories().FirstOrDefault(c => c.CategoryId == dto.CategoryId)?.CategoryName ?? "Travel",
                UserName = "Alice Example",
                CreatedAt = DateTime.UtcNow
            };
            return expense;
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_CreateExpense @UserId, @CategoryId, @AmountMinor, @ExpenseDate, @Description", connection);
            command.Parameters.AddWithValue("@UserId", dto.UserId);
            command.Parameters.AddWithValue("@CategoryId", dto.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(dto.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate);
            command.Parameters.AddWithValue("@Description", dto.Description ?? (object)DBNull.Value);
            
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
            throw new Exception("Failed to create expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense in database at {Method}", nameof(CreateExpenseAsync));
            throw new DatabaseException($"Database connection error in {nameof(CreateExpenseAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateExpenseAsync(ExpenseUpdateDto dto)
    {
        if (_useDummyData)
        {
            return true;
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_UpdateExpense @ExpenseId, @CategoryId, @AmountMinor, @ExpenseDate, @Description", connection);
            command.Parameters.AddWithValue("@ExpenseId", dto.ExpenseId);
            command.Parameters.AddWithValue("@CategoryId", dto.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(dto.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate);
            command.Parameters.AddWithValue("@Description", dto.Description ?? (object)DBNull.Value);
            
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense in database at {Method}", nameof(UpdateExpenseAsync));
            throw new DatabaseException($"Database connection error in {nameof(UpdateExpenseAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        if (_useDummyData)
        {
            return true;
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_SubmitExpense @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense in database at {Method}", nameof(SubmitExpenseAsync));
            throw new DatabaseException($"Database connection error in {nameof(SubmitExpenseAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        if (_useDummyData)
        {
            return true;
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_ApproveExpense @ExpenseId, @ReviewerId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);
            
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense in database at {Method}", nameof(ApproveExpenseAsync));
            throw new DatabaseException($"Database connection error in {nameof(ApproveExpenseAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        if (_useDummyData)
        {
            return true;
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_RejectExpense @ExpenseId, @ReviewerId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);
            
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense in database at {Method}", nameof(RejectExpenseAsync));
            throw new DatabaseException($"Database connection error in {nameof(RejectExpenseAsync)}: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        if (_useDummyData)
        {
            return true;
        }

        try
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC sp_DeleteExpense @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense in database at {Method}", nameof(DeleteExpenseAsync));
            throw new DatabaseException($"Database connection error in {nameof(DeleteExpenseAsync)}: {ex.Message}", ex);
        }
    }

    private Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusName = reader.IsDBNull(reader.GetOrdinal("StatusName")) ? null : reader.GetString(reader.GetOrdinal("StatusName")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName"))
        };
    }

    // Dummy data methods for when database is not available
    private List<Expense> GetDummyExpenses(string? filter = null)
    {
        var expenses = new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, ExpenseDate = new DateTime(2025, 10, 20), Description = "Taxi from airport to client site", CreatedAt = DateTime.UtcNow },
            new() { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, ExpenseDate = new DateTime(2025, 9, 15), Description = "Client lunch meeting", CreatedAt = DateTime.UtcNow },
            new() { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, ExpenseDate = new DateTime(2025, 11, 1), Description = "Office stationery", CreatedAt = DateTime.UtcNow },
            new() { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, ExpenseDate = new DateTime(2025, 8, 10), Description = "Hotel during client visit", CreatedAt = DateTime.UtcNow },
            new() { ExpenseId = 5, UserId = 2, UserName = "Bob Manager", CategoryId = 1, CategoryName = "Travel", StatusId = 1, StatusName = "Draft", AmountMinor = 23400, ExpenseDate = new DateTime(2025, 11, 11), Description = "Meeting", CreatedAt = DateTime.UtcNow }
        };

        if (!string.IsNullOrEmpty(filter))
        {
            expenses = expenses.Where(e => 
                (e.CategoryName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.UserName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        return expenses;
    }

    private List<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }

    private List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true },
            new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true }
        };
    }

    private DashboardStats GetDummyStats()
    {
        return new DashboardStats
        {
            TotalExpenses = 10,
            PendingApprovals = 1,
            ApprovedAmount = 519.24m,
            ApprovedCount = 6
        };
    }
}

public class DatabaseException : Exception
{
    public DatabaseException(string message) : base(message) { }
    public DatabaseException(string message, Exception innerException) : base(message, innerException) { }
}
