using System.Net.Http.Json;

namespace Example.Shared;

public class SomeServiceClient
{
    private readonly HttpClient _client;
    
    public SomeServiceClient(HttpClient httpClient)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri("http://localhost:5000");
    }

    public async Task<IReadOnlyList<User>> BrowseAsync()
    {
        return await _client.GetFromJsonAsync<IReadOnlyList<User>>("api/users") ?? [];
    }
}