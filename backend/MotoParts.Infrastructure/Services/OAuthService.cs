using System.Text.Json;

namespace MotoParts.Infrastructure.Services;

public record ExternalUserInfo(string Subject, string Email, string Name);

/// <summary>Проверка OAuth-токенов Google и обмен кода VK на профиль пользователя.</summary>
public class OAuthService(IHttpClientFactory httpClientFactory, IConfiguration config)
{
    /// <summary>Валидация Google id_token через tokeninfo endpoint.</summary>
    public async Task<ExternalUserInfo?> VerifyGoogleAsync(string idToken)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}");
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var aud = root.GetProperty("aud").GetString();
        if (aud != config["OAuth:Google:ClientId"]) return null;

        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
        var sub = root.GetProperty("sub").GetString();
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : email;
        if (email is null || sub is null) return null;

        return new ExternalUserInfo(sub, email, name ?? email);
    }

    /// <summary>Обмен authorization code VK на access_token + email.</summary>
    public async Task<ExternalUserInfo?> VerifyVkAsync(string code, string redirectUri)
    {
        var client = httpClientFactory.CreateClient();
        var url = "https://oauth.vk.com/access_token" +
                  $"?client_id={config["OAuth:Vk:ClientId"]}" +
                  $"&client_secret={config["OAuth:Vk:ClientSecret"]}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&code={Uri.EscapeDataString(code)}";

        var tokenResponse = await client.GetAsync(url);
        if (!tokenResponse.IsSuccessStatusCode) return null;

        using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var tokenRoot = tokenDoc.RootElement;
        if (!tokenRoot.TryGetProperty("access_token", out var accessTokenEl)) return null;

        var accessToken = accessTokenEl.GetString()!;
        var userId = tokenRoot.GetProperty("user_id").GetInt64().ToString();
        // email приходит только если у приложения VK запрошен scope=email
        var email = tokenRoot.TryGetProperty("email", out var emailEl)
            ? emailEl.GetString()
            : $"vk{userId}@vk.local";

        var profileResponse = await client.GetAsync(
            $"https://api.vk.com/method/users.get?user_ids={userId}&access_token={accessToken}&v=5.199");
        var name = $"VK {userId}";
        if (profileResponse.IsSuccessStatusCode)
        {
            using var profileDoc = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync());
            if (profileDoc.RootElement.TryGetProperty("response", out var arr) && arr.GetArrayLength() > 0)
            {
                var p = arr[0];
                name = $"{p.GetProperty("last_name").GetString()} {p.GetProperty("first_name").GetString()}".Trim();
            }
        }

        return new ExternalUserInfo(userId, email!, name);
    }
}
