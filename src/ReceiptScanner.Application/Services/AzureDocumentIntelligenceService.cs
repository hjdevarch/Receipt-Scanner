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

    public AzureDocumentIntelligenceService(
        IConfiguration configuration, 
        ILogger<AzureDocumentIntelligenceService> logger)
    {
        _logger = logger;
        
        var endpoint = configuration["AzureDocumentIntelligence:Endpoint"] ?? 
                      throw new ArgumentNullException("AzureDocumentIntelligence:Endpoint configuration is missing");
        
        var apiKey = configuration["AzureDocumentIntelligence:ApiKey"] ?? 
                    throw new ArgumentNullException("AzureDocumentIntelligence:ApiKey configuration is missing");

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    private Task<string> GetDefaultCurrencyAsync()
    {
        // Return GBP as default - this is just a fallback when Azure doesn't detect currency
        // The actual user default currency will be handled at the application layer
        return Task.FromResult("GBP");
    }

    public async Task<DocumentAnalysisResult> AnalyzeReceiptAsync(Stream imageStream)
    {
        try
        {
            _logger.LogInformation("Starting receipt analysis with Azure Document Intelligence");

            // Use the prebuilt receipt model
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-receipt", imageStream);
            var result = operation.Value;

            // Log the raw Azure API result to a file for debugging
            await LogAzureResultToFileAsync(result);

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

                // Extract Reward/Discount (handle both Currency and Double types)
                var rewardFieldNames = new[] { "Reward", "Discount", "Savings", "Coupon", "Promotion", "TotalDiscount" };
                foreach (var fieldName in rewardFieldNames)
                {
                    if (receipt.Fields.TryGetValue(fieldName, out var rewardFieldVar))
                    {
                        if (rewardFieldVar.FieldType == DocumentFieldType.Currency)
                        {
                            analysisResult.Reward = (decimal?)rewardFieldVar.Value.AsCurrency().Amount;
                            _logger.LogInformation("Found Reward from '{FieldName}' (Currency): {Reward}", fieldName, analysisResult.Reward);
                            break;
                        }
                        else if (rewardFieldVar.FieldType == DocumentFieldType.Double)
                        {
                            analysisResult.Reward = (decimal?)rewardFieldVar.Value.AsDouble();
                            _logger.LogInformation("Found Reward from '{FieldName}' (Double): {Reward}", fieldName, analysisResult.Reward);
                            break;
                        }
                    }
                }

                // If reward not found in structured fields, search page-level key-value pairs
                if (analysisResult.Reward == null && result.Pages.Count > 0)
                {
                    _logger.LogInformation("Reward not found in structured fields, searching key-value pairs");
                    
                    var rewardKeywords = new[] { "reward", "discount", "savings", "coupon", "promotion", "total savings", "asda rewards", "off your shop" };
                    
                    foreach (var page in result.Pages)
                    {
                        if (page.Lines == null) continue;
                        
                        // Search through lines for reward patterns
                        for (int i = 0; i < page.Lines.Count; i++)
                        {
                            var line = page.Lines[i];
                            var lineText = line.Content.ToLower();
                            
                            // Check if line contains reward keywords
                            if (rewardKeywords.Any(keyword => lineText.Contains(keyword)))
                            {
                                _logger.LogInformation("Found potential reward keyword in line: '{Line}'", line.Content);
                                
                                // Look at the next few lines for a currency value
                                for (int j = i; j < Math.Min(i + 3, page.Lines.Count); j++)
                                {
                                    var nextLine = page.Lines[j].Content;
                                    
                                    // Try to extract currency value (e.g., "-£5.00", "£5.00", "-5.00")
                                    var match = System.Text.RegularExpressions.Regex.Match(nextLine, @"-?[£$€]?\s?(\d+\.?\d*)");
                                    if (match.Success)
                                    {
                                        if (decimal.TryParse(match.Groups[1].Value, out decimal rewardValue))
                                        {
                                            // Store as positive value (discounts are typically positive in our system)
                                            analysisResult.Reward = Math.Abs(rewardValue);
                                            _logger.LogInformation("Extracted Reward from key-value pair: {Reward} (from line: '{Line}')", 
                                                analysisResult.Reward, nextLine);
                                            break;
                                        }
                                    }
                                }
                                
                                if (analysisResult.Reward != null)
                                    break;
                            }
                        }
                        
                        if (analysisResult.Reward != null)
                            break;
                    }
                    
                    if (analysisResult.Reward == null)
                    {
                        _logger.LogInformation("No reward found in key-value pairs");
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

                // Extract currency - prioritize CountryRegion as it's most reliable
                if (receipt.Fields.TryGetValue("CountryRegion", out var countryRegion) && countryRegion.FieldType == DocumentFieldType.CountryRegion)
                {
                    // Log the raw field information for debugging
                    _logger.LogInformation("CountryRegion field found - Type: {Type}, Content: '{Content}', Confidence: {Confidence}", 
                        countryRegion.FieldType, countryRegion.Content, countryRegion.Confidence);
                    
                    // Try to get the country from the value
                    string? country = null;
                    try 
                    {
                        // First try the AsCountryRegion method
                        country = countryRegion.Value?.AsCountryRegion();
                        _logger.LogInformation("AsCountryRegion() returned: '{Country}'", country);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get country using AsCountryRegion()");
                    }
                    
                    // If AsCountryRegion doesn't work, try the Content field directly
                    if (string.IsNullOrEmpty(country) && !string.IsNullOrEmpty(countryRegion.Content))
                    {
                        country = countryRegion.Content;
                        _logger.LogInformation("Using Content field for country: '{Country}'", country);
                    }
                    
                    if (!string.IsNullOrEmpty(country))
                    {
                        var defaultCurrency = await GetDefaultCurrencyAsync();
                        analysisResult.Currency = country switch
                        {
                            "GBR" => "GBP",
                            "GBP" => "GBP",
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
                            _ => defaultCurrency // Use default from settings
                        };
                        _logger.LogInformation("Extracted currency from CountryRegion: {Country} -> {Currency}", country, analysisResult.Currency);
                    }
                    else
                    {
                        _logger.LogWarning("Could not extract country from CountryRegion field, falling back to symbol detection");
                        analysisResult.Currency = await GetDefaultCurrencyAsync(); // Use default from settings
                    }
                }
                else if (receipt.Fields.TryGetValue("Total", out var totalField) && totalField.FieldType == DocumentFieldType.Currency)
                {
                    // Check if Total field has currency symbol in content
                    if (!string.IsNullOrEmpty(totalField.Content))
                    {
                        var currencySymbol = totalField.Content.Substring(0, 1);
                        _logger.LogInformation("Currency info - Symbol from Total Content: '{Symbol}', Content: '{Content}'", currencySymbol, totalField.Content);
                        
                        var defaultCurrency = await GetDefaultCurrencyAsync();
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
                            _ => defaultCurrency // Use default from settings
                        };
                        
                        _logger.LogInformation("Extracted currency from Total field symbol '{Symbol}': {Currency}", currencySymbol, analysisResult.Currency);
                    }
                    else
                    {
                        analysisResult.Currency = await GetDefaultCurrencyAsync(); // Use default from settings
                        _logger.LogInformation("Total field has no currency content, using default: {Currency}", analysisResult.Currency);
                    }
                }
                else
                {
                    // Try to detect currency from item TotalPrice and Price fields if Total field doesn't have currency info
                    string? detectedFromItems = null;
                    if (receipt.Fields.TryGetValue("Items", out var currencyItems) && currencyItems.FieldType == DocumentFieldType.List)
                    {
                        _logger.LogInformation("Attempting to extract currency from {ItemCount} item price fields", currencyItems.Value.AsList().Count);
                        
                        foreach (var item in currencyItems.Value.AsList())
                        {
                            if (item.FieldType == DocumentFieldType.Dictionary)
                            {
                                var itemDict = item.Value.AsDictionary();
                                
                                // Check TotalPrice field first
                                if (itemDict.TryGetValue("TotalPrice", out var totalPriceField))
                                {
                                    var priceContent = totalPriceField.Content;
                                    _logger.LogInformation("Checking TotalPrice field content: '{Content}'", priceContent);
                                    if (!string.IsNullOrEmpty(priceContent) && priceContent.Length > 0)
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
                                
                                // If TotalPrice didn't work, check Price field
                                if (detectedFromItems == null && itemDict.TryGetValue("Price", out var priceField))
                                {
                                    var priceContent = priceField.Content;
                                    _logger.LogInformation("Checking Price field content: '{Content}'", priceContent);
                                    if (!string.IsNullOrEmpty(priceContent) && priceContent.Length > 0)
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
                                            _logger.LogInformation("Extracted currency from item Price field symbol '{Symbol}': {Currency}", symbol, detectedFromItems);
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
                        _logger.LogInformation("Successfully detected currency from item fields: {Currency}", detectedFromItems);
                    }
                    else
                    {
                        analysisResult.Currency = await GetDefaultCurrencyAsync(); // Use default from settings
                        _logger.LogInformation("No currency detected from any fields, using default: {Currency}", analysisResult.Currency);
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
                                _logger.LogInformation("TotalPrice field found - Type: {FieldType}, Content: {Content}, Value: {Value}", 
                                    totalPrice.FieldType, totalPrice.Content, totalPrice.Value);
                                
                                if (totalPrice.FieldType == DocumentFieldType.Currency)
                                {
                                    var currencyValue = totalPrice.Value.AsCurrency();
                                    _logger.LogInformation("TotalPrice Currency - Amount: {Amount}", currencyValue.Amount);
                                    receiptItem.TotalPrice = (decimal?)currencyValue.Amount;
                                    _logger.LogInformation("Set receiptItem.TotalPrice to: {TotalPrice}", receiptItem.TotalPrice);
                                }
                                else if (totalPrice.FieldType == DocumentFieldType.Double)
                                {
                                    receiptItem.TotalPrice = (decimal?)totalPrice.Value.AsDouble();
                                    _logger.LogInformation("Found TotalPrice (Double): {TotalPrice}", receiptItem.TotalPrice);
                                }
                                else
                                {
                                    _logger.LogWarning("TotalPrice field has unexpected type: {FieldType}", totalPrice.FieldType);
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

                            // Only add items that have both a name AND a price
                            if (!string.IsNullOrEmpty(receiptItem.Name) /*&& receiptItem.TotalPrice.HasValue && receiptItem.TotalPrice > 0*/)
                            {
                                _logger.LogInformation("Adding receipt item: {Name}, Qty: {Quantity} {QuantityUnit}, UnitPrice: {UnitPrice}, Total: {TotalPrice}", 
                                    receiptItem.Name, receiptItem.Quantity, receiptItem.QuantityUnit, receiptItem.UnitPrice, receiptItem.TotalPrice);
                                analysisResult.Items.Add(receiptItem);
                            }
                            else if (string.IsNullOrEmpty(receiptItem.Name))
                            {
                                _logger.LogWarning("Skipping item: missing name");
                            }
                            else if (!receiptItem.TotalPrice.HasValue || receiptItem.TotalPrice == 0)
                            {
                                _logger.LogWarning("Skipping item '{Name}': missing or zero price", receiptItem.Name);
                            }
                        }
                    }
                }
            }

            // Extract raw text
            analysisResult.RawText = result.Content;

            // Log final extracted values for debugging
            _logger.LogInformation("Final extracted values - SubTotal: {SubTotal}, Tax: {Tax}, Total: {Total}, Reward: {Reward}, Currency: {Currency}, ReceiptNumber: {ReceiptNumber}", 
                analysisResult.SubTotal, analysisResult.Tax, analysisResult.Total, analysisResult.Reward, analysisResult.Currency, analysisResult.ReceiptNumber);

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

    private async Task LogAzureResultToFileAsync(AnalyzeResult result)
    {
        try
        {
            var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "AzureResults");
            Directory.CreateDirectory(logsDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"AzureResult_{timestamp}.txt";
            var filePath = Path.Combine(logsDirectory, fileName);

            using (var writer = new StreamWriter(filePath))
            {
                await writer.WriteLineAsync($"=== Azure Document Intelligence Result - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                await writer.WriteLineAsync();
                
                await writer.WriteLineAsync($"Content: {result.Content}");
                await writer.WriteLineAsync();
                
                await writer.WriteLineAsync($"Number of Documents: {result.Documents.Count}");
                await writer.WriteLineAsync();

                for (int i = 0; i < result.Documents.Count; i++)
                {
                    var document = result.Documents[i];
                    await writer.WriteLineAsync($"--- Document {i + 1} ---");
                    await writer.WriteLineAsync($"Document Type: {document.DocumentType}");
                    await writer.WriteLineAsync($"Confidence: {document.Confidence}");
                    await writer.WriteLineAsync();
                    
                    await writer.WriteLineAsync("Fields:");
                    foreach (var field in document.Fields)
                    {
                        await writer.WriteLineAsync($"  {field.Key}:");
                        await writer.WriteLineAsync($"    Type: {field.Value.FieldType}");
                        await writer.WriteLineAsync($"    Confidence: {field.Value.Confidence}");
                        
                        try
                        {
                            if (field.Value.FieldType == DocumentFieldType.List)
                            {
                                var list = field.Value.Value.AsList();
                                await writer.WriteLineAsync($"    Value: List with {list.Count} items");
                                
                                // If this is the Items list, show details of each item
                                if (field.Key == "Items")
                                {
                                    for (int itemIndex = 0; itemIndex < list.Count; itemIndex++)
                                    {
                                        var item = list[itemIndex];
                                        await writer.WriteLineAsync($"      Item {itemIndex}:");
                                        
                                        if (item.FieldType == DocumentFieldType.Dictionary)
                                        {
                                            var itemDict = item.Value.AsDictionary();
                                            foreach (var itemField in itemDict)
                                            {
                                                await writer.WriteLineAsync($"        {itemField.Key}:");
                                                await writer.WriteLineAsync($"          Type: {itemField.Value.FieldType}");
                                                await writer.WriteLineAsync($"          Content: {itemField.Value.Content ?? "N/A"}");
                                                
                                                try
                                                {
                                                    var itemValueStr = itemField.Value.FieldType switch
                                                    {
                                                        DocumentFieldType.String => itemField.Value.Value.AsString(),
                                                        DocumentFieldType.Double => itemField.Value.Value.AsDouble().ToString(),
                                                        DocumentFieldType.Currency => itemField.Value.Value.AsCurrency().Amount.ToString(),
                                                        _ => itemField.Value.Content ?? "N/A"
                                                    };
                                                    await writer.WriteLineAsync($"          Value: {itemValueStr}");
                                                }
                                                catch
                                                {
                                                    await writer.WriteLineAsync($"          Value: [Could not extract]");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var valueStr = field.Value.FieldType switch
                                {
                                    DocumentFieldType.String => field.Value.Value.AsString(),
                                    DocumentFieldType.Date => field.Value.Value.AsDate().ToString(),
                                    DocumentFieldType.Time => field.Value.Value.AsTime().ToString(),
                                    DocumentFieldType.PhoneNumber => field.Value.Value.AsPhoneNumber(),
                                    DocumentFieldType.Double => field.Value.Value.AsDouble().ToString(),
                                    DocumentFieldType.Int64 => field.Value.Value.AsInt64().ToString(),
                                    DocumentFieldType.Currency => field.Value.Value.AsCurrency().Amount.ToString(),
                                    _ => field.Value.Content ?? "N/A"
                                };
                                await writer.WriteLineAsync($"    Value: {valueStr}");
                            }
                        }
                        catch
                        {
                            await writer.WriteLineAsync($"    Value: [Could not extract value]");
                        }
                        
                        if (!string.IsNullOrEmpty(field.Value.Content))
                        {
                            await writer.WriteLineAsync($"    Content: {field.Value.Content}");
                        }
                        await writer.WriteLineAsync();
                    }
                }

                await writer.WriteLineAsync("=== End of Azure Result ===");
            }

            _logger.LogInformation("Azure result logged to file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log Azure result to file");
        }
    }
}