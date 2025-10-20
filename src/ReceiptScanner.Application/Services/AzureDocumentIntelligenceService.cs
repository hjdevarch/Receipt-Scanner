using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReceiptScanner.Application.Interfaces;

namespace ReceiptScanner.Application.Services;

public class AzureDocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<AzureDocumentIntelligenceService> _logger;

    public AzureDocumentIntelligenceService(IConfiguration configuration, ILogger<AzureDocumentIntelligenceService> logger)
    {
        _logger = logger;
        
        var endpoint = configuration["AzureDocumentIntelligence:Endpoint"] ?? 
                      throw new ArgumentNullException("AzureDocumentIntelligence:Endpoint configuration is missing");
        
        var apiKey = configuration["AzureDocumentIntelligence:ApiKey"] ?? 
                    throw new ArgumentNullException("AzureDocumentIntelligence:ApiKey configuration is missing");

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<DocumentAnalysisResult> AnalyzeReceiptAsync(Stream imageStream)
    {
        try
        {
            _logger.LogInformation("Starting receipt analysis with Azure Document Intelligence");

            // Use the prebuilt receipt model
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-receipt", imageStream);
            var result = operation.Value;

            var analysisResult = new DocumentAnalysisResult
            {
                IsSuccess = true,
                Items = new List<DocumentReceiptItem>()
            };

            if (result.Documents.Count > 0)
            {
                var receipt = result.Documents[0];

                // Log all receipt-level fields for debugging
                _logger.LogInformation("Available receipt fields: {Fields}", string.Join(", ", receipt.Fields.Keys));

                // Extract merchant information
                if (receipt.Fields.TryGetValue("MerchantName", out var merchantName) && merchantName.FieldType == DocumentFieldType.String)
                {
                    analysisResult.MerchantName = merchantName.Value.AsString();
                }

                if (receipt.Fields.TryGetValue("MerchantAddress", out var merchantAddress) && merchantAddress.FieldType == DocumentFieldType.String)
                {
                    analysisResult.MerchantAddress = merchantAddress.Value.AsString();
                }

                if (receipt.Fields.TryGetValue("MerchantPhoneNumber", out var merchantPhone) && merchantPhone.FieldType == DocumentFieldType.String)
                {
                    analysisResult.MerchantPhone = merchantPhone.Value.AsString();
                }

                // Extract transaction details
                if (receipt.Fields.TryGetValue("TransactionDate", out var transactionDate) && transactionDate.FieldType == DocumentFieldType.Date)
                {
                    analysisResult.TransactionDate = transactionDate.Value.AsDate().DateTime;
                }

                // Try multiple field names for receipt number
                if (receipt.Fields.TryGetValue("TransactionId", out var transactionId) && transactionId.FieldType == DocumentFieldType.String)
                {
                    analysisResult.ReceiptNumber = transactionId.Value.AsString();
                    _logger.LogInformation("Found ReceiptNumber from TransactionId: {ReceiptNumber}", analysisResult.ReceiptNumber);
                }
                else if (receipt.Fields.TryGetValue("ReceiptId", out var receiptId) && receiptId.FieldType == DocumentFieldType.String)
                {
                    analysisResult.ReceiptNumber = receiptId.Value.AsString();
                    _logger.LogInformation("Found ReceiptNumber from ReceiptId: {ReceiptNumber}", analysisResult.ReceiptNumber);
                }
                else
                {
                    // Generate a receipt number from available data (e.g., store number + transaction time)
                    var storeInfo = "";
                    if (receipt.Fields.TryGetValue("MerchantName", out var merchantNameField))
                    {
                        storeInfo = merchantNameField.Value.AsString()?.Replace(" ", "").Substring(0, Math.Min(4, merchantNameField.Value.AsString().Replace(" ", "").Length)) ?? "";
                    }
                    analysisResult.ReceiptNumber = $"{storeInfo}{DateTime.Now:MMddHHmm}";
                    _logger.LogInformation("Generated ReceiptNumber: {ReceiptNumber}", analysisResult.ReceiptNumber);
                }

                // Extract totals with enhanced logging and fallback calculation
                if (receipt.Fields.TryGetValue("Subtotal", out var subtotal) && subtotal.FieldType == DocumentFieldType.Currency)
                {
                    analysisResult.SubTotal = (decimal?)subtotal.Value.AsCurrency().Amount;
                    _logger.LogInformation("Found Subtotal: {SubTotal}", analysisResult.SubTotal);
                }
                else if (receipt.Fields.TryGetValue("SubTotal", out var subTotal) && subTotal.FieldType == DocumentFieldType.Currency)
                {
                    analysisResult.SubTotal = (decimal?)subTotal.Value.AsCurrency().Amount;
                    _logger.LogInformation("Found SubTotal: {SubTotal}", analysisResult.SubTotal);
                }
                else
                {
                    _logger.LogWarning("Subtotal field not found - will calculate from Total - Tax");
                }

                if (receipt.Fields.TryGetValue("TotalTax", out var tax) && tax.FieldType == DocumentFieldType.Currency)
                {
                    analysisResult.Tax = (decimal?)tax.Value.AsCurrency().Amount;
                    _logger.LogInformation("Found TotalTax: {Tax}", analysisResult.Tax);
                }
                else
                {
                    _logger.LogWarning("TotalTax field not found or not currency type");
                }

                if (receipt.Fields.TryGetValue("Total", out var total) && total.FieldType == DocumentFieldType.Currency)
                {
                    analysisResult.Total = (decimal?)total.Value.AsCurrency().Amount;
                    _logger.LogInformation("Found Total: {Total}", analysisResult.Total);
                }
                else
                {
                    _logger.LogWarning("Total field not found or not currency type");
                }

                // Extract SubTotal from TaxDetails if not found directly
                if (analysisResult.SubTotal == null && receipt.Fields.TryGetValue("TaxDetails", out var taxDetails) && taxDetails.FieldType == DocumentFieldType.List)
                {
                    var taxDetailsList = taxDetails.Value.AsList();
                    if (taxDetailsList.Count > 0 && taxDetailsList[0].FieldType == DocumentFieldType.Dictionary)
                    {
                        var taxDetailsDict = taxDetailsList[0].Value.AsDictionary();
                        if (taxDetailsDict.TryGetValue("NetAmount", out var netAmount) && netAmount.FieldType == DocumentFieldType.Currency)
                        {
                            analysisResult.SubTotal = (decimal?)netAmount.Value.AsCurrency().Amount;
                            _logger.LogInformation("Found SubTotal from TaxDetails.NetAmount: {SubTotal}", analysisResult.SubTotal);
                        }
                    }
                }

                // Calculate SubTotal if still not found
                if (analysisResult.SubTotal == null && analysisResult.Total.HasValue && analysisResult.Tax.HasValue)
                {
                    analysisResult.SubTotal = analysisResult.Total.Value - analysisResult.Tax.Value;
                    _logger.LogInformation("Calculated SubTotal: {SubTotal} (Total {Total} - Tax {Tax})", 
                        analysisResult.SubTotal, analysisResult.Total, analysisResult.Tax);
                }

                // Extract currency from Total field or CountryRegion
                if (receipt.Fields.TryGetValue("Total", out var totalField) && totalField.FieldType == DocumentFieldType.Currency)
                {
                    var currencyInfo = totalField.Value.AsCurrency();
                    // Try to extract currency symbol, defaulting to GBP for UK receipts
                    var currencySymbol = totalField.Content?.Substring(0, 1);
                    analysisResult.Currency = currencySymbol == "£" ? "GBP" : "USD";
                    _logger.LogInformation("Extracted currency from Total field symbol '{Symbol}': {Currency}", currencySymbol, analysisResult.Currency);
                }
                else if (receipt.Fields.TryGetValue("CountryRegion", out var countryRegion) && countryRegion.FieldType == DocumentFieldType.CountryRegion)
                {
                    var country = countryRegion.Value.AsCountryRegion();
                    analysisResult.Currency = country == "GBR" ? "GBP" : "USD";
                    _logger.LogInformation("Extracted currency from CountryRegion: {Country} -> {Currency}", country, analysisResult.Currency);
                }
                else
                {
                    analysisResult.Currency = "GBP"; // Default for UK receipts
                    _logger.LogInformation("Using default currency: {Currency}", analysisResult.Currency);
                }

                // Extract items
                if (receipt.Fields.TryGetValue("Items", out var items) && items.FieldType == DocumentFieldType.List)
                {
                    _logger.LogInformation("Found {ItemCount} items in receipt", items.Value.AsList().Count);
                    
                    foreach (var item in items.Value.AsList())
                    {
                        if (item.FieldType == DocumentFieldType.Dictionary)
                        {
                            var itemDict = item.Value.AsDictionary();
                            var receiptItem = new DocumentReceiptItem();

                            // Log all available fields for debugging
                            _logger.LogInformation("Available item fields: {Fields}", string.Join(", ", itemDict.Keys));

                            // Try different field names for description/name
                            if (itemDict.TryGetValue("Description", out var description) && description.FieldType == DocumentFieldType.String)
                            {
                                receiptItem.Name = description.Value.AsString();
                                _logger.LogInformation("Found Description: {Description}", receiptItem.Name);
                            }
                            else if (itemDict.TryGetValue("Name", out var name) && name.FieldType == DocumentFieldType.String)
                            {
                                receiptItem.Name = name.Value.AsString();
                                _logger.LogInformation("Found Name: {Name}", receiptItem.Name);
                            }

                            // Try different field names for quantity
                            if (itemDict.TryGetValue("Quantity", out var quantity))
                            {
                                if (quantity.FieldType == DocumentFieldType.Double)
                                {
                                    receiptItem.Quantity = (int?)quantity.Value.AsDouble();
                                    _logger.LogInformation("Found Quantity (Double): {Quantity}", receiptItem.Quantity);
                                }
                                else if (quantity.FieldType == DocumentFieldType.Int64)
                                {
                                    receiptItem.Quantity = (int?)quantity.Value.AsInt64();
                                    _logger.LogInformation("Found Quantity (Int64): {Quantity}", receiptItem.Quantity);
                                }
                            }

                            // Try different field names for prices
                            if (itemDict.TryGetValue("Price", out var price))
                            {
                                if (price.FieldType == DocumentFieldType.Currency)
                                {
                                    receiptItem.UnitPrice = (decimal?)price.Value.AsCurrency().Amount;
                                    _logger.LogInformation("Found Price (Currency): {Price}", receiptItem.UnitPrice);
                                }
                                else if (price.FieldType == DocumentFieldType.Double)
                                {
                                    receiptItem.UnitPrice = (decimal?)price.Value.AsDouble();
                                    _logger.LogInformation("Found Price (Double): {Price}", receiptItem.UnitPrice);
                                }
                            }
                            else if (itemDict.TryGetValue("UnitPrice", out var unitPrice))
                            {
                                if (unitPrice.FieldType == DocumentFieldType.Currency)
                                {
                                    receiptItem.UnitPrice = (decimal?)unitPrice.Value.AsCurrency().Amount;
                                    _logger.LogInformation("Found UnitPrice (Currency): {UnitPrice}", receiptItem.UnitPrice);
                                }
                                else if (unitPrice.FieldType == DocumentFieldType.Double)
                                {
                                    receiptItem.UnitPrice = (decimal?)unitPrice.Value.AsDouble();
                                    _logger.LogInformation("Found UnitPrice (Double): {UnitPrice}", receiptItem.UnitPrice);
                                }
                            }

                            if (itemDict.TryGetValue("TotalPrice", out var totalPrice))
                            {
                                if (totalPrice.FieldType == DocumentFieldType.Currency)
                                {
                                    receiptItem.TotalPrice = (decimal?)totalPrice.Value.AsCurrency().Amount;
                                    _logger.LogInformation("Found TotalPrice (Currency): {TotalPrice}", receiptItem.TotalPrice);
                                }
                                else if (totalPrice.FieldType == DocumentFieldType.Double)
                                {
                                    receiptItem.TotalPrice = (decimal?)totalPrice.Value.AsDouble();
                                    _logger.LogInformation("Found TotalPrice (Double): {TotalPrice}", receiptItem.TotalPrice);
                                }
                            }
                            else if (itemDict.TryGetValue("Total", out var itemTotal))
                            {
                                if (itemTotal.FieldType == DocumentFieldType.Currency)
                                {
                                    receiptItem.TotalPrice = (decimal?)itemTotal.Value.AsCurrency().Amount;
                                    _logger.LogInformation("Found Total (Currency): {Total}", receiptItem.TotalPrice);
                                }
                                else if (itemTotal.FieldType == DocumentFieldType.Double)
                                {
                                    receiptItem.TotalPrice = (decimal?)itemTotal.Value.AsDouble();
                                    _logger.LogInformation("Found Total (Double): {Total}", receiptItem.TotalPrice);
                                }
                            }

                            if (!string.IsNullOrEmpty(receiptItem.Name))
                            {
                                _logger.LogInformation("Adding receipt item: {Name}, Qty: {Quantity}, Unit: {UnitPrice}, Total: {TotalPrice}", 
                                    receiptItem.Name, receiptItem.Quantity, receiptItem.UnitPrice, receiptItem.TotalPrice);
                                analysisResult.Items.Add(receiptItem);
                            }
                        }
                    }
                }
            }

            // Extract raw text
            analysisResult.RawText = result.Content;

            _logger.LogInformation("Receipt analysis completed successfully. Found {ItemCount} items", analysisResult.Items.Count);
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing receipt with Azure Document Intelligence");
            return new DocumentAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}