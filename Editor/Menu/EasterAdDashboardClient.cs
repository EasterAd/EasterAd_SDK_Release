using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EasterAd_Editor.Menu
{
    internal sealed class EasterAdDashboardClient : IDisposable
    {
        public const string DefaultBaseUrl = "https://dev.easterad.com";

        private readonly string _baseUrl;
        private readonly string _apiKey;

        public EasterAdDashboardClient(string baseUrl, string apiKey)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
            _apiKey = apiKey ?? "";
        }

        public void Dispose()
        {
        }

        public async Task<EasterAdDashboardUser> GetCurrentUserAsync()
        {
            return await GetJsonAsync<EasterAdDashboardUser>("/api/auth/me");
        }

        public async Task<List<EasterAdDashboardOrganization>> GetOrganizationsAsync(EasterAdDashboardUser user)
        {
            List<EasterAdDashboardOrganization> organizations = new List<EasterAdDashboardOrganization>();
            HashSet<string> organizationIds = new HashSet<string>();
            if (user?.permissions?.assumableRoles == null) { return organizations; }

            foreach (EasterAdDashboardAssumableRole role in user.permissions.assumableRoles)
            {
                if (string.IsNullOrEmpty(role.organizationId)) { continue; }
                if (!organizationIds.Add(role.organizationId)) { continue; }

                EasterAdDashboardOrganization organization = await GetOrganizationInfoAsync(role.organizationId, role.organizationType);
                if (organization == null) { continue; }

                if (string.IsNullOrEmpty(organization.organizationType))
                {
                    organization.organizationType = role.organizationType;
                }

                organizations.Add(organization);
            }

            return organizations;
        }

        public async Task<EasterAdDashboardOrganization> GetOrganizationInfoAsync(string organizationId, string organizationType)
        {
            try
            {
                return await GetJsonAsync<EasterAdDashboardOrganization>("/api/core/organization/info/" + Uri.EscapeDataString(organizationId));
            }
            catch (EasterAdDashboardApiException)
            {
                if (string.IsNullOrEmpty(organizationType)) { throw; }
                return await GetJsonAsync<EasterAdDashboardOrganization>(
                    "/api/core/organization/" + Uri.EscapeDataString(organizationType) + "/" + Uri.EscapeDataString(organizationId));
            }
        }

        public async Task<List<EasterAdDashboardGame>> GetGamesAsync(string organizationId)
        {
            return await GetJsonArrayAsync<EasterAdDashboardGame>(
                "/api/core/organization/" + Uri.EscapeDataString(organizationId) + "/apps");
        }

        public async Task<List<EasterAdDashboardAdUnit>> GetAdUnitsAsync(string organizationId, string gameId)
        {
            return await GetJsonArrayAsync<EasterAdDashboardAdUnit>(
                "/api/core/organization/" + Uri.EscapeDataString(organizationId) +
                "/apps/" + Uri.EscapeDataString(gameId) + "/adUnits");
        }

        public async Task<EasterAdDashboardAdUnit> CreateAdUnitAsync(string organizationId, string gameId, string name)
        {
            EasterAdDashboardCreateAdUnitRequest body = new EasterAdDashboardCreateAdUnitRequest
            {
                name = name,
                type = "Display"
            };
            return await PostJsonAsync<EasterAdDashboardAdUnit>(
                "/api/core/organization/" + Uri.EscapeDataString(organizationId) +
                "/apps/" + Uri.EscapeDataString(gameId) + "/adUnits",
                JsonUtility.ToJson(body));
        }

        public async Task ArchiveAdUnitAsync(string organizationId, string gameId, string adUnitId)
        {
            await SendAsync(
                "DELETE",
                "/api/core/organization/" + Uri.EscapeDataString(organizationId) +
                "/apps/" + Uri.EscapeDataString(gameId) + "/adUnits/" + Uri.EscapeDataString(adUnitId),
                null);
        }

        public async Task<EasterAdDashboardAdUnit> RestoreAdUnitAsync(string organizationId, string gameId, string adUnitId)
        {
            return await PostJsonAsync<EasterAdDashboardAdUnit>(
                "/api/core/organization/" + Uri.EscapeDataString(organizationId) +
                "/apps/" + Uri.EscapeDataString(gameId) + "/adUnits/" + Uri.EscapeDataString(adUnitId) + "/restore",
                null);
        }

        private async Task<T> GetJsonAsync<T>(string path)
        {
            string json = await SendAsync("GET", path, null);
            return JsonUtility.FromJson<T>(json);
        }

        private async Task<T> PostJsonAsync<T>(string path, string jsonBody)
        {
            string json = await SendAsync("POST", path, jsonBody);
            return JsonUtility.FromJson<T>(json);
        }

        private async Task<List<T>> GetJsonArrayAsync<T>(string path)
        {
            string json = await SendAsync("GET", path, null);
            EasterAdDashboardJsonArray<T> wrapper = JsonUtility.FromJson<EasterAdDashboardJsonArray<T>>("{\"items\":" + json + "}");
            return wrapper?.items ?? new List<T>();
        }

        private async Task<string> SendAsync(string method, string path, string jsonBody)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Dashboard API key is empty.");
            }

            using (UnityWebRequest request = new UnityWebRequest(_baseUrl + path, method))
            {
                request.timeout = 20;
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + _apiKey);
                request.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrEmpty(jsonBody))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                string content = request.downloadHandler == null ? "" : request.downloadHandler.text;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new EasterAdDashboardApiException(request.responseCode, content);
                }

                return content;
            }
        }
    }

    internal sealed class EasterAdDashboardApiException : Exception
    {
        public long StatusCode { get; }

        public EasterAdDashboardApiException(long statusCode, string responseText)
            : base("Dashboard request failed with HTTP " + statusCode + ".")
        {
            StatusCode = statusCode;
            ResponseText = responseText;
        }

        public string ResponseText { get; }
    }

    [Serializable]
    internal sealed class EasterAdDashboardJsonArray<T>
    {
        public List<T> items = new List<T>();
    }

    [Serializable]
    internal sealed class EasterAdDashboardCreateAdUnitRequest
    {
        public string name = "";
        public string type = "Display";
    }

    [Serializable]
    internal sealed class EasterAdDashboardUser
    {
        public EasterAdDashboardPermissions permissions = new EasterAdDashboardPermissions();
    }

    [Serializable]
    internal sealed class EasterAdDashboardPermissions
    {
        public List<EasterAdDashboardAssumableRole> assumableRoles = new List<EasterAdDashboardAssumableRole>();
    }

    [Serializable]
    internal sealed class EasterAdDashboardAssumableRole
    {
        public string roleId = "";
        public string organizationId = "";
        public string organizationType = "";
    }

    [Serializable]
    internal sealed class EasterAdDashboardOrganization
    {
        public string _id = "";
        public string name = "";
        public string organizationType = "";
        public string universalSdkKey = "";
    }

    [Serializable]
    internal sealed class EasterAdDashboardGame
    {
        public string _id = "";
        public string name = "";
        public string platform = "";
        public string storeUrl = "";
        public string currentStatus = "";
        public string sdkKey = "";
        public string deletedAt = "";
    }

    [Serializable]
    internal sealed class EasterAdDashboardAdUnit
    {
        public string _id = "";
        public string appId = "";
        public string name = "";
        public string type = "";
        public string deletedAt = "";
    }
}
