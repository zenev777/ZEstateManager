// ReportResponseDto.cs
namespace ZEstate.Core.DTOs.Reports;

public class IncomeByApartmentDto
{
    public string ApartmentNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class IncomeByFeeTypeDto
{
    // Underlying int value of ZEstate.Infrastructure.Data.Enums.FeeType.
    public int FeeType { get; set; }
    public decimal Total { get; set; }
}

public class ExpenseByRepairDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class FinancialSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Balance { get; set; }
    public List<IncomeByApartmentDto> IncomeByApartment { get; set; } = new();
    public List<IncomeByFeeTypeDto> IncomeByFeeType { get; set; } = new();
    public List<ExpenseByRepairDto> ExpensesByRepair { get; set; } = new();
}

public class BalanceHistoryEntryDto
{
    public string Period { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Balance { get; set; }
}
