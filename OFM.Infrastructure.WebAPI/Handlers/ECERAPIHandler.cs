using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Models;
using System.Text;

namespace OFM.Infrastructure.WebAPI.Handlers
{
    public static class ECERAPIHandler
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        // Static fields to hold the cached token and its expiry time.
        //private static string _cachedToken;
        //private static DateTime _tokenExpiry;

        private static string _accessToken;
        private static DateTime _expiration;
        private static string _cachedToken;

        /// <summary>
        /// Retrieves an access token. Uses a cached token if available and not expired.
        /// </summary>
        public static async Task<string> GetTokenAsync(string _tokenUrl, string _tokenRequestBody)
        {
            // If a token exists and is still valid, return it.
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _expiration)
            {
                return _cachedToken;
            }

            // Otherwise, request a new token from the token endpoint.
            var request = new HttpRequestMessage(HttpMethod.Post, _tokenUrl)
            {
                Content = new StringContent(_tokenRequestBody, Encoding.UTF8, "application/x-www-form-urlencoded")
            };

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            TokenResponse tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseBody);

            // Cache the token for one hour. (Optionally, adjust this based on tokenResponse.expires_in).
            _cachedToken = tokenResponse.access_token;
            _expiration = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);

            return _cachedToken;
        }
    }

}

