using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MotoParts.Api.Services;

public record YooKassaPayment(string Id, string Status, string? ConfirmationUrl);

/// <summary>
/// Интеграция с ЮKassa (https://yookassa.ru/developers/api).
/// Оплата банковскими картами РФ: payment_method_data.type = "bank_card".
/// </summary>
public class YooKassaService(IHttpClientFactory httpClientFactory, IConfiguration config)
{
    private const string BaseUrl = "https://api.yookassa.ru/v3";

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        var shopId = config["YooKassa:ShopId"];
        var secretKey = config["YooKassa:SecretKey"];
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{shopId}:{secretKey}")));
        return client;
    }

    /// <summary>Создаёт платёж и возвращает URL страницы подтверждения ЮKassa.</summary>
    public async Task<YooKassaPayment> CreatePaymentAsync(
        decimal amount, string description, string returnUrl, Guid orderId)
    {
        var client = CreateClient();

        var body = new
        {
            amount = new { value = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), currency = "RUB" },
            payment_method_data = new { type = "bank_card" },
            confirmation = new { type = "redirect", return_url = returnUrl },
            capture = true,
            description,
            metadata = new { order_id = orderId.ToString() },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/payments")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        // Ключ идемпотентности обязателен для POST-запросов ЮKassa
        request.Headers.Add("Idempotence-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ЮKassa вернула ошибку {(int)response.StatusCode}: {json}");

        return ParsePayment(json);
    }

    public async Task<YooKassaPayment> GetPaymentAsync(string paymentId)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"{BaseUrl}/payments/{paymentId}");
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ЮKassa вернула ошибку {(int)response.StatusCode}: {json}");

        return ParsePayment(json);
    }

    private static YooKassaPayment ParsePayment(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string? confirmationUrl = null;
        if (root.TryGetProperty("confirmation", out var confirmation) &&
            confirmation.TryGetProperty("confirmation_url", out var urlEl))
        {
            confirmationUrl = urlEl.GetString();
        }

        return new YooKassaPayment(
            root.GetProperty("id").GetString()!,
            root.GetProperty("status").GetString()!,
            confirmationUrl);
    }
}
