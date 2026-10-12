using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CampusEats.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
public class ApiTests
{
    [Theory]
    [InlineData("invalid", "long-enough-password", false)]
    [InlineData("student@example.test", "short", false)]
    [InlineData("student@example.test", "long-enough-password", true)]
    public void CredentialsAreValidated(string email,string password,bool expected) => Assert.Equal(expected,new Credentials(email,password).IsValid());

    // Requires a dedicated migrated PostgreSQL database. Fails clearly instead of silently skipping.
    [Fact]
    public async Task DatabaseMenuCorsAndJwtWorkTogether()
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Default")), "Set ConnectionStrings__Default to a dedicated migrated test database.");
        using var factory=new WebApplicationFactory<Program>();
        using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);
        var menu=await client.GetFromJsonAsync<MenuItem[]>("/api/menu");
        Assert.NotNull(menu); Assert.Equal(8,menu.Length); Assert.All(menu,x=>Assert.True(x.Price>0));
        var drinks=await client.GetFromJsonAsync<MenuItem[]>("/api/menu?category=drinks");
        Assert.NotNull(drinks); Assert.Equal(2,drinks.Length); Assert.All(drinks,x=>Assert.Equal("Drinks",x.Category));
        using var preflight=new HttpRequestMessage(HttpMethod.Options,"/api/menu");
        preflight.Headers.Add("Origin","http://localhost:5173");preflight.Headers.Add("Access-Control-Request-Method","GET");
        var cors=await client.SendAsync(preflight);Assert.Equal("http://localhost:5173",cors.Headers.GetValues("Access-Control-Allow-Origin").Single());
        using var denied=new HttpRequestMessage(HttpMethod.Options,"/api/menu");
        denied.Headers.Add("Origin","https://untrusted.example");denied.Headers.Add("Access-Control-Request-Method","GET");
        Assert.False((await client.SendAsync(denied)).Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
        var input=new Credentials($"student-{Guid.NewGuid():N}@example.test",Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync("/api/auth/register",input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/auth/register",input)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/login",input with { Password="incorrect-password" })).StatusCode);
        var response=await client.PostAsJsonAsync("/api/auth/login",input);response.EnsureSuccessStatusCode();
        var token=await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token.GetProperty("token").GetString());
        var me=await client.GetFromJsonAsync<JsonElement>("/api/auth/me");Assert.Equal(input.Email,me.GetProperty("email").GetString());
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer","invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
