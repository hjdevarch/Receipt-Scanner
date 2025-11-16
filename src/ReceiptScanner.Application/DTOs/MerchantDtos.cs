namespace  ReceiptScanner.Application.Services;

public class MerchantWithTotalDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string Website { get; set; }
    public string LogoPath { get; set; }
    public decimal TotalAmount { get; set; }
    public int ReceiptCount { get; set; }
}