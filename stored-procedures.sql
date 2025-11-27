/*
  stored-procedures.sql
  Stored procedures for Expense Management System
  All application data access goes through these procedures
*/

SET NOCOUNT ON;
GO

-- Get all expenses with optional filter
CREATE OR ALTER PROCEDURE sp_GetAllExpenses
    @Filter NVARCHAR(255) = NULL
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        r.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
    WHERE @Filter IS NULL
        OR u.UserName LIKE '%' + @Filter + '%'
        OR c.CategoryName LIKE '%' + @Filter + '%'
        OR e.Description LIKE '%' + @Filter + '%'
    ORDER BY e.ExpenseDate DESC;
END;
GO

-- Get pending expenses (Submitted status)
CREATE OR ALTER PROCEDURE sp_GetPendingExpenses
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        NULL AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE s.StatusName = 'Submitted'
    ORDER BY e.SubmittedAt ASC;
END;
GO

-- Get expense by ID
CREATE OR ALTER PROCEDURE sp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        r.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
    WHERE e.ExpenseId = @ExpenseId;
END;
GO

-- Get all categories
CREATE OR ALTER PROCEDURE sp_GetCategories
AS
BEGIN
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END;
GO

-- Get all statuses
CREATE OR ALTER PROCEDURE sp_GetStatuses
AS
BEGIN
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END;
GO

-- Get all users
CREATE OR ALTER PROCEDURE sp_GetUsers
AS
BEGIN
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.IsActive
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END;
GO

-- Get dashboard statistics
CREATE OR ALTER PROCEDURE sp_GetDashboardStats
AS
BEGIN
    SELECT 
        (SELECT COUNT(*) FROM dbo.Expenses) AS TotalExpenses,
        (SELECT COUNT(*) FROM dbo.Expenses e 
         INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId 
         WHERE s.StatusName = 'Submitted') AS PendingApprovals,
        ISNULL((SELECT SUM(CAST(e.AmountMinor AS DECIMAL(18,2)) / 100) FROM dbo.Expenses e 
         INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId 
         WHERE s.StatusName = 'Approved'), 0) AS ApprovedAmount,
        (SELECT COUNT(*) FROM dbo.Expenses e 
         INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId 
         WHERE s.StatusName = 'Approved') AS ApprovedCount;
END;
GO

-- Create new expense
CREATE OR ALTER PROCEDURE sp_CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL
AS
BEGIN
    DECLARE @StatusId INT;
    SELECT @StatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';
    
    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, CreatedAt)
    VALUES (@UserId, @CategoryId, @StatusId, @AmountMinor, 'GBP', @ExpenseDate, @Description, SYSUTCDATETIME());
    
    DECLARE @ExpenseId INT = SCOPE_IDENTITY();
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        NULL AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE e.ExpenseId = @ExpenseId;
END;
GO

-- Update expense
CREATE OR ALTER PROCEDURE sp_UpdateExpense
    @ExpenseId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL
AS
BEGIN
    UPDATE dbo.Expenses
    SET CategoryId = @CategoryId,
        AmountMinor = @AmountMinor,
        ExpenseDate = @ExpenseDate,
        Description = @Description
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
END;
GO

-- Submit expense
CREATE OR ALTER PROCEDURE sp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    UPDATE dbo.Expenses
    SET StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted'),
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
END;
GO

-- Approve expense
CREATE OR ALTER PROCEDURE sp_ApproveExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    UPDATE dbo.Expenses
    SET StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved'),
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
END;
GO

-- Reject expense
CREATE OR ALTER PROCEDURE sp_RejectExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    UPDATE dbo.Expenses
    SET StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected'),
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
END;
GO

-- Delete expense (only drafts can be deleted)
CREATE OR ALTER PROCEDURE sp_DeleteExpense
    @ExpenseId INT
AS
BEGIN
    DELETE FROM dbo.Expenses
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
END;
GO

PRINT 'All stored procedures created successfully!';
GO
