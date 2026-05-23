using System.Net;
using System.Text;
using System.Xml;
using FaceScan.Web.Services.Interfaces;

namespace FaceScan.Web.Services;

/// <summary>
/// ONVIF camera discovery and management service implementation
/// Supports standard ONVIF port 8080/8081 and custom port 8899
/// Also supports RTSP streaming on port 554
/// </summary>
public class OnvifCameraService : IOnvifCameraService
{
    private readonly ILogger<OnvifCameraService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public OnvifCameraService(ILogger<OnvifCameraService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<OnvifDeviceInfo?> DiscoverCameraAsync(string ipAddress, int port, string? username = null, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var onvifUri = $"http://{ipAddress}:{port}/onvif/device_service";
            _logger.LogInformation("Discovering ONVIF camera at {OnvifUri}", onvifUri);

            var deviceInfo = await GetDeviceInfoAsync(onvifUri, username, password, cancellationToken);
            if (deviceInfo != null)
            {
                deviceInfo.OnvifUri = onvifUri;
                deviceInfo.Port = port;
                deviceInfo.DiscoveredAtUtc = DateTime.UtcNow;
                deviceInfo.IsOnline = true;

                // Try to get snapshot URI
                deviceInfo.SnapshotUri = await GetSnapshotUriAsync(onvifUri, username, password, cancellationToken);
                
                // Try to get RTSP URI
                deviceInfo.RtspUri = await GetRtspUriAsync(onvifUri, username, password, cancellationToken);
                if (string.IsNullOrEmpty(deviceInfo.RtspUri))
                {
                    // Fallback: construct RTSP URI if not found via ONVIF
                    deviceInfo.RtspUri = ConstructRtspUri(ipAddress, username, password);
                }
                
                _logger.LogInformation("Camera discovered: {Manufacturer} {Model} at {IpAddress}:{Port}", 
                    deviceInfo.Manufacturer, deviceInfo.Model, ipAddress, port);
            }

            return deviceInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover camera at {IpAddress}:{Port}", ipAddress, port);
            return new OnvifDeviceInfo
            {
                ErrorMessage = ex.Message,
                IsOnline = false,
                DiscoveredAtUtc = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> TestConnectionAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = DefaultTimeout;

            var request = new HttpRequestMessage(HttpMethod.Get, onvifUri);
            
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            var isOnline = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
            
            _logger.LogInformation("Connection test to {OnvifUri}: {IsOnline}", onvifUri, isOnline);
            return isOnline;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection test failed for {OnvifUri}", onvifUri);
            return false;
        }
    }

    public async Task<bool> TestRtspConnectionAsync(string rtspUri, string? username = null, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!rtspUri.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid RTSP URI format: {RtspUri}", rtspUri);
                return false;
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = DefaultTimeout;

            var request = new HttpRequestMessage(HttpMethod.Options, rtspUri);
            
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            var isAccessible = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
            
            _logger.LogInformation("RTSP connection test to {RtspUri}: {IsAccessible}", rtspUri, isAccessible);
            return isAccessible;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RTSP connection test failed for {RtspUri}", rtspUri);
            return false;
        }
    }

    public async Task<string?> GetSnapshotUriAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var hostWithPort = ExtractHostFromUri(onvifUri);
            var hostOnly = ExtractHostnameFromUri(onvifUri);

            // Try standard ONVIF snapshot URL patterns
            var snapshotPatterns = new[]
            {
                $"http://{hostWithPort}/ISAPI/Streaming/channels/101/picture",  // Hikvision (same port as ONVIF)
                $"http://{hostWithPort}/cgi-bin/snapshot.cgi",  // Dahua/Generic (same port as ONVIF)
                $"http://{hostWithPort}/onvif/snapshot",  // ONVIF snapshot endpoint
                $"http://{hostWithPort}/snapshot",  // Generic fallback (same port)
                $"http://{hostOnly}/ISAPI/Streaming/channels/101/picture",  // Hikvision default HTTP port
                $"http://{hostOnly}/cgi-bin/snapshot.cgi",  // Dahua/Generic default HTTP port
                $"http://{hostOnly}/snapshot",  // Generic fallback default HTTP port
                $"http://{hostOnly}/onvif/snapshot"  // ONVIF fallback default HTTP port
            };

            foreach (var url in snapshotPatterns)
            {
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = DefaultTimeout;

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                    }

                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    if (response.IsSuccessStatusCode && await IsImageResponseAsync(response, cancellationToken))
                    {
                        _logger.LogInformation("Found snapshot URL: {SnapshotUrl}", url);
                        return url;
                    }
                }
                catch
                {
                    // Try next pattern
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get snapshot URI from {OnvifUri}", onvifUri);
            return null;
        }
    }

    public async Task<string?> GetRtspUriAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var soapRequest = BuildGetStreamUriRequest();
            var request = new HttpRequestMessage(HttpMethod.Post, onvifUri)
            {
                Content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml")
            };

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = DefaultTimeout;
            
            var response = await httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return ParseRtspUriFromResponse(content);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get RTSP URI from {OnvifUri}", onvifUri);
            return null;
        }
    }

    public async Task<OnvifDeviceInfo?> GetDeviceInfoAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = DefaultTimeout;

            var soapRequest = BuildGetSystemDateAndTimeRequest();
            var request = new HttpRequestMessage(HttpMethod.Post, onvifUri)
            {
                Content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml")
            };

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to simple HTTP GET for basic info
                return await GetDeviceInfoViaHttpAsync(onvifUri, username, password, cancellationToken);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseOnvifResponse(content, onvifUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get device info via SOAP, trying HTTP fallback");
            return await GetDeviceInfoViaHttpAsync(onvifUri, username, password, cancellationToken);
        }
    }

    private async Task<OnvifDeviceInfo?> GetDeviceInfoViaHttpAsync(string onvifUri, string? username = null, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = DefaultTimeout;

            var request = new HttpRequestMessage(HttpMethod.Get, onvifUri);
            
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var host = ExtractHostFromUri(onvifUri);
                return new OnvifDeviceInfo
                {
                    Manufacturer = "ONVIF Camera",
                    Model = "Unknown",
                    OnvifUri = onvifUri,
                    IsOnline = true
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP fallback also failed for {OnvifUri}", onvifUri);
            return null;
        }
    }

    private OnvifDeviceInfo? ParseOnvifResponse(string soapResponse, string onvifUri)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(soapResponse);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("soap", "http://www.w3.org/2003/05/soap-envelope");
            namespaceManager.AddNamespace("tds", "http://www.onvif.org/ver10/device/wsdl");
            namespaceManager.AddNamespace("tt", "http://www.onvif.org/ver10/schema");

            var host = ExtractHostFromUri(onvifUri);

            return new OnvifDeviceInfo
            {
                Manufacturer = ExtractXmlValue(xmlDoc, namespaceManager, "//tt:Manufacturer"),
                Model = ExtractXmlValue(xmlDoc, namespaceManager, "//tt:Model"),
                FirmwareVersion = ExtractXmlValue(xmlDoc, namespaceManager, "//tt:FirmwareVersion"),
                SerialNumber = ExtractXmlValue(xmlDoc, namespaceManager, "//tt:SerialNumber"),
                HardwareId = ExtractXmlValue(xmlDoc, namespaceManager, "//tt:HardwareId"),
                OnvifUri = onvifUri,
                IsOnline = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse ONVIF response");
            return new OnvifDeviceInfo
            {
                OnvifUri = onvifUri,
                IsOnline = true,
                Manufacturer = "ONVIF Camera",
                Model = "Unknown"
            };
        }
    }

    private string? ParseRtspUriFromResponse(string soapResponse)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(soapResponse);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("soap", "http://www.w3.org/2003/05/soap-envelope");
            namespaceManager.AddNamespace("trt", "http://www.onvif.org/ver10/media/wsdl");
            namespaceManager.AddNamespace("tt", "http://www.onvif.org/ver10/schema");

            var uriNode = xmlDoc.SelectSingleNode("//tt:Uri", namespaceManager);
            if (uriNode != null && uriNode.InnerText.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            {
                return uriNode.InnerText;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string ExtractXmlValue(XmlDocument doc, XmlNamespaceManager namespaceManager, string xpath)
    {
        try
        {
            var node = doc.SelectSingleNode(xpath, namespaceManager);
            return node?.InnerText ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ExtractHostFromUri(string onvifUri)
    {
        try
        {
            var uri = new Uri(onvifUri);
            return $"{uri.Host}:{uri.Port}";
        }
        catch
        {
            return onvifUri;
        }
    }

    private string ExtractHostnameFromUri(string onvifUri)
    {
        try
        {
            var uri = new Uri(onvifUri);
            return uri.Host;
        }
        catch
        {
            return onvifUri;
        }
    }

    private static async Task<bool> IsImageResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(mediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length >= 4)
            {
                // JPEG SOI (FF D8) or PNG signature (89 50 4E 47)
                var isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8;
                var isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                return isJpeg || isPng;
            }
        }

        return false;
    }

    private string ConstructRtspUri(string ipAddress, string? username = null, string? password = null)
    {
        // Standard RTSP stream URI patterns
        var credentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
            ? $"{username}:{password}@"
            : string.Empty;

        return $"rtsp://{credentials}{ipAddress}:554/h264/ch1/main/av_stream";
    }

    private string BuildGetSystemDateAndTimeRequest()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tds=""http://www.onvif.org/ver10/device/wsdl"">
  <soap:Body>
    <tds:GetSystemDateAndTime/>
  </soap:Body>
</soap:Envelope>";
    }

    private string BuildGetStreamUriRequest()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"">
  <soap:Body>
    <trt:GetStreamUri>
      <trt:StreamSetup>
        <trt:Stream>RTP-Unicast</trt:Stream>
        <trt:Transport>
          <trt:Protocol>RTSP</trt:Protocol>
        </trt:Transport>
      </trt:StreamSetup>
      <trt:ProfileToken>Profile_1</trt:ProfileToken>
    </trt:GetStreamUri>
  </soap:Body>
</soap:Envelope>";
    }
}
