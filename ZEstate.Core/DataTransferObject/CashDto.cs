// CashDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Cash;

public class CashBalancesDto
{
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
}

public class TransferFundsDto
{
    // "Cash" | "Bank" - the account money moves OUT of
    [Required]
    public string From { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }
}

public class CashLedgerEntryDto
{
    public int Id { get; set; }
    // CashAccountType: Cash = 0, Bank = 1
    public int Account { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
