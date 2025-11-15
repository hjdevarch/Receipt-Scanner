namespace ReceiptScanner.Application.DTOs;

/// <summary>
/// Summary of receipt totals for different time periods
/// </summary>
public class ReceiptSummaryDto
{
    /// <summary>
    /// Total amount of all receipts for the user
    /// </summary>
    public decimal Total { get; set; }
    
    /// <summary>
    /// Total amount of receipts for the current year
    /// </summary>
    public decimal ThisYear { get; set; }
    
    /// <summary>
    /// Total amount of receipts for the current month
    /// </summary>
    public decimal ThisMonth { get; set; }
    
    /// <summary>
    /// Total amount of receipts for the current week (Sunday to Saturday)
    /// </summary>
    public decimal ThisWeek { get; set; }
}
