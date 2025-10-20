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

                // Log available receipt-level fields (not item fields)
                var receiptLevelFields = receipt.Fields.Where(f => !f.Key.Contains("Items")).ToList();
                _logger.LogInformation("Receipt-level fields available: {Fields}", string.Join(", ", receiptLevelFields.Select(f => $"{f.Key}({f.Value.FieldType})")));

                // Try multiple field name variations for totals
                var totalFieldNames = new[] { "Total", "TotalAmount", "GrandTotal", "FinalTotal" };
                var subTotalFieldNames = new[] { "Subtotal", "SubTotal", "SubtotalAmount", "NetAmount", "ItemsTotal" };
                var taxFieldNames = new[] { "TotalTax", "Tax", "TaxAmount", "VAT", "SalesTax" };

                // Extract Total (handle both Currency and Double types)
                foreach (var fieldName in totalFieldNames)
                {
                    if (receipt.Fields.TryGetValue(fieldName, out var totalFieldVar))
                    {
                        if (totalFieldVar.FieldType == DocumentFieldType.Currency)
                        {
                            analysisResult.Total = (decimal?)totalFieldVar.Value.AsCurrency().Amount;
                            _logger.LogInformation("Found Total from '{FieldName}' (Currency): {Total}", fieldName, analysisResult.Total);
                            break;
                        }
                        else if (totalFieldVar.FieldType == DocumentFieldType.Double)
                        {
                            analysisResult.Total = (decimal?)totalFieldVar.Value.AsDouble();
                            _logger.LogInformation("Found Total from '{FieldName}' (Double): {Total}", fieldName, analysisResult.Total);
                            break;
                        }
                    }
                }

                // Extract SubTotal (handle both Currency and Double types)
                foreach (var fieldName in subTotalFieldNames)
                {
                    if (receipt.Fields.TryGetValue(fieldName, out var subTotalFieldVar))
                    {
                        if (subTotalFieldVar.FieldType == DocumentFieldType.Currency)
                        {
                            analysisResult.SubTotal = (decimal?)subTotalFieldVar.Value.AsCurrency().Amount;
                            _logger.LogInformation("Found SubTotal from '{FieldName}' (Currency): {SubTotal}", fieldName, analysisResult.SubTotal);
                            break;
                        }
                        else if (subTotalFieldVar.FieldType == DocumentFieldType.Double)
                        {
                            analysisResult.SubTotal = (decimal?)subTotalFieldVar.Value.AsDouble();
                            _logger.LogInformation("Found SubTotal from '{FieldName}' (Double): {SubTotal}", fieldName, analysisResult.SubTotal);
                            break;
                        }
                    }
                }

                // Extract Tax (handle both Currency and Double types)
                foreach (var fieldName in taxFieldNames)
                {
                    if (receipt.Fields.TryGetValue(fieldName, out var taxFieldVar))
                    {
                        if (taxFieldVar.FieldType == DocumentFieldType.Currency)
                        {
                            analysisResult.Tax = (decimal?)taxFieldVar.Value.AsCurrency().Amount;
                            _logger.LogInformation("Found Tax from '{FieldName}' (Currency): {Tax}", fieldName, analysisResult.Tax);
                            break;
                        }
                        else if (taxFieldVar.FieldType == DocumentFieldType.Double)
                        {
                            analysisResult.Tax = (decimal?)taxFieldVar.Value.AsDouble();
                            _logger.LogInformation("Found Tax from '{FieldName}' (Double): {Tax}", fieldName, analysisResult.Tax);
                            break;
                        }
                    }
                }

                // Extract SubTotal from TaxDetails if not found directly
                if (analysisResult.SubTotal == null && receipt.Fields.TryGetValue("TaxDetails", out var taxDetails) && taxDetails.FieldType == DocumentFieldType.List)
                {
                    _logger.LogInformation("Found TaxDetails array with {Count} items", taxDetails.Value.AsList().Count);
                    var taxDetailsList = taxDetails.Value.AsList();
                    if (taxDetailsList.Count > 0 && taxDetailsList[0].FieldType == DocumentFieldType.Dictionary)
                    {
                        var taxDetailsDict = taxDetailsList[0].Value.AsDictionary();
                        _logger.LogInformation("TaxDetails[0] fields: {Fields}", string.Join(", ", taxDetailsDict.Keys));
                        
                        if (taxDetailsDict.TryGetValue("NetAmount", out var netAmount) && netAmount.FieldType == DocumentFieldType.Currency)
                        {
                            analysisResult.SubTotal = (decimal?)netAmount.Value.AsCurrency().Amount;
                            _logger.LogInformation("Found SubTotal from TaxDetails.NetAmount: {SubTotal}", analysisResult.SubTotal);
                        }
                        else
                        {
                            _logger.LogWarning("NetAmount field not found in TaxDetails or not currency type");
                        }
                    }
                }
                else if (analysisResult.SubTotal == null)
                {
                    _logger.LogWarning("TaxDetails field not found or not list type");
                }

                // Calculate totals from items if not found in receipt fields
                if (analysisResult.Total == null && analysisResult.Items.Any())
                {
                    analysisResult.Total = analysisResult.Items.Sum(item => item.TotalPrice);
                    _logger.LogInformation("Calculated Total from item sum: {Total}", analysisResult.Total);
                }

                // Calculate SubTotal if still not found
                if (analysisResult.SubTotal == null && analysisResult.Total.HasValue && analysisResult.Tax.HasValue)
                {
                    analysisResult.SubTotal = analysisResult.Total.Value - analysisResult.Tax.Value;
                    _logger.LogInformation("Calculated SubTotal: {SubTotal} (Total {Total} - Tax {Tax})", 
                        analysisResult.SubTotal, analysisResult.Total, analysisResult.Tax);
                }
                else if (analysisResult.SubTotal == null && analysisResult.Total.HasValue && !analysisResult.Tax.HasValue)
                {
                    // If no tax found, assume SubTotal = Total (tax-free items or tax included)
                    analysisResult.SubTotal = analysisResult.Total;
                    analysisResult.Tax = 0m;
                    _logger.LogInformation("Set SubTotal = Total (no tax): {SubTotal}", analysisResult.SubTotal);
                }

                // Extract currency from Total field or CountryRegion
                if (receipt.Fields.TryGetValue("Total", out var totalField) && totalField.FieldType == DocumentFieldType.Currency)
                {
                    var currencyInfo = totalField.Value.AsCurrency();
                    // Try to extract currency symbol
                    var currencySymbol = totalField.Content?.Substring(0, 1);
                    
                    // Log the currency info for debugging
                    _logger.LogInformation("Currency info - Symbol: '{Symbol}', Content: '{Content}'", currencySymbol, totalField.Content);
                    
                    // Infer currency from symbol
                    analysisResult.Currency = currencySymbol switch
                    {
                        "£" => "GBP",
                        "€" => "EUR", 
                        "$" => "USD",
                        "₣" => "CHF", // Swiss Franc
                        "₤" => "ITL", // Italian Lira (historical)
                        "L" => "ITL",  // Alternative Italian Lira symbol
                        "₺" => "TRY", // Turkish Lira
                        "﷼" => "IRR", // Iranian Rial
                        _ => "USD" // Default fallback
                    };
                    _logger.LogInformation("Extracted currency from symbol '{Symbol}': {Currency}", currencySymbol, analysisResult.Currency);
                }
                else
                {
                    // Try to detect currency from item TotalPrice fields if Total field doesn't have currency info
                    string? detectedFromItems = null;
                    if (receipt.Fields.TryGetValue("Items", out var currencyItems) && currencyItems.FieldType == DocumentFieldType.List)
                    {
                        foreach (var item in currencyItems.Value.AsList())
                        {
                            if (item.FieldType == DocumentFieldType.Dictionary)
                            {
                                var itemDict = item.Value.AsDictionary();
                                if (itemDict.TryGetValue("TotalPrice", out var totalPriceField))
                                {
                                    var priceContent = totalPriceField.Content;
                                    if (!string.IsNullOrEmpty(priceContent))
                                    {
                                        var symbol = priceContent.Substring(0, 1);
                                        var currencyFromPrice = symbol switch
                                        {
                                            "£" => "GBP",
                                            "€" => "EUR",
                                            "$" => "USD",
                                            "₣" => "CHF",
                                            "₤" => "ITL",
                                            "L" => "ITL",
                                            _ => null
                                        };
                                        
                                        if (currencyFromPrice != null)
                                        {
                                            detectedFromItems = currencyFromPrice;
                                            _logger.LogInformation("Extracted currency from item TotalPrice field symbol '{Symbol}': {Currency}", symbol, detectedFromItems);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (detectedFromItems != null)
                    {
                        analysisResult.Currency = detectedFromItems;
                    }
                    else if (receipt.Fields.TryGetValue("CountryRegion", out var countryRegion) && countryRegion.FieldType == DocumentFieldType.CountryRegion)
                    {
                        var country = countryRegion.Value.AsCountryRegion();
                        analysisResult.Currency = country switch
                        {
                            "GBR" => "GBP",
                            "EUR" => "EUR", // For Eurozone countries
                            "DEU" => "EUR", // Germany
                            "FRA" => "EUR", // France
                            "ITA" => "EUR", // Italy
                            "ESP" => "EUR", // Spain
                            "NLD" => "EUR", // Netherlands
                            "BEL" => "EUR", // Belgium
                            "AUT" => "EUR", // Austria
                            "IRL" => "EUR", // Ireland
                            "FIN" => "EUR", // Finland
                            "PRT" => "EUR", // Portugal
                            "GRC" => "EUR", // Greece
                            "USA" => "USD",
                            _ => "USD" // Default
                        };
                        _logger.LogInformation("Extracted currency from CountryRegion: {Country} -> {Currency}", country, analysisResult.Currency);
                    }
                    else
                    {
                        analysisResult.Currency = "USD"; // Default to USD instead of assuming GBP
                        _logger.LogInformation("Using default currency: {Currency}", analysisResult.Currency);
                    }
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
                            
                            // Log detailed field information
                            foreach (var field in itemDict)
                            {
                                _logger.LogInformation("Item field '{FieldName}' ({FieldType}): {Content}", 
                                    field.Key, field.Value.FieldType, field.Value.Content);
                            }

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
                                    var qtyValue = quantity.Value?.AsDouble() ?? 0;
                                    receiptItem.Quantity = (decimal)qtyValue;
                                    _logger.LogInformation("Found Quantity (Double): {Quantity}", receiptItem.Quantity);
                                }
                                else if (quantity.FieldType == DocumentFieldType.Int64)
                                {
                                    receiptItem.Quantity = (decimal)(quantity.Value?.AsInt64() ?? 0);
                                    _logger.LogInformation("Found Quantity (Int64): {Quantity}", receiptItem.Quantity);
                                }
                            }
                            
                            // Extract QuantityUnit
                            if (itemDict.TryGetValue("QuantityUnit", out var quantityUnit) && quantityUnit.FieldType == DocumentFieldType.String)
                            {
                                receiptItem.QuantityUnit = quantityUnit.Value?.AsString();
                                _logger.LogInformation("Found QuantityUnit: {QuantityUnit}", receiptItem.QuantityUnit);
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

                            // If quantity is 0 or null but we have total price and unit price, calculate quantity
                            if ((!receiptItem.Quantity.HasValue || receiptItem.Quantity == 0) && 
                                receiptItem.TotalPrice.HasValue && receiptItem.UnitPrice.HasValue && receiptItem.UnitPrice > 0)
                            {
                                receiptItem.Quantity = Math.Round(receiptItem.TotalPrice.Value / receiptItem.UnitPrice.Value, 3);
                                _logger.LogInformation("Calculated Quantity from TotalPrice/UnitPrice: {Quantity} = {TotalPrice} / {UnitPrice}", 
                                    receiptItem.Quantity, receiptItem.TotalPrice, receiptItem.UnitPrice);
                            }
                            
                            // If still no quantity, default to 1
                            if (!receiptItem.Quantity.HasValue || receiptItem.Quantity == 0)
                            {
                                receiptItem.Quantity = 1;
                                _logger.LogInformation("Defaulting Quantity to 1");
                            }

                            if (!string.IsNullOrEmpty(receiptItem.Name))
                            {
                                _logger.LogInformation("Adding receipt item: {Name}, Qty: {Quantity} {QuantityUnit}, UnitPrice: {UnitPrice}, Total: {TotalPrice}", 
                                    receiptItem.Name, receiptItem.Quantity, receiptItem.QuantityUnit, receiptItem.UnitPrice, receiptItem.TotalPrice);
                                analysisResult.Items.Add(receiptItem);
                            }
                        }
                    }
                }
            }

            // Extract raw text
            analysisResult.RawText = result.Content;

            // Log final extracted values for debugging
            _logger.LogInformation("Final extracted values - SubTotal: {SubTotal}, Tax: {Tax}, Total: {Total}, Currency: {Currency}, ReceiptNumber: {ReceiptNumber}", 
                analysisResult.SubTotal, analysisResult.Tax, analysisResult.Total, analysisResult.Currency, analysisResult.ReceiptNumber);

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