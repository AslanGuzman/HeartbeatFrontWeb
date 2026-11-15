using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Services;

public sealed class MissionControlClient(
    HttpClient httpClient,
    MissionControlTokenRefresher tokenRefresher)
{
    private static readonly JsonSerializerOptions MissionControlJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accessToken = await tokenRefresher.GetAccessTokenAsync(cancellationToken);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await tokenRefresher.ClearSessionAsync(cancellationToken);
        }

        return response;
    }

    public Task<HttpResponseMessage> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default) =>
        httpClient.PostAsJsonAsync("auth/login", request, cancellationToken);

    public async Task<HttpResponseMessage> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "me");
        return await SendAsync(request, cancellationToken);
    }

    public Task<CrewSummaryDto[]> GetCrewAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CrewSummaryDto[]>("crew", cancellationToken);

    public Task<AstronautProfile> GetCrewMemberAsync(
        Guid astronautId,
        CancellationToken cancellationToken = default) =>
        GetAsync<AstronautProfile>($"crew/{astronautId:D}", cancellationToken);

    public Task<AstronautStatusDto> GetAstronautStatusAsync(
        Guid astronautId,
        CancellationToken cancellationToken = default) =>
        GetAsync<AstronautStatusDto>($"crew/{astronautId:D}/status", cancellationToken);

    public Task<Paged<CheckUpDto>> GetCheckUpsAsync(
        Guid astronautId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        HealthStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}"
        };

        if (from is not null)
        {
            query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (to is not null)
        {
            query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (status is not null)
        {
            var statusName = JsonNamingPolicy.CamelCase.ConvertName(status.Value.ToString());
            query.Add($"status={Uri.EscapeDataString(statusName)}");
        }

        var uri = $"crew/{astronautId:D}/checkups?{string.Join("&", query)}";
        return GetAsync<Paged<CheckUpDto>>(uri, cancellationToken);
    }

    public Task<CheckUpDetailDto> GetCheckUpDetailAsync(
        Guid astronautId,
        Guid checkUpId,
        CancellationToken cancellationToken = default) =>
        GetAsync<CheckUpDetailDto>(
            $"crew/{astronautId:D}/checkups/{checkUpId:D}",
            cancellationToken);

    public Task<JsonDocument> GetReferenceRangesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonDocument>("reference-ranges", cancellationToken);

    public Task<Paged<MessageDto>> GetCrewMessagesAsync(
        Guid astronautId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var uri = $"crew/{astronautId:D}/messages" +
                  $"?page={page.ToString(CultureInfo.InvariantCulture)}" +
                  $"&pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}";
        return GetAsync<Paged<MessageDto>>(uri, cancellationToken);
    }

    public async Task<MessageDto> SendCrewMessageAsync(
        Guid astronautId,
        SendMessageRequest message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"crew/{astronautId:D}/messages")
        {
            Content = JsonContent.Create(message, options: MissionControlJsonOptions)
        };
        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MessageDto>(
            MissionControlJsonOptions,
            cancellationToken) ?? throw new InvalidDataException(
                "The Mission Control API returned an empty response.");
    }

    public async Task MarkMessageReadAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"messages/{messageId:D}/read");
        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<Paged<AlertDto>> GetAlertsAsync(
        string state = "open",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var uri = $"alerts?state={Uri.EscapeDataString(state)}" +
                  $"&page={page.ToString(CultureInfo.InvariantCulture)}" +
                  $"&pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}";
        return GetAsync<Paged<AlertDto>>(uri, cancellationToken);
    }

    public async Task<AlertDto> AcknowledgeAlertAsync(
        Guid alertId,
        string note,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"alerts/{alertId:D}/acknowledge")
        {
            Content = JsonContent.Create(
                new AcknowledgeAlertRequest(note),
                options: MissionControlJsonOptions)
        };
        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AlertDto>(
            MissionControlJsonOptions,
            cancellationToken) ?? throw new InvalidDataException(
                "The Mission Control API returned an empty response.");
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(
            MissionControlJsonOptions,
            cancellationToken) ?? throw new InvalidDataException(
                "The Mission Control API returned an empty response.");
    }
}
