using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVital.IntegrationTests.Helpers;

public static class GqlHelper
{
    public static async Task<JsonDocument> ExecuteQueryAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        if (token != null)
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        else
            client.DefaultRequestHeaders.Authorization = null;

        var body = new { query, variables };
        var response = await client.PostAsJsonAsync("/graphql", body);
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    public static async Task<string?> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var mutation = """
            mutation Login($email: String!, $password: String!) {
                login(email: $email, password: $password) {
                    token
                    academyId
                    expiresAt
                    user { id name email role }
                }
            }
            """;

        var result = await ExecuteQueryAsync(client, mutation,
            new { email, password });

        return result.RootElement
            .GetProperty("data")
            .GetProperty("login")
            .GetProperty("token")
            .GetString();
    }
}
