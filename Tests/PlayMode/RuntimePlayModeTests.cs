using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EasterAd.Contracts.Http;
using EasterAd_Dependencies.Common;
using EasterAd_Dependencies;
using EasterAd_Implementation;
using EasterAd_Implementation.Library;
using EasterAd_Implementation.Privacy;
using Google.Protobuf;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AdSegmentationManager = EasterAd_Dependencies.Unity.AdSegmentationManager;
using DependencyGameObject = EasterAd_Dependencies.Unity.GameObject;
using ContractAdRequest = EasterAd.Contracts.Serving.AdRequest;
using ContractAdResponse = EasterAd.Contracts.Serving.AdResponse;
using ContractDevice = EasterAd.Contracts.AdCom.Device;
using ContractDeviceType = EasterAd.Contracts.AdCom.DeviceType;
using ContractOperatingSystem = EasterAd.Contracts.AdCom.OperatingSystem;
using CreateSessionRequest = EasterAd.Contracts.Serving.CreateSessionRequest;
using DisplayLogEntry = EasterAd.Contracts.Serving.DisplayLogEntry;
using ImpressionRequest = EasterAd.Contracts.Serving.ImpressionRequest;
using InstanceManager = EasterAd_Dependencies.Unity.InstanceManager;
using Object = UnityEngine.Object;
using RuntimeUI = EasterAd_Dependencies.Unity.UI;
using UnityComponentManager = EasterAd_Dependencies.Unity.ComponentManager;

namespace EasterAd.Tests.PlayMode
{
    public sealed class RuntimePlayModeTests
    {
        private Material assignedMaterial;
        private Camera testCamera;
        private RenderTexture testRenderTexture;
        private Texture2D sourceTexture;
        private Texture2D readbackTexture;
        private UnityEngine.GameObject unityObject;
        private UnityEngine.GameObject cameraObject;
        private string registeredClientKey;
        private int registeredSegmentationId;
        private IDisposable componentManagerOverride;
        private IDisposable impressionLoggingOverride;
        private ICamera originalMainCamera;
        private bool originalLogEnable;
        private List<string> originalDebugLogs;
        private List<RuntimeUI.DebugMesh> originalDebugMeshes;
        private UnityEngine.GameObject preExistingClientObject;
        private string preExistingClientKey;
        private ItemClient originalPreExistingClient;
        private IDisposable runtimePlatformOverride;
        private EasterAdSdkClient mobileSdkClient;
        private MockMobileAdProvider mobileProvider;
        private List<ItemClient> mobileItemClients;
        private List<UnityEngine.GameObject> mobileItemObjects;

        private sealed class MockImageComponentManager : IComponentManager
        {
            private readonly Renderer renderer;
            private readonly Texture2D texture;

            internal MockImageComponentManager(Renderer renderer, Texture2D texture)
            {
                this.renderer = renderer;
                this.texture = texture;
            }

            public void RunRequest(string rootUrl, string url, string session, string body,
                IGameObject gameObject, Func<bool> isActive,
                Action<Dictionary<string, object>> callback)
            {
                if (!isActive()) { return; }
                renderer.material.mainTexture = texture;
                callback(new Dictionary<string, object>
                {
                    { "_id", "444444444444444444444444" },
                    { "url", "https://fixtures.invalid/ad-image.png" },
                    { "mime", "image/png" },
                    { "width", 2 },
                    { "height", 2 },
                    { "interactionUrl", string.Empty }
                });
            }
        }

        private sealed class CountingComponentManager : IComponentManager
        {
            internal int RequestCount { get; private set; }
            internal string LastBody { get; private set; }

            public void RunRequest(string rootUrl, string url, string session, string body,
                IGameObject gameObject, Func<bool> isActive,
                Action<Dictionary<string, object>> callback)
            {
                if (!isActive()) { return; }
                RequestCount++;
                LastBody = body;
            }
        }

        private sealed class MockSessionSystemInfo : ISessionSystemInfo
        {
            internal string Language = "en";
            internal string Platform = "WindowsEditor";
            internal int DeviceType = 2;
            internal string DeviceModel = "Fixture PC";
            internal string OperatingSystem = "Windows";
            internal string OperatingSystemVersion = "11";
            internal int ScreenWidth = 1920;
            internal int ScreenHeight = 1080;
            internal int ScreenPpi = 96;
            internal string ApplicationIdentifier = "com.easterad.fixture";
            internal string ApplicationVersion = "2.0.0";

            public string GetLanguage() => Language;
            public string GetPlatform() => Platform;
            public int GetDeviceType() => DeviceType;
            public string GetDeviceModel() => DeviceModel;
            public string GetOperatingSystem() => OperatingSystem;
            public string GetOperatingSystemVersion() => OperatingSystemVersion;
            public int GetScreenWidth() => ScreenWidth;
            public int GetScreenHeight() => ScreenHeight;
            public int GetScreenPpi() => ScreenPpi;
            public string GetApplicationIdentifier() => ApplicationIdentifier;
            public string GetApplicationVersion() => ApplicationVersion;
        }

        private sealed class ProtobufCaptureComponentManager : IComponentManager, IProtobufComponentManager
        {
            internal ContractAdRequest LastRequest { get; private set; }
            internal string LastUrl { get; private set; }
            internal string LastMethod { get; private set; }
            internal string LastSession { get; private set; }
            internal byte[] ResponseBody { get; set; } = Array.Empty<byte>();
            internal string Error { get; set; } = "";
            internal bool Retryable { get; set; }
            internal string UpdatedSession { get; set; } = "";
            internal int LegacyRequestCount { get; private set; }
            internal int ProtobufRequestCount { get; private set; }

            public void RunRequest(string rootUrl, string url, string session, string body,
                IGameObject gameObject, Func<bool> isActive,
                Action<Dictionary<string, object>> callback)
            {
                LegacyRequestCount++;
                throw new InvalidOperationException("Typed tests must not enter the legacy JSON transport.");
            }

            public void RunProtobufRequest(string url, string method, string session, byte[] body,
                IGameObject gameObject, Func<bool> isActive,
                Action<byte[], string, bool, string> callback)
            {
                if (!isActive()) return;
                ProtobufRequestCount++;
                LastUrl = url;
                LastMethod = method;
                LastSession = session;
                LastRequest = ContractAdRequest.Parser.ParseFrom(body);
                callback(ResponseBody, Error, Retryable, UpdatedSession);
            }

            public void LoadImage(string url, string mime, int expectedWidth, int expectedHeight,
                IGameObject gameObject, Func<bool> isActive, Action<string, bool> callback)
            {
                if (isActive()) callback("", false);
            }
        }

        private sealed class DeferredImageProtobufComponentManager : IComponentManager,
            IProtobufComponentManager
        {
            private readonly List<Action<string, bool>> imageCompletions =
                new List<Action<string, bool>>();

            internal List<string> RequestSessions { get; } = new List<string>();
            internal int RequestCount { get; private set; }

            public void RunRequest(string rootUrl, string url, string session, string body,
                IGameObject gameObject, Func<bool> isActive,
                Action<Dictionary<string, object>> callback)
            {
                throw new InvalidOperationException("Typed fixture entered the legacy transport.");
            }

            public void RunProtobufRequest(string url, string method, string session, byte[] body,
                IGameObject gameObject, Func<bool> isActive,
                Action<byte[], string, bool, string> callback)
            {
                if (!isActive()) return;
                RequestCount++;
                RequestSessions.Add(session);
                string fillId = RequestCount == 1
                    ? "444444444444444444444444"
                    : "555555555555555555555555";
                string updatedSession = RequestCount == 1 ? "session=first" : "session=second";
                callback(new ContractAdResponse
                {
                    Id = fillId,
                    Mime = "image/png",
                    Width = 16,
                    Height = 9,
                    AspectRatio = 1.78,
                    Url = "https://fixtures.invalid/" + fillId + ".png"
                }.ToByteArray(), "", false, updatedSession);
            }

            public void LoadImage(string url, string mime, int expectedWidth, int expectedHeight,
                IGameObject gameObject, Func<bool> isActive, Action<string, bool> callback)
            {
                imageCompletions.Add((error, retryable) =>
                {
                    if (isActive()) callback(error, retryable);
                });
            }

            internal void CompleteImage(int index, string error = "", bool retryable = false)
            {
                imageCompletions[index](error, retryable);
            }
        }

        private sealed class DeferredComponentManager : IComponentManager
        {
            private Action<Dictionary<string, object>> completion;

            internal int RequestCount { get; private set; }

            public void RunRequest(string rootUrl, string url, string session, string body,
                IGameObject gameObject, Func<bool> isActive,
                Action<Dictionary<string, object>> callback)
            {
                if (!isActive()) { return; }
                RequestCount++;
                completion = callback;
            }

            internal void Complete(Dictionary<string, object> result)
            {
                completion?.Invoke(result);
            }
        }

        private sealed class RetryThenSuccessComponentManager : IComponentManager
        {
            internal int RequestCount { get; private set; }

            public void RunRequest(string rootUrl, string url, string session, string body,
                IGameObject gameObject, Func<bool> isActive,
                Action<Dictionary<string, object>> callback)
            {
                if (!isActive()) { return; }
                RequestCount++;
                if (RequestCount == 1)
                {
                    callback(new Dictionary<string, object>
                    {
                        { "adError", "raw fixture text must not escape" },
                        { "adErrorType", "NetworkError" },
                        { "retryable", true }
                    });
                    return;
                }

                callback(new Dictionary<string, object>
                {
                    { "_id", "444444444444444444444444" },
                    { "url", "https://fixtures.invalid/newer-image.png" },
                    { "mime", "image/png" },
                    { "width", 2 },
                    { "height", 2 }
                });
            }
        }

        private sealed class MockMobileAdHandle : IDisposable
        {
            private readonly bool throwOnDispose;
            private readonly Action duringDispose;

            internal MockMobileAdHandle(bool throwOnDispose = false, Action duringDispose = null)
            {
                this.throwOnDispose = throwOnDispose;
                this.duringDispose = duringDispose;
            }

            internal int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                duringDispose?.Invoke();
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("fixture handle cleanup failure");
                }
            }
        }

        private sealed class MockMobileAdProvider : global::EasterAd.IEasterAdMobileAdProvider
        {
            private Action<global::EasterAd.EasterAdMobileAdResult> completion;

            internal int LoadAndShowCount { get; private set; }
            internal global::EasterAd.EasterAdMobileAdRequest LastRequest { get; private set; }
            internal MockMobileAdHandle LastHandle { get; private set; }
            internal global::EasterAd.EasterAdMobileAdResult SynchronousResult { get; set; }
            internal Action AfterSynchronousCompletion { get; set; }
            internal Action DuringInvocation { get; set; }
            internal bool ReturnNullHandle { get; set; }
            internal bool ThrowAfterSynchronousCompletion { get; set; }
            internal bool ThrowOnHandleDispose { get; set; }
            internal Action DuringHandleDispose { get; set; }

            public IDisposable LoadAndShow(global::EasterAd.EasterAdMobileAdRequest request,
                Action<global::EasterAd.EasterAdMobileAdResult> callback)
            {
                LoadAndShowCount++;
                LastRequest = request;
                completion = callback;
                DuringInvocation?.Invoke();
                if (SynchronousResult != null)
                {
                    callback(SynchronousResult);
                    AfterSynchronousCompletion?.Invoke();
                }

                if (ThrowAfterSynchronousCompletion)
                {
                    throw new InvalidOperationException("fixture provider failure");
                }

                if (ReturnNullHandle)
                {
                    return null;
                }

                LastHandle = new MockMobileAdHandle(ThrowOnHandleDispose, DuringHandleDispose);
                return LastHandle;
            }

            internal void Complete(global::EasterAd.EasterAdMobileAdResult result)
            {
                completion?.Invoke(result);
            }
        }

        private sealed class SingleResponseServer : IDisposable
        {
            private readonly TcpListener listener;
            private readonly Task serverTask;
            private readonly int statusCode;
            private readonly string contentType;
            private readonly byte[] responseBody;
            private readonly string setCookie;
            private Exception serverError;

            internal SingleResponseServer(int statusCode, string contentType, byte[] responseBody,
                string setCookie = "")
            {
                this.statusCode = statusCode;
                this.contentType = contentType;
                this.responseBody = responseBody ?? Array.Empty<byte>();
                this.setCookie = setCookie ?? "";
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Url = "http://127.0.0.1:" + port + "/proto";
                serverTask = Task.Run(Serve);
            }

            internal string Url { get; }
            internal string RequestHeaders { get; private set; }
            internal byte[] RequestBody { get; private set; } = Array.Empty<byte>();

            private void Serve()
            {
                try
                {
                    using TcpClient client = listener.AcceptTcpClient();
                    using NetworkStream stream = client.GetStream();
                    stream.ReadTimeout = 5000;
                    ReadRequest(stream);

                    string reason = statusCode == 200 ? "OK" :
                        statusCode == 201 ? "Created" :
                        statusCode == 204 ? "No Content" :
                        statusCode == 302 ? "Found" :
                        statusCode == 401 ? "Unauthorized" :
                        statusCode == 404 ? "Not Found" :
                        statusCode == 408 ? "Request Timeout" :
                        statusCode == 429 ? "Too Many Requests" :
                        statusCode >= 500 ? "Server Error" : "Response";
                    var headers = new StringBuilder()
                        .Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reason).Append("\r\n")
                        .Append("Content-Length: ").Append(responseBody.Length).Append("\r\n");
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        headers.Append("Content-Type: ").Append(contentType).Append("\r\n");
                    }
                    if (!string.IsNullOrEmpty(setCookie))
                    {
                        headers.Append("Set-Cookie: ").Append(setCookie).Append("; Path=/\r\n");
                    }
                    if (statusCode == 302)
                    {
                        headers.Append("Location: http://127.0.0.1:1/blocked\r\n");
                    }
                    headers.Append("Connection: close\r\n\r\n");
                    byte[] headerBytes = Encoding.ASCII.GetBytes(headers.ToString());
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    if (responseBody.Length > 0)
                    {
                        stream.Write(responseBody, 0, responseBody.Length);
                    }
                    stream.Flush();
                }
                catch (Exception error)
                {
                    serverError = error;
                }
            }

            private void ReadRequest(NetworkStream stream)
            {
                var headerBytes = new List<byte>();
                while (headerBytes.Count < 64 * 1024)
                {
                    int value = stream.ReadByte();
                    if (value < 0) throw new EndOfStreamException("Request ended before headers completed.");
                    headerBytes.Add((byte)value);
                    int count = headerBytes.Count;
                    if (count >= 4 && headerBytes[count - 4] == '\r' && headerBytes[count - 3] == '\n' &&
                        headerBytes[count - 2] == '\r' && headerBytes[count - 1] == '\n')
                    {
                        break;
                    }
                }

                RequestHeaders = Encoding.ASCII.GetString(headerBytes.ToArray());
                int contentLength = 0;
                foreach (string line in RequestHeaders.Split(new[] { "\r\n" }, StringSplitOptions.None))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
                    }
                }

                RequestBody = new byte[contentLength];
                int offset = 0;
                while (offset < contentLength)
                {
                    int read = stream.Read(RequestBody, offset, contentLength - offset);
                    if (read <= 0) throw new EndOfStreamException("Request body ended early.");
                    offset += read;
                }
            }

            internal void ThrowIfFaulted()
            {
                if (serverError != null) throw new AssertionException(serverError.ToString());
            }

            public void Dispose()
            {
                listener.Stop();
                try
                {
                    serverTask.Wait(2000);
                }
                catch (AggregateException error)
                {
                    serverError ??= error.Flatten().InnerException;
                }
            }
        }

        private sealed class BlockingImageServer : IDisposable
        {
            private readonly TcpListener listener;
            private readonly ManualResetEventSlim imageRequestReceived = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim releaseImageResponse = new ManualResetEventSlim(false);
            private readonly Task serverTask;
            private readonly byte[] imageResponse;
            private readonly string creativeMime;
            private readonly int declaredWidth;
            private readonly int declaredHeight;
            private readonly bool omitImageContentLength;
            private readonly int imageChunkSize;
            private readonly int imageChunkDelayMilliseconds;
            private readonly string imageRedirectLocation;
            private readonly bool preserveCookieFixture;
            private readonly bool closeImageWithoutResponse;
            private Exception serverError;

            internal BlockingImageServer(byte[] imageResponse = null, string creativeMime = "image/png",
                int declaredWidth = 2, int declaredHeight = 2, bool omitImageContentLength = false,
                int imageChunkSize = 0, int imageChunkDelayMilliseconds = 0,
                string imageRedirectLocation = null, bool preserveCookieFixture = false,
                bool closeImageWithoutResponse = false)
            {
                this.imageResponse = imageResponse;
                this.creativeMime = creativeMime;
                this.declaredWidth = declaredWidth;
                this.declaredHeight = declaredHeight;
                this.omitImageContentLength = omitImageContentLength;
                this.imageChunkSize = imageChunkSize;
                this.imageChunkDelayMilliseconds = imageChunkDelayMilliseconds;
                this.imageRedirectLocation = imageRedirectLocation;
                this.preserveCookieFixture = preserveCookieFixture;
                this.closeImageWithoutResponse = closeImageWithoutResponse;
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                RootUrl = "http://127.0.0.1:" + port + "/";
                ImageRootUrl = preserveCookieFixture ? "http://localhost:" + port + "/" : RootUrl;
                serverTask = Task.Run(Serve);
            }

            internal string RootUrl { get; }
            internal string ImageRootUrl { get; }
            internal string AdRequestUrl => RootUrl + "ad";
            internal string CookieSeedUrl => ImageRootUrl + "seed";
            internal string CookieProbeUrl => ImageRootUrl + "probe";
            internal bool ImageRequested => imageRequestReceived.IsSet;
            internal string ImageRequestHeaders { get; private set; } = "";
            internal string CookieProbeRequestHeaders { get; private set; } = "";

            internal void ReleaseImageResponse()
            {
                releaseImageResponse.Set();
            }

            internal void ThrowIfFaulted()
            {
                if (serverError != null)
                {
                    throw new AssertionException("Local creative fixture failed: " + serverError);
                }
            }

            public void Dispose()
            {
                releaseImageResponse.Set();
                listener.Stop();
                serverTask.Wait(TimeSpan.FromSeconds(2));
                imageRequestReceived.Dispose();
                releaseImageResponse.Dispose();
            }

            private void Serve()
            {
                try
                {
                    if (preserveCookieFixture)
                    {
                        using (TcpClient seedClient = listener.AcceptTcpClient())
                        {
                            ReadRequest(seedClient.GetStream());
                            WriteResponse(seedClient.GetStream(), "text/plain", Array.Empty<byte>(),
                                "Set-Cookie: easterad_fixture_cookie=preserve; Path=/\r\n");
                        }
                    }

                    using (TcpClient adClient = listener.AcceptTcpClient())
                    {
                        ReadRequest(adClient.GetStream());
                        string body = "{\"_id\":\"late-image\",\"url\":\"" + ImageRootUrl +
                                      "image\",\"mime\":\"" + creativeMime + "\",\"width\":" +
                                      declaredWidth + ",\"height\":" + declaredHeight + "}";
                        WriteResponse(adClient.GetStream(), "application/json", Encoding.UTF8.GetBytes(body));
                    }

                    using (TcpClient imageClient = listener.AcceptTcpClient())
                    {
                        ImageRequestHeaders = ReadRequest(imageClient.GetStream());
                        imageRequestReceived.Set();
                        if (closeImageWithoutResponse)
                        {
                            // Closing the connection without a response deterministically exercises a local image-network failure.
                        }
                        else if (imageResponse == null)
                        {
                            releaseImageResponse.Wait(TimeSpan.FromSeconds(10));
                        }
                        else if (!string.IsNullOrEmpty(imageRedirectLocation))
                        {
                            WriteRedirect(imageClient.GetStream(), imageRedirectLocation);
                        }
                        else
                        {
                            WriteImageResponse(imageClient.GetStream());
                        }
                    }

                    if (preserveCookieFixture)
                    {
                        using (TcpClient probeClient = listener.AcceptTcpClient())
                        {
                            CookieProbeRequestHeaders = ReadRequest(probeClient.GetStream());
                            WriteResponse(probeClient.GetStream(), "text/plain", Array.Empty<byte>());
                        }
                    }
                }
                catch (Exception exception)
                {
                    bool expectedClientDisconnect = imageResponse != null && imageRequestReceived.IsSet &&
                                                    (exception is IOException || exception is SocketException);
                    if (!releaseImageResponse.IsSet && !expectedClientDisconnect)
                    {
                        serverError = exception;
                    }
                }
            }

            private static string ReadRequest(NetworkStream stream)
            {
                var request = new List<byte>();
                int headerEnd = -1;
                while (headerEnd < 0)
                {
                    int value = stream.ReadByte();
                    if (value < 0) { throw new InvalidOperationException("Unexpected end of HTTP request."); }
                    request.Add((byte)value);
                    int count = request.Count;
                    if (count >= 4 && request[count - 4] == '\r' && request[count - 3] == '\n' &&
                        request[count - 2] == '\r' && request[count - 1] == '\n')
                    {
                        headerEnd = count;
                    }
                }

                string headers = Encoding.ASCII.GetString(request.ToArray());
                int contentLength = 0;
                foreach (string line in headers.Split(new[] { "\r\n" }, StringSplitOptions.None))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
                    }
                }

                for (int index = 0; index < contentLength; index++)
                {
                    if (stream.ReadByte() < 0) { throw new InvalidOperationException("Unexpected end of HTTP body."); }
                }

                return headers;
            }

            private void WriteImageResponse(NetworkStream stream)
            {
                string headers = "HTTP/1.1 200 OK\r\nContent-Type: " + creativeMime + "\r\n";
                if (!omitImageContentLength)
                {
                    headers += "Content-Length: " + imageResponse.Length + "\r\n";
                }

                headers += "Connection: close\r\n\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                stream.Write(headerBytes, 0, headerBytes.Length);

                int chunkSize = imageChunkSize > 0 ? imageChunkSize : imageResponse.Length;
                for (int offset = 0; offset < imageResponse.Length; offset += chunkSize)
                {
                    int count = Math.Min(chunkSize, imageResponse.Length - offset);
                    stream.Write(imageResponse, offset, count);
                    stream.Flush();
                    if (imageChunkDelayMilliseconds > 0)
                    {
                        Thread.Sleep(imageChunkDelayMilliseconds);
                    }
                }
            }

            private static void WriteRedirect(NetworkStream stream, string location)
            {
                string headers = "HTTP/1.1 302 Found\r\nLocation: " + location +
                                 "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Flush();
            }

            private static void WriteResponse(NetworkStream stream, string contentType, byte[] body,
                string additionalHeaders = "")
            {
                string headers = "HTTP/1.1 200 OK\r\nContent-Type: " + contentType +
                                 "\r\nContent-Length: " + body.Length + "\r\n" + additionalHeaders +
                                 "Connection: close\r\n\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush();
            }
        }

        [SetUp]
        public void SetUp()
        {
            FunctionScheduler.ClearScheduledCalls();
            EasterAdSdkClient.ResetMobilePresentationStateForTests();
            componentManagerOverride?.Dispose();
            componentManagerOverride = null;
            originalMainCamera = InstanceManager.CameraManager.GetMainCamera();
            originalLogEnable = InstanceManager.DebugLogger.LogEnable;
            originalDebugLogs = new List<string>(InstanceManager.DebugLogger.DebugLogs);
            originalDebugMeshes = new List<RuntimeUI.DebugMesh>(RuntimeUI.DebugMeshes);
            mobileItemClients = new List<ItemClient>();
            mobileItemObjects = new List<UnityEngine.GameObject>();
        }

        [Test]
        public void ServingHttpRoutesComeFromPinnedGeneratedContractsWithoutSessionUpdate()
        {
            Assert.That(ServingHttpBindings.SessionService.CreateSession.Method, Is.EqualTo("POST"));
            Assert.That(ServingHttpBindings.SessionService.CreateSession.Path, Is.EqualTo("/api/ad/session"));
            Assert.That(ServingHttpBindings.SessionService.DeleteSession.Method, Is.EqualTo("DELETE"));
            Assert.That(ServingHttpBindings.AdService.RequestAd.Path, Is.EqualTo("/api/ad/ad"));
            Assert.That(ServingHttpBindings.AdService.ReportImpression.Path,
                Is.EqualTo("/api/ad/ad/impression"));
            Assert.That(ServingHttpBindings.DisplayLogService.CreateDisplayLogs.Path,
                Is.EqualTo("/api/ad/logs/display"));
            Assert.That(typeof(ServingHttpBindings.SessionService)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(field => field.Name),
                Does.Not.Contain("UpdateSession"));
        }

        [TestCase(SystemLanguage.ChineseSimplified, "zh")]
        [TestCase(SystemLanguage.ChineseTraditional, "zh")]
        [TestCase(SystemLanguage.Unknown, "")]
        public void UnitySystemLanguageUsesAlpha2OrIsOmitted(SystemLanguage language, string expected)
        {
            Assert.That(EasterAd_Dependencies.Unity.SystemInfo.LanguageCode(language), Is.EqualTo(expected));
        }

        [Test]
        public void SessionRequestUsesRequiredNativeAppAndStableDeviceContext()
        {
            var info = new MockSessionSystemInfo
            {
                ApplicationIdentifier = "  com.easterad.fixture  ",
                ApplicationVersion = "  2.1.0  "
            };

            CreateSessionRequest request = EasterAdSdkClient.BuildSessionRequest(
                "111111111111111111111111", "  fixture-sdk-key  ", info);

            Assert.That(request.AppId, Is.EqualTo("111111111111111111111111"));
            Assert.That(request.SdkKey, Is.EqualTo("fixture-sdk-key"));
            Assert.That(request.HasSdkKey, Is.True);
            Assert.That(CreateSessionRequest.Parser.ParseFrom(request.ToByteArray()).HasSdkKey,
                Is.True);
            Assert.That(request.SdkVersion, Is.EqualTo(EasterAdSdkClient.Version));
            Assert.That(request.DistributionChannelCase,
                Is.EqualTo(CreateSessionRequest.DistributionChannelOneofCase.App));
            Assert.That(request.Site, Is.Null);
            Assert.That(request.App.Bundle, Is.EqualTo("com.easterad.fixture"));
            Assert.That(request.App.Version, Is.EqualTo("2.1.0"));
            Assert.That(request.Device.Type, Is.EqualTo(ContractDeviceType.PersonalComputer));
            Assert.That(request.Device.Os, Is.EqualTo(ContractOperatingSystem.Windows));
            Assert.That(request.Device.Model, Is.EqualTo("Fixture PC"));
            Assert.That(request.Device.Osv, Is.EqualTo("11"));
            Assert.That(request.Device.W, Is.EqualTo(1920));
            Assert.That(request.Device.H, Is.EqualTo(1080));
            Assert.That(request.Device.Ppi, Is.EqualTo(96));
        }

        [TestCase("EN", "Lang", "en")]
        [TestCase("fil", "Langb", "fil")]
        [TestCase("ENG", "Langb", "eng")]
        [TestCase("zh-Hant-TW", "Langb", "zh-Hant-TW")]
        [TestCase("", "None", "")]
        [TestCase("Unknown", "None", "")]
        [TestCase("en_US", "None", "")]
        public void SessionLanguageOneofRejectsUnknownOrMalformedValues(string language,
            string expectedCase, string expectedValue)
        {
            var info = new MockSessionSystemInfo { Language = language };
            ContractDevice device = EasterAdSdkClient.BuildSessionRequest(
                "111111111111111111111111", "", info).Device;

            Assert.That(device.LanguageCase.ToString(), Is.EqualTo(expectedCase));
            string actual = device.LanguageCase == ContractDevice.LanguageOneofCase.Lang
                ? device.Lang
                : device.LanguageCase == ContractDevice.LanguageOneofCase.Langb
                    ? device.Langb
                    : "";
            Assert.That(actual, Is.EqualTo(expectedValue));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptySdkKeyIsOmittedForTestSessions(string sdkKey)
        {
            CreateSessionRequest request = EasterAdSdkClient.BuildSessionRequest(
                "111111111111111111111111", sdkKey, new MockSessionSystemInfo());

            Assert.That(request.HasSdkKey, Is.False);
            Assert.That(CreateSessionRequest.Parser.ParseFrom(request.ToByteArray()).HasSdkKey,
                Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void SessionRequestMapsEveryValidAdComDeviceType(int deviceType)
        {
            CreateSessionRequest request = EasterAdSdkClient.BuildSessionRequest(
                "111111111111111111111111", "", new MockSessionSystemInfo
                {
                    DeviceType = deviceType
                });

            Assert.That((int)request.Device.Type, Is.EqualTo(deviceType));
        }

        [TestCase("", "2.0.0", 1920, 1080, 96, 2)]
        [TestCase("com.easterad.fixture", "", 1920, 1080, 96, 2)]
        [TestCase("com.easterad.fixture", "2.0.0", -1, 1080, 96, 2)]
        [TestCase("com.easterad.fixture", "2.0.0", 1920, -1, 96, 2)]
        [TestCase("com.easterad.fixture", "2.0.0", 1920, 1080, -1, 2)]
        [TestCase("com.easterad.fixture", "2.0.0", 1920, 1080, 96, 99)]
        [TestCase("com.easterad.fixture", "2.0.0", 1920, 1080, 96, 0)]
        public void SessionRequestFailsClosedForMissingNativeAppOrInvalidDeviceContext(
            string bundle, string version, int width, int height, int ppi, int deviceType)
        {
            var info = new MockSessionSystemInfo
            {
                ApplicationIdentifier = bundle,
                ApplicationVersion = version,
                ScreenWidth = width,
                ScreenHeight = height,
                ScreenPpi = ppi,
                DeviceType = deviceType
            };

            Type expectedExceptionType = deviceType < 1 || deviceType > 8
                ? typeof(ArgumentOutOfRangeException)
                : typeof(InvalidOperationException);
            Exception error = null;
            try
            {
                EasterAdSdkClient.BuildSessionRequest(
                    "111111111111111111111111", "", info);
            }
            catch (Exception caught)
            {
                error = caught;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.GetType(), Is.EqualTo(expectedExceptionType));
        }

        [TestCase("")]
        [TestCase("not-an-object-id")]
        [TestCase("a5e10000000000000000000g")]
        public void SessionRequestRejectsMalformedAppIdentifiers(string appId)
        {
            Assert.Throws<InvalidOperationException>(() => EasterAdSdkClient.BuildSessionRequest(
                appId, "", new MockSessionSystemInfo()));
        }

        [TestCase(true, 204, "session=new", "session=new")]
        [TestCase(true, 204, "", "")]
        [TestCase(true, 200, "session=new", "")]
        [TestCase(false, 204, "session=new", "")]
        public void CreatedSessionComesOnlyFromSuccessfulNoContentSetCookie(bool success,
            int statusCode, string cookie, string expected)
        {
            Assert.That(EasterAdSdkClient.SelectCreatedSessionForTests(
                    success, (HttpStatusCode)statusCode, cookie),
                Is.EqualTo(expected));
        }

        [Test]
        public void SessionRecreationAdoptsBeforeDeletingAndPreservesStateOnFailuresOrRaces()
        {
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=existing");
            var operations = new List<string>();

            bool recreated = EasterAdSdkClient.TryRecreateSessionForTests(mobileSdkClient,
                () =>
                {
                    operations.Add("create");
                    return "session=replacement";
                },
                session =>
                {
                    operations.Add("delete:" + session);
                    return true;
                },
                out bool previousDeleteFailed);

            Assert.That(recreated, Is.True);
            Assert.That(previousDeleteFailed, Is.False);
            Assert.That(EasterAdSdkClient.GetSessionForTests(mobileSdkClient),
                Is.EqualTo("session=replacement"));
            CollectionAssert.AreEqual(new[] { "create", "delete:session=existing" }, operations);

            operations.Clear();
            Assert.That(EasterAdSdkClient.TryRecreateSessionForTests(mobileSdkClient,
                () => "", session =>
                {
                    operations.Add(session);
                    return true;
                }, out _), Is.False);
            Assert.That(EasterAdSdkClient.GetSessionForTests(mobileSdkClient),
                Is.EqualTo("session=replacement"));
            Assert.That(operations, Is.Empty);

            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=before-race");
            operations.Clear();
            Assert.That(EasterAdSdkClient.TryRecreateSessionForTests(mobileSdkClient,
                () =>
                {
                    EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=concurrent");
                    return "session=orphan";
                }, session =>
                {
                    operations.Add(session);
                    return true;
                }, out _), Is.False);
            Assert.That(EasterAdSdkClient.GetSessionForTests(mobileSdkClient),
                Is.EqualTo("session=concurrent"));
            CollectionAssert.AreEqual(new[] { "session=orphan" }, operations);

            operations.Clear();
            Assert.That(EasterAdSdkClient.TryRecreateSessionForTests(mobileSdkClient,
                () =>
                {
                    EasterAdSdkClient.SetSessionForTests(
                        mobileSdkClient, "session=already-adopted");
                    return "session=already-adopted";
                }, session =>
                {
                    operations.Add(session);
                    return true;
                }, out _), Is.False);
            Assert.That(EasterAdSdkClient.GetSessionForTests(mobileSdkClient),
                Is.EqualTo("session=already-adopted"));
            Assert.That(operations, Is.Empty,
                "A CAS loser must not delete a replacement that another operation already adopted.");
        }

        [TestCase("application/proto", true)]
        [TestCase("Application/Proto; charset=binary", true)]
        [TestCase("application/json", false)]
        [TestCase("text/plain", false)]
        [TestCase("", false)]
        public void SyncTransportAcceptsOnlyProtobufResponseMime(string contentType, bool expected)
        {
            Assert.That(EasterAdSdkClient.IsProtobufContentTypeForTests(contentType),
                Is.EqualTo(expected));
        }

        [Test]
        public void SyncTransportDisablesRedirectsBoundsIoAndCarriesOnlyTheStartingCookie()
        {
            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:1/proto");

            EasterAdSdkClient.ConfigureTransportRequestForTests(
                request, "POST", 4321, "session=starting");

            Assert.That(request.AllowAutoRedirect, Is.False);
            Assert.That(request.Timeout, Is.EqualTo(4321));
            Assert.That(request.ReadWriteTimeout, Is.EqualTo(4321));
            Assert.That(request.Accept, Is.EqualTo("application/proto"));
            Assert.That(request.Headers["Cookie"], Is.EqualTo("session=starting"));
        }

        [Test]
        public void SyncTransportRejectsProtobufBodiesAboveTheExactResponseLimit()
        {
            int responseLimit = EasterAdSdkClient.MaxProtobufResponseBytesForTests;
            byte[] maximum = new byte[responseLimit];
            byte[] oversized = new byte[responseLimit + 1];

            Assert.That(EasterAdSdkClient.TryReadBoundedProtobufResponseForTests(
                new MemoryStream(maximum), out byte[] accepted), Is.True);
            Assert.That(accepted.Length, Is.EqualTo(responseLimit));
            Assert.That(EasterAdSdkClient.TryReadBoundedProtobufResponseForTests(
                new MemoryStream(oversized), out byte[] rejected), Is.False);
            Assert.That(rejected, Is.Empty);
        }

        [Test]
        public void IntrinsicRequestUsesOnlyCurrentContractFieldsAndParserRejectsMalformedBody()
        {
            var manager = new ProtobufCaptureComponentManager
            {
                ResponseBody = new ContractAdResponse
                {
                    Id = "444444444444444444444444",
                    Url = "https://fixtures.invalid/ad.png",
                    Mime = "image/png",
                    Width = 2,
                    Height = 2,
                    AspectRatio = 1
                }.ToByteArray(),
                UpdatedSession = "session=rotated"
            };
            ReplaceComponentManager(manager);
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (EasterAdSdkClient.OverrideComponentManagerForTests(
                           new CountingComponentManager()))
                {
                }
            });
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=original");
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            var request = new ContractAdRequest
            {
                AdUnitId = "222222222222222222222222",
                Width = 2,
                Height = 2,
                Interactable = true
            };
            ContractAdResponse parsed = null;
            string error = null;
            string callbackSession = null;

            EasterAdSdkClient.RequestContractAdForTests(
                request, new DependencyGameObject(unityObject), () => true,
                (response, requestError, _, _, session) =>
                {
                    parsed = response;
                    error = requestError;
                    callbackSession = session;
                });

            Assert.That(parsed, Is.Not.Null);
            Assert.That(error, Is.Empty);
            Assert.That(callbackSession, Is.EqualTo("session=rotated"));
            Assert.That(manager.LastMethod, Is.EqualTo("POST"));
            Assert.That(manager.LastUrl, Does.EndWith("/api/ad/ad"));
            Assert.That(manager.LastSession, Is.EqualTo("session=original"));
            Assert.That(manager.LegacyRequestCount, Is.Zero);
            CollectionAssert.AreEqual(
                new[] { "ad_unit_id", "width", "height", "interactable" },
                ContractAdRequest.Descriptor.Fields.InDeclarationOrder().Select(field => field.Name));

            manager.ResponseBody = new byte[] { 0xff };
            manager.UpdatedSession = "session=must-not-be-adopted";
            parsed = null;
            error = null;
            callbackSession = null;
            EasterAdSdkClient.RequestContractAdForTests(
                request, new DependencyGameObject(unityObject), () => true,
                (response, requestError, _, _, session) =>
                {
                    parsed = response;
                    error = requestError;
                    callbackSession = session;
                });

            Assert.That(parsed, Is.Null);
            Assert.That(error, Is.EqualTo("InvalidServerResponse"));
            Assert.That(callbackSession, Is.EqualTo("session=original"));
        }

        [Test]
        public void LateImageCompletionCannotRollbackTheNewestValidatedResponseSession()
        {
            var manager = new DeferredImageProtobufComponentManager();
            ReplaceComponentManager(manager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=original");
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            UnityEngine.GameObject secondObject =
                UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            mobileItemObjects.Add(secondObject);
            ItemClient first = new PlaneClient(new DependencyGameObject(unityObject),
                "222222222222222222222222");
            ItemClient second = new PlaneClient(new DependencyGameObject(secondObject),
                "333333333333333333333333");

            FunctionScheduler.FuncCall(ref first, "Load");
            FunctionScheduler.FuncCall(ref second, "Load");

            CollectionAssert.AreEqual(new[] { "session=original", "session=first" },
                manager.RequestSessions);
            Assert.That(EasterAdSdkClient.GetSessionForTests(mobileSdkClient),
                Is.EqualTo("session=second"));

            manager.CompleteImage(1);
            manager.CompleteImage(0);

            Assert.That(second.GetStatus(), Is.EqualTo(ItemStatus.Loaded));
            Assert.That(first.GetStatus(), Is.EqualTo(ItemStatus.Loaded));
            Assert.That(EasterAdSdkClient.GetSessionForTests(mobileSdkClient),
                Is.EqualTo("session=second"),
                "An older image completion must never re-adopt its response cookie.");
        }

        [Test]
        public void OutOfOrderAdAndLogCookiesUseCompareAndSwapAgainstTheirStartingSession()
        {
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=starting");

            Assert.That(EasterAdSdkClient.TryAdoptSessionForTests(
                mobileSdkClient, "session=starting", "session=log-new"),
                Is.True);
            Assert.That(EasterAdSdkClient.TryAdoptSessionForTests(
                mobileSdkClient, "session=starting", "session=ad-late"),
                Is.False);
            Assert.That(EasterAdSdkClient.TryAdoptSessionForTests(
                mobileSdkClient, "session=log-new", "session=display-newest"),
                Is.True);
            Assert.That(EasterAdSdkClient.TryAdoptSessionForTests(
                mobileSdkClient, "session=log-new", "session=impression-late"),
                Is.False);
            Assert.That(EasterAdSdkClient.GetSessionForTests(mobileSdkClient),
                Is.EqualTo("session=display-newest"));
        }

        [UnityTest]
        public IEnumerator ProtobufTransportClassifiesStatusMimeAndCookieFailClosed()
        {
            yield return VerifyProtobufTransport(200, "application/proto; charset=binary",
                new ContractAdResponse().ToByteArray(), "session=valid",
                "", false, "session=valid");
            yield return VerifyProtobufTransport(204, "", Array.Empty<byte>(), "session=nofill",
                "NoFill", false, "session=nofill");
            yield return VerifyProtobufTransport(401, "application/proto", Array.Empty<byte>(),
                "session=untrusted", "InvalidServerResponse", false, "session=original");
            yield return VerifyProtobufTransport(404, "application/proto", Array.Empty<byte>(),
                "session=untrusted", "InvalidServerResponse", false, "session=original");
            yield return VerifyProtobufTransport(302, "application/proto", Array.Empty<byte>(),
                "session=untrusted", "InvalidServerResponse", false, "session=original");
            yield return VerifyProtobufTransport(408, "application/proto", Array.Empty<byte>(),
                "session=untrusted", "NetworkError", true, "session=original");
            yield return VerifyProtobufTransport(429, "application/proto", Array.Empty<byte>(),
                "session=untrusted", "NetworkError", true, "session=original");
            yield return VerifyProtobufTransport(503, "application/proto", Array.Empty<byte>(),
                "session=untrusted", "NetworkError", true, "session=original");
            yield return VerifyProtobufTransport(200, "text/plain", new byte[] { 0x00 },
                "session=untrusted", "InvalidServerResponse", false, "session=original");
            yield return VerifyProtobufTransport(201, "application/proto", new byte[] { 0x00 },
                "session=untrusted", "InvalidServerResponse", false, "session=original");
        }

        [UnityTest]
        public IEnumerator ProtobufImageTransportRetriesOnlyAllowlistedHttpFailures()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            var manager = new UnityComponentManager();
            int[] statuses = { 404, 408, 429, 503 };
            foreach (int status in statuses)
            {
                using var server = new SingleResponseServer(status, "image/png", Array.Empty<byte>());
                string error = null;
                bool retryable = false;
                int callbackCount = 0;
                manager.LoadImage(server.Url, "image/png", 2, 2,
                    new DependencyGameObject(unityObject), () => true,
                    (resultError, resultRetryable) =>
                    {
                        callbackCount++;
                        error = resultError;
                        retryable = resultRetryable;
                    });

                float deadline = Time.realtimeSinceStartup + 8f;
                while (callbackCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(callbackCount, Is.EqualTo(1));
                bool expectedRetryable = status == 408 || status == 429 || status >= 500;
                Assert.That(retryable, Is.EqualTo(expectedRetryable), "HTTP " + status);
                Assert.That(error, Is.EqualTo(expectedRetryable
                    ? "ImageLoadError"
                    : "InvalidServerResponse"));
                server.ThrowIfFaulted();
            }
        }

        [Test]
        public void FailedRelativeAngleStillSerializesExactThreeComponentVectors()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            ItemClient client = new PlaneClient(
                new DependencyGameObject(unityObject), "222222222222222222222222");
            EasterAdSdkClient.SeedImpressionItemForTests(client,
                "444444444444444444444444", 0, 0, 0);
            DisplayLogEntry entry = EasterAdSdkClient.CreateRelativeAngleDisplayLogForTests(
                client, false, new[] { 1f, 2f, 3f }, 70,
                new[] { 4f, 5f, 6f }, 80);

            Assert.That(entry.IsViewable, Is.False);
            Assert.That(entry.ReasonOfUnviewable[
                EasterAdSdkClient.ImpressionReasonIndexForTests("RelativeAngle")], Is.True);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, entry.UpVector);
            CollectionAssert.AreEqual(new[] { 4f, 5f, 6f }, entry.WorldspaceNormal);
        }

        [Test]
        public void ImpressionReasonsUseTheEnumDomainRegardlessOfSubsetOrOrder()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            ItemClient client = new PlaneClient(
                new DependencyGameObject(unityObject), "222222222222222222222222");
            EasterAdSdkClient.SeedImpressionItemForTests(client,
                "444444444444444444444444", 0, 0, 0);
            DisplayLogEntry entry = EasterAdSdkClient.CreateReasonDisplayLogForTests(client,
                new[] { "TimeRequirement", "IdlePeriod", "RelativeAngle" },
                new[] { false, true, false });

            Assert.That(entry.ReasonOfUnviewable.Count,
                Is.EqualTo(EasterAdSdkClient.ImpressionReasonDomainSizeForTests));
            Assert.That(entry.ReasonOfUnviewable[
                EasterAdSdkClient.ImpressionReasonIndexForTests("TimeRequirement")], Is.True);
            Assert.That(entry.ReasonOfUnviewable[
                EasterAdSdkClient.ImpressionReasonIndexForTests("RelativeAngle")], Is.True);
            Assert.That(entry.ReasonOfUnviewable[
                EasterAdSdkClient.ImpressionReasonIndexForTests("IdlePeriod")], Is.False);
        }

        [Test]
        public void ImpressionResultNormalizesDimensionsAndStatsOrDropsInvalidWireValues()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            ItemClient client = new PlaneClient(
                new DependencyGameObject(unityObject), "222222222222222222222222");
            DateTime endedAt = DateTime.UtcNow;
            Assert.That(EasterAdSdkClient.TryCreateImpressionResultForTests(client,
                "444444444444444444444444", endedAt, -2, -3, 1.5f, 55f, 0.25f,
                out ImpressionRequest request), Is.True);
            Assert.That(request.Width, Is.EqualTo(2));
            Assert.That(request.Height, Is.EqualTo(3));
            Assert.That(request.Stats.AvgSize, Is.EqualTo(1));
            Assert.That(request.Stats.AvgAngle, Is.EqualTo(55));
            Assert.That(request.Stats.AvgOcclusion, Is.EqualTo(0.25));

            Assert.That(EasterAdSdkClient.TryCreateImpressionResultForTests(client,
                "444444444444444444444444", endedAt, 0, 3, 1.5f, 55f, 0.25f,
                out _), Is.False);
            Assert.That(EasterAdSdkClient.TryCreateImpressionResultForTests(client,
                "444444444444444444444444", endedAt, 1, 3, 1.5f, 55f, 1f,
                out _), Is.False);
            Assert.That(EasterAdSdkClient.TryCreateImpressionResultForTests(client,
                "444444444444444444444444", endedAt, 1, 3, 1.5f, float.NaN, 0f,
                out _), Is.False);
        }

        [Test]
        public void SameAdUnitItemsKeepIndependentFillStatisticsAndFinalizeExactlyOnce()
        {
            var results = new List<ImpressionRequest>();
            object controller = EasterAdSdkClient.CreateImpressionControllerForTests(
                _ => { }, value => results.Add((ImpressionRequest)value));
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            UnityEngine.GameObject secondObject =
                UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            mobileItemObjects.Add(secondObject);
            ItemClient first = CreateImpressionFixtureClient(unityObject,
                "222222222222222222222222", "444444444444444444444444", 0.25f);
            ItemClient second = CreateImpressionFixtureClient(secondObject,
                "222222222222222222222222", "555555555555555555555555", 0.75f);

            EasterAdSdkClient.RecordPassedImpressionForTests(controller, first);
            EasterAdSdkClient.RecordPassedImpressionForTests(controller, second);

            Assert.That(EasterAdSdkClient.ActiveImpressionCountForTests(controller), Is.EqualTo(2));
            Assert.That(EasterAdSdkClient.FinalizeImpressionForTests(controller, second), Is.True);
            Assert.That(EasterAdSdkClient.FinalizeImpressionForTests(controller, first), Is.True);
            Assert.That(EasterAdSdkClient.FinalizeImpressionForTests(controller, first), Is.False);
            Assert.That(EasterAdSdkClient.ActiveImpressionCountForTests(controller), Is.Zero);
            Assert.That(results.Select(result => result.ScheduleId),
                Is.EquivalentTo(new[]
                {
                    "444444444444444444444444",
                    "555555555555555555555555"
                }));
            Assert.That(results.Single(result =>
                    result.ScheduleId == "444444444444444444444444").Stats.AvgSize,
                Is.EqualTo(0.25).Within(0.000001));
            Assert.That(results.Single(result =>
                    result.ScheduleId == "555555555555555555555555").Stats.AvgSize,
                Is.EqualTo(0.75).Within(0.000001));
        }

        [Test]
        public void DestroyAndQuitFinalizeActiveImpressionsBeforeCleanupExactlyOnce()
        {
            var results = new List<ImpressionRequest>();
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, value => results.Add((ImpressionRequest)value),
                DateTime.MinValue, out mobileSdkClient);
            ItemClient destroyed = CreateFirstPartyItem(mobileSdkClient, "destroy-active-impression");
            EasterAdSdkClient.SeedImpressionItemForTests(destroyed,
                "444444444444444444444444", 0.5f, 45f, 0.1f);
            SetItemStatus(destroyed, ItemStatus.Loaded);
            object controller = EasterAdSdkClient.GetActiveImpressionControllerForTests();
            EasterAdSdkClient.RecordPassedImpressionForTests(controller, destroyed);

            mobileSdkClient.DestroyItem(destroyed);
            mobileSdkClient.DestroyItem(destroyed);

            Assert.That(destroyed.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(EasterAdSdkClient.ActiveImpressionCountForTests(controller), Is.Zero);
            Assert.That(results.Count, Is.EqualTo(1));

            ItemClient quitting = CreateFirstPartyItem(mobileSdkClient, "quit-active-impression",
                "333333333333333333333333");
            EasterAdSdkClient.SeedImpressionItemForTests(quitting,
                "555555555555555555555555", 0.6f, 50f, 0.2f);
            SetItemStatus(quitting, ItemStatus.Loaded);
            EasterAdSdkClient.RecordPassedImpressionForTests(controller, quitting);

            mobileSdkClient.OnApplicationQuit();

            Assert.That(quitting.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(EasterAdSdkClient.ActiveImpressionCountForTests(controller), Is.Zero);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results.Select(result => result.ScheduleId),
                Is.EquivalentTo(new[]
                {
                    "444444444444444444444444",
                    "555555555555555555555555"
                }));
        }

        [Test]
        public void DisplayLogDropsMalformedAdUnitOrFillIdentifiers()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            ItemClient invalidFill = CreateImpressionFixtureClient(unityObject,
                "222222222222222222222222", "invalid-fill", 0.5f);
            Assert.That(EasterAdSdkClient.CanCreateDisplayLogForTests(invalidFill), Is.False);

            UnityEngine.GameObject secondObject =
                UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            mobileItemObjects.Add(secondObject);
            ItemClient invalidAdUnit = CreateImpressionFixtureClient(secondObject,
                "invalid-ad-unit", "444444444444444444444444", 0.5f);
            Assert.That(EasterAdSdkClient.CanCreateDisplayLogForTests(invalidAdUnit), Is.False);
        }

        [TestCase("Id")]
        [TestCase("Mime")]
        [TestCase("Width")]
        [TestCase("Height")]
        [TestCase("AspectRatio")]
        public void IntrinsicResponseRequiresExplicitPresenceForEveryRequiredScalar(string field)
        {
            ContractAdResponse response = CreateValidContractAdResponse();
            switch (field)
            {
                case "Id": response.ClearId(); break;
                case "Mime": response.ClearMime(); break;
                case "Width": response.ClearWidth(); break;
                case "Height": response.ClearHeight(); break;
                case "AspectRatio": response.ClearAspectRatio(); break;
                default: Assert.Fail("Unknown fixture field: " + field); break;
            }

            Assert.That(EasterAdSdkClient.ClassifyContractAdResponseForTests(response),
                Is.EqualTo("InvalidServerResponse"));
        }

        [Test]
        public void IntrinsicResponseAcceptsFiniteRoundedAspectButRejectsMalformedFillOrNonFiniteAspect()
        {
            ContractAdResponse response = CreateValidContractAdResponse();
            response.Width = 16;
            response.Height = 9;
            response.AspectRatio = 1.78;

            Assert.That(EasterAdSdkClient.ClassifyContractAdResponseForTests(response),
                Is.EqualTo("Loaded"));

            response.Id = "not-an-object-id";
            Assert.That(EasterAdSdkClient.ClassifyContractAdResponseForTests(response),
                Is.EqualTo("InvalidServerResponse"));
            response.Id = "444444444444444444444444";
            response.AspectRatio = double.NaN;
            Assert.That(EasterAdSdkClient.ClassifyContractAdResponseForTests(response),
                Is.EqualTo("InvalidServerResponse"));
        }

        [Test]
        public void AdLoadErrorsDistinguishExplicitNoFillUnsupportedAndInvalidResponses()
        {
            EasterAdSdkClient.ClassifyAdErrorForTests("NoFill", false, false,
                out string noFillOutcome, out _, out _);
            Assert.That(noFillOutcome, Is.EqualTo("NoFill"));
            EasterAdSdkClient.ClassifyAdErrorForTests("", false, false,
                out string emptyOutcome, out _, out _);
            Assert.That(emptyOutcome, Is.EqualTo("InvalidServerResponse"));
            EasterAdSdkClient.ClassifyAdErrorForTests("Unknown", false, false,
                out string unknownOutcome, out _, out _);
            Assert.That(unknownOutcome, Is.EqualTo("InvalidServerResponse"));
            EasterAdSdkClient.ClassifyAdErrorForTests("UnsupportedContent", false, true,
                out string unsupportedOutcome, out bool shouldRetry, out ItemStatus status);
            Assert.That(unsupportedOutcome, Is.EqualTo("UnsupportedContent"));
            Assert.That(shouldRetry, Is.False);
            Assert.That(status, Is.EqualTo(ItemStatus.NoFill));
        }

        [Test]
        public void InvalidIntrinsicSurfaceDimensionsFailBeforeTransport()
        {
            var componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            unityObject.transform.localScale = new Vector3(0, 1, 1);
            ItemClient client = new PlaneClient(
                new DependencyGameObject(unityObject), "222222222222222222222222");

            FunctionScheduler.FuncCall(ref client, "Load");

            Assert.That(componentManager.RequestCount, Is.Zero);
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
        }

        [Test]
        public void InvalidIntrinsicObjectIdFailsBeforeTransport()
        {
            var componentManager = new ProtobufCaptureComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            ItemClient client = new PlaneClient(new DependencyGameObject(unityObject), "invalid-id");

            FunctionScheduler.FuncCall(ref client, "Load");

            Assert.That(componentManager.ProtobufRequestCount, Is.Zero);
            Assert.That(componentManager.LegacyRequestCount, Is.Zero);
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
        }

        [Test]
        public void FirstPartyLoadWithoutSessionFailsDeterministicallyBeforeTypedOrLegacyTransport()
        {
            var protobufManager = new ProtobufCaptureComponentManager();
            ReplaceComponentManager(protobufManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "");
            string lastOutcome = null;
            string lastError = null;
            mobileSdkClient.AdRequestObserved += diagnostics =>
            {
                lastOutcome = diagnostics.Outcome;
                lastError = diagnostics.Error;
            };
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            ItemClient typedClient = new PlaneClient(new DependencyGameObject(unityObject),
                "222222222222222222222222");

            FunctionScheduler.FuncCall(ref typedClient, "Load");

            Assert.That(protobufManager.ProtobufRequestCount, Is.Zero);
            Assert.That(protobufManager.LegacyRequestCount, Is.Zero);
            Assert.That(typedClient.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(lastOutcome, Is.EqualTo("Error"));
            Assert.That(lastError, Is.EqualTo("SessionUnavailable"));

            var legacyManager = new CountingComponentManager();
            ReplaceComponentManager(legacyManager);
            UnityEngine.GameObject legacyObject =
                UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            mobileItemObjects.Add(legacyObject);
            ItemClient legacyClient = new PlaneClient(new DependencyGameObject(legacyObject),
                "333333333333333333333333");

            FunctionScheduler.FuncCall(ref legacyClient, "Load");

            Assert.That(legacyManager.RequestCount, Is.Zero);
            Assert.That(legacyClient.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(lastError, Is.EqualTo("SessionUnavailable"));
        }

        [UnityTest]
        public IEnumerator PackagedMaterialManagerRecreatesMaterialClearedDuringPlayerLoop()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            var renderer = unityObject.GetComponent<Renderer>();
            renderer.sharedMaterial = null;
            unityObject.AddComponent<EasterAd.MaterialManager>();
            assignedMaterial = renderer.sharedMaterial;

            yield return null;

            Assert.That(assignedMaterial, Is.Not.Null,
                "The packaged MaterialManager must assign its initial SDK material.");

            Object.Destroy(assignedMaterial);
            renderer.sharedMaterial = null;
            yield return null;

            assignedMaterial = renderer.sharedMaterial;
            Assert.That(assignedMaterial, Is.Not.Null,
                "The packaged MaterialManager must recover when another runtime component clears its material.");
            Assert.That(assignedMaterial.shader.name, Is.EqualTo("EasterAd/UnifiedShader"),
                "Material recovery must preserve the current EasterAd shader contract.");
        }

        [UnityTest]
        public IEnumerator AssignedAdTextureRendersExpectedCenterPixel()
        {
            Color32 expectedPixel = new Color32(17, 93, 211, 255);
            Func<Texture2D> acquireAdImage = () => CreateSolidTexture(expectedPixel);
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            var renderer = unityObject.GetComponent<Renderer>();
            renderer.sharedMaterial = null;
            unityObject.AddComponent<EasterAd.MaterialManager>();
            assignedMaterial = renderer.sharedMaterial;

            sourceTexture = acquireAdImage();
            ReplaceComponentManager(new MockImageComponentManager(renderer, sourceTexture));
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = new PlaneClient(new DependencyGameObject(unityObject),
                "222222222222222222222222");
            FunctionScheduler.FuncCall(ref client, "Load");
            assignedMaterial = renderer.sharedMaterial;

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loaded),
                "The mocked image response must complete the SDK ad-loading path.");
            Assert.That(assignedMaterial.mainTexture, Is.SameAs(sourceTexture),
                "The SDK loading path must assign the acquired ad image to the runtime material.");

            cameraObject = new UnityEngine.GameObject("EasterAd frame verification camera");
            testCamera = cameraObject.AddComponent<Camera>();
            testCamera.clearFlags = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = Color.black;
            testCamera.orthographic = true;
            testCamera.orthographicSize = 0.75f;
            testCamera.transform.position = new Vector3(0f, 0f, -2f);
            testRenderTexture = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32);
            testCamera.targetTexture = testRenderTexture;

            yield return null;

            assignedMaterial.SetVector("_ConstantData", new Vector3(1f, 1f, 0f));
            testCamera.Render();

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = testRenderTexture;
                readbackTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                readbackTexture.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                readbackTexture.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32 actualPixel = readbackTexture.GetPixel(16, 16);
            Assert.That(actualPixel, Is.EqualTo(expectedPixel),
                "The player-loop frame must preserve the assigned ad texture pixel at the rendered quad center.");
        }

        [UnityTest]
        public IEnumerator AdSegmentationManagerReadsExactPixelCountsFromGpuBuffer()
        {
            const uint expectedVisiblePixels = 4096;
            var manager = new AdSegmentationManager();

            try
            {
                const int itemInstanceId = 314159;
                int segmentationId = manager.RegisterAd(itemInstanceId);
                var pixelCounts = new uint[256];
                pixelCounts[segmentationId] = expectedVisiblePixels;

                typeof(AdSegmentationManager)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(field => field.FieldType == typeof(float))
                    .SetValue(manager, Time.time - 1f);

                yield return null;

                manager.UpdatePixelCounts(pixelCounts);

                Assert.That(manager.GetPixelCount(segmentationId), Is.EqualTo(expectedVisiblePixels),
                    "The GPU readback boundary must preserve the exact segmentation pixel count.");
                Assert.That(manager.GetScreenAreaRatio(segmentationId), Is.EqualTo(0.0625f).Within(0.000001f),
                    "The viewability area ratio must be derived from the exact 256x256 segmentation frame.");
            }
            finally
            {
                manager.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ViewConditionsProduceExpectedRuntimeLog()
        {
            registeredClientKey = "runtime-view-log";
            preExistingClientKey = "pre-existing-runtime-client";
            var capturedImpressionLogs = new List<DisplayLogEntry>();
            var capturedResultLogs = new List<ImpressionRequest>();
            EasterAdSdkClient preExistingSdkClient = EasterAdSdkClient.CreateClient(this);
            preExistingClientObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            ItemClient preExistingClient = new PlaneClient(
                new DependencyGameObject(preExistingClientObject), "111111111111111111111111");
            EasterAdSdkClient.SeedImpressionItemForTests(preExistingClient,
                "666666666666666666666666", 0, 0, 0);
            SetItemStatus(preExistingClient, ItemStatus.Loaded);
            preExistingClient.AllowImpression = false;
            originalPreExistingClient = preExistingSdkClient.GetItemClient(preExistingClientKey);
            if (originalPreExistingClient == null)
            {
                preExistingSdkClient.AddItemClient(preExistingClientKey, ref preExistingClient);
            }
            else
            {
                preExistingSdkClient.UpdateItemClient(preExistingClientKey, ref preExistingClient);
            }

            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            const string runtimeAdUnitId = "222222222222222222222222";
            ItemClient client = new PlaneClient(new DependencyGameObject(unityObject), runtimeAdUnitId);
            EasterAdSdkClient.SeedImpressionItemForTests(client,
                "444444444444444444444444", 0, 0, 0);
            SetItemStatus(client, ItemStatus.Loaded);

            InstanceManager.UI.AddDebugMesh(new DependencyGameObject(unityObject), new[] { 255, 0, 0, 128 });
            var expectedDebugMeshes = new List<RuntimeUI.DebugMesh>(RuntimeUI.DebugMeshes);

            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                value => capturedImpressionLogs.Add((DisplayLogEntry)value),
                value => capturedResultLogs.Add((ImpressionRequest)value),
                DateTime.Now, out EasterAdSdkClient sdkClient);
            sdkClient.AddItemClient(registeredClientKey, ref client);

            cameraObject = new UnityEngine.GameObject("EasterAd view-condition camera");
            testCamera = cameraObject.AddComponent<Camera>();
            testCamera.orthographic = true;
            testCamera.orthographicSize = 6f;
            testCamera.transform.position = new Vector3(0f, 5f, 0f);
            testCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
            InstanceManager.CameraManager.SetMainCamera(new DependencyGameObject(cameraObject).Camera);

            var segmentationManager = (AdSegmentationManager)InstanceManager.AdSegmentationManager;
            registeredSegmentationId = segmentationManager.RegisterAd(unityObject.GetInstanceID());
            uint[] pixelCounts = (uint[])typeof(AdSegmentationManager)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(uint[]))
                .GetValue(segmentationManager);
            pixelCounts[registeredSegmentationId] = segmentationManager.GetTotalScreenPixels();

            InstanceManager.DebugLogger.LogEnable = true;
            InstanceManager.DebugLogger.DebugLogs.Clear();
            for (int evaluation = 0; evaluation < 5; evaluation++)
            {
                sdkClient.ImpressionRoutine();
                yield return new WaitForSecondsRealtime(0.21f);
            }

            client.AllowImpression = false;
            sdkClient.ImpressionRoutine();

            impressionLoggingOverride.Dispose();
            impressionLoggingOverride = null;
            RestoreDebugMeshes(expectedDebugMeshes);

            Assert.That(InstanceManager.DebugLogger.DebugLogs,
                Is.EqualTo(new[] { "Impression passed: " + runtimeAdUnitId }),
                "An ad satisfying the runtime view conditions must reach the SDK impression logging boundary exactly once.");
            Assert.That(capturedImpressionLogs, Has.Count.EqualTo(6),
                "Each view-condition evaluation must use the injected logger instead of the process-wide HTTP log worker.");
            Assert.That(capturedResultLogs, Has.Count.EqualTo(1),
                "Ending a qualified view must invoke the injected impression-result logger exactly once.");
            Assert.That(capturedResultLogs[0].AdUnitId, Is.EqualTo(runtimeAdUnitId),
                "The emitted impression result must identify the viewed ad placement.");
            Assert.That(EasterAdSdkClient.CreateClient(this), Is.SameAs(preExistingSdkClient),
                "The logging override must restore the pre-existing SDK singleton object graph.");
            Assert.That(preExistingClient.GetStatus(), Is.EqualTo(ItemStatus.Loaded),
                "The isolated view test must not mutate a pre-existing loaded client.");
            Assert.That(RuntimeUI.DebugMeshes, Is.EqualTo(expectedDebugMeshes),
                "The isolated UI must preserve debug meshes that existed before the view test ran.");
        }

        [Test]
        public void PackagedDependencyWrapperTracksCameraComponentLifecycle()
        {
            unityObject = new UnityEngine.GameObject("EasterAd camera wrapper boundary");
            var wrapper = new DependencyGameObject(unityObject);

            Assert.That(wrapper.Camera, Is.Null,
                "A wrapped object without a Unity Camera must not expose a stale camera wrapper.");

            var unityCamera = unityObject.AddComponent<Camera>();
            Assert.That(wrapper.Camera, Is.Not.Null,
                "The wrapper must discover a Camera added after wrapper construction.");

            Object.DestroyImmediate(unityCamera);
            Assert.That(wrapper.Camera, Is.Null,
                "The wrapper must stop exposing a Camera after the Unity component is removed.");
        }

        [Test]
        public void PackagedDependencyTransformWritesThroughToUnityObject()
        {
            unityObject = new UnityEngine.GameObject("EasterAd transform wrapper boundary");
            var wrapper = new DependencyGameObject(unityObject);

            wrapper.Transform.X = 2.5f;
            wrapper.Transform.Y = -3.0f;
            wrapper.Transform.Z = 4.5f;
            wrapper.Transform.LocalScaleX = 1.5f;
            wrapper.Transform.LocalScaleY = 2.0f;
            wrapper.Transform.LocalScaleZ = 2.5f;

            Assert.That(unityObject.transform.position, Is.EqualTo(new Vector3(2.5f, -3.0f, 4.5f)),
                "Dependency transform position writes must update the wrapped Unity object.");
            Assert.That(unityObject.transform.localScale, Is.EqualTo(new Vector3(1.5f, 2.0f, 2.5f)),
                "Dependency transform scale writes must update the wrapped Unity object.");
        }
        [Test]
        public void NonInteractablePlaneClientRejectsInteractionWithoutStateTransition()
        {
            unityObject = new UnityEngine.GameObject("EasterAd interaction failure boundary");
            ItemClient client = new PlaneClient(new DependencyGameObject(unityObject), "non-interactable-runtime-test");

            FunctionScheduler.FuncCall(ref client, "StartInteraction", out string interactionUrl);

            Assert.That(interactionUrl, Is.Empty,
                "A non-interactable placement must not expose an interaction URL.");
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Ready),
                "Rejected interaction must leave the ad item in its prior ready state.");
        }

        [TestCase("Android", true)]
        [TestCase("iOS", true)]
        [TestCase("WindowsEditor", false)]
        [TestCase("AndroidEditor", false)]
        [TestCase("android", false)]
        public void MobileRoutingUsesOnlyExactActualRuntimePlatformCodes(string platform, bool expected)
        {
            using (EasterAdSdkClient.OverrideRuntimePlatformForTests(platform))
            {
                Assert.That(EasterAdSdkClient.CreateClient(this).UsesExternalMobileAds, Is.EqualTo(expected));
            }
        }

        [TestCase("https://ads.example/image.png", true)]
        [TestCase("http://ads.example/image.png", true)]
        [TestCase("/relative/image.png", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("data:image/png;base64,AAAA", false)]
        [TestCase("file:///tmp/image.png", false)]
        [TestCase("https://user:password@ads.example/image.png", false)]
        public void HttpUrlPolicyAllowsOnlyAbsoluteHttpUrlsWithoutUserInfo(string url, bool expected)
        {
            Assert.That(HttpUrlPolicy.IsAllowed(url), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidMobileFailureEnumIsNormalizedToInvalidResult()
        {
            global::EasterAd.EasterAdMobileAdResult result =
                global::EasterAd.EasterAdMobileAdResult.Failed(
                    (global::EasterAd.EasterAdMobileAdFailure)999);

            Assert.That(result.Outcome, Is.EqualTo(global::EasterAd.EasterAdMobileAdOutcome.Failed));
            Assert.That(result.Failure, Is.EqualTo(global::EasterAd.EasterAdMobileAdFailure.InvalidResult));
        }

        [Test]
        public void MobileRoutingIgnoresCustomTelemetryPlatformDuringInitialization()
        {
            EasterAdSdkClient sdkClient = CreateMobileSdk();

            Assert.That(sdkClient.UsesExternalMobileAds, Is.True);
            sdkClient.ReInitialize(false, 2, "Windows", "en");
            Assert.That(sdkClient.UsesExternalMobileAds, Is.True,
                "Custom telemetry platform values must never override actual Android routing.");
        }

        [TestCase("Android")]
        [TestCase("iOS")]
        public void MobileLifecycleSuppressesEveryFirstPartyHttpOperation(string runtimePlatform)
        {
            var observedOperations = new List<string>();
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests(runtimePlatform);
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);

            using (EasterAdSdkClient.ObserveHttpRequestsForTests(observedOperations.Add))
            {
                mobileSdkClient.Initialize("fixture-game", false, "fixture-sdk-key", 2, "Windows", "en");
                mobileSdkClient.Initialize("fixture-game", false, "fixture-sdk-key");
                mobileSdkClient.ReInitialize(false, 2, "Windows", "en");
                mobileSdkClient.ReInitialize(false);
                mobileSdkClient.ImpressionRoutine();
                EasterAdSdkClient.SeedPendingMobileShutdownStateForTests("sentinel-mobile-session");
                mobileSdkClient.OnApplicationQuit();
            }

            Assert.That(observedOperations, Is.Empty,
                "Mobile lifecycle entry points must not perform health, session, impression, or shutdown HTTP calls.");
        }

        [Test]
        public void WebGlLifecycleFailsClosedWithoutAnyAdOrFirstPartyHttpOperation()
        {
            var observedOperations = new List<string>();
            var diagnostics = new List<string>();
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WebGLPlayer");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            mobileSdkClient.AdRequestObserved += value =>
                diagnostics.Add(value.Outcome + ":" + value.Error);

            using (EasterAdSdkClient.ObserveHttpRequestsForTests(observedOperations.Add))
            {
                mobileSdkClient.Initialize("fixture-game", false, "fixture-sdk-key", 2, "Windows", "en");
                mobileSdkClient.Initialize("fixture-game", false, "fixture-sdk-key");
                mobileSdkClient.ReInitialize(false, 2, "Windows", "en");
                mobileSdkClient.ReInitialize(false);

                ItemClient itemClient = CreateMobileItem(mobileSdkClient, "webgl-unsupported");
                FunctionScheduler.FuncCall(ref itemClient, "Load");
                FunctionScheduler.FailFuncCall();
                mobileSdkClient.ImpressionRoutine();

                Assert.That(mobileSdkClient.UsesExternalMobileAds, Is.False);
                Assert.That(mobileSdkClient.CanRequestAds, Is.False);
                Assert.That(itemClient.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
                Assert.That(diagnostics, Is.EqualTo(new[] { "Skipped:UnsupportedPlatform" }));

                EasterAdSdkClient.SeedPendingMobileShutdownStateForTests("sentinel-webgl-session");
                mobileSdkClient.OnApplicationQuit();
            }

            Assert.That(provider.LoadAndShowCount, Is.Zero,
                "Unity WebGL must not enter the Android/iOS external-provider route.");
            Assert.That(componentManager.RequestCount, Is.Zero,
                "Unity WebGL must not enter first-party serving until a browser transport contract exists.");
            Assert.That(observedOperations, Is.Empty,
                "Unity WebGL lifecycle entry points must not perform session, ad, impression, or shutdown HTTP calls.");
        }

        [UnityTest]
        public IEnumerator ActualPlaneRendererStaysHiddenOnUnsupportedWebGlRuntime()
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WebGLPlayer");
            var observedOperations = new List<string>();
            UnityEngine.GameObject sdkObject = null;
            UnityEngine.GameObject itemObject = null;

            using (EasterAdSdkClient.ObserveHttpRequestsForTests(observedOperations.Add))
            {
                try
                {
                    sdkObject = new UnityEngine.GameObject("Actual EasterAdSdk WebGL unsupported lifecycle");
                    EasterAdSdk sdk = sdkObject.AddComponent<EasterAdSdk>();
                    Assert.That(sdk.UsesExternalMobileAds, Is.False);
                    Assert.That(sdk.SupportsAdsOnCurrentPlatform, Is.False);

                    itemObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
                    Renderer renderer = itemObject.GetComponent<Renderer>();
                    global::EasterAd.Plane item = itemObject.AddComponent<global::EasterAd.Plane>();
                    item.loadOnStart = false;
                    item.enableRefresh = true;
                    item.refreshTime = 0f;
                    item.InitializeWithAdUnitId("actual-webgl-unsupported-rendering");

                    Assert.That(item.IsInitialized, Is.True);
                    Assert.That(renderer.enabled, Is.False,
                        "Unsupported WebGL must hide the in-game renderer during initialization.");
                    item.SetRenderingVisible(true);
                    Assert.That(renderer.enabled, Is.False,
                        "The public rendering API must not reveal an unsupported WebGL ad surface.");

                    item.Load();
                    Assert.That(item.Client.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
                    SetItemStatus(item.Client, ItemStatus.Impressed);
                    yield return null;

                    Assert.That(provider.LoadAndShowCount, Is.Zero,
                        "Unsupported WebGL must not invoke the Android/iOS provider route.");
                    Assert.That(componentManager.RequestCount, Is.Zero,
                        "Unsupported WebGL must not enter first-party serving or refresh.");
                    Assert.That(renderer.enabled, Is.False,
                        "The Update loop must keep the unsupported WebGL surface hidden.");
                    Assert.That(observedOperations, Is.Empty);
                }
                finally
                {
                    if (itemObject != null) Object.DestroyImmediate(itemObject);
                    if (sdkObject != null) Object.DestroyImmediate(sdkObject);
                    EasterAdSdkClient.UnregisterMobileAdProvider(provider);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualPlaneRendererStaysHiddenAcrossAndroidLifecycleAndRefresh()
        {
            yield return VerifyActualMobileItemRendering("Android", false);
        }

        [UnityTest]
        public IEnumerator ActualCanvasGraphicStaysHiddenAcrossIosLifecycleAndRefresh()
        {
            yield return VerifyActualMobileItemRendering("iOS", true);
        }

        [Test]
        public void MissingMobileProviderFailsClosedWithoutFirstPartyRequest()
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "mobile-missing-provider");

            FunctionScheduler.FuncCall(ref client, "Load");

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(componentManager.RequestCount, Is.Zero,
                "Missing mobile providers must never fall back to first-party serving or rendering.");
        }

        [Test]
        public void ExternalMobileProviderDoesNotRequireAFirstPartySession()
        {
            var componentManager = new ProtobufCaptureComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "mobile-sessionless-provider");

            Assert.That(EasterAdSdkClient.GetSessionForTests(sdkClient), Is.Empty);
            FunctionScheduler.FuncCall(ref client, "Load");

            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
            Assert.That(componentManager.ProtobufRequestCount, Is.Zero);
            Assert.That(componentManager.LegacyRequestCount, Is.Zero);
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loading));
        }

        [Test]
        public void MobilePolicyGatesBlockProviderAndFirstPartyRequests()
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();

            sdkClient.SetOfflineMode(true);
            ItemClient offlineClient = CreateMobileItem(sdkClient, "mobile-offline-policy");
            FunctionScheduler.FuncCall(ref offlineClient, "Load");
            sdkClient.SetOfflineMode(false);

            sdkClient.SetAdRequestsEnabled(false);
            ItemClient killSwitchClient = CreateMobileItem(sdkClient, "mobile-kill-switch-policy");
            FunctionScheduler.FuncCall(ref killSwitchClient, "Load");
            sdkClient.SetAdRequestsEnabled(true);

            sdkClient.ConfigurePrivacy(new EasterAdPrivacyOptions { AdRequestsAllowed = false });
            ItemClient privacyClient = CreateMobileItem(sdkClient, "mobile-privacy-policy");
            FunctionScheduler.FuncCall(ref privacyClient, "Load");

            Assert.That(offlineClient.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(killSwitchClient.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(privacyClient.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(provider.LoadAndShowCount, Is.Zero);
            Assert.That(componentManager.RequestCount, Is.Zero);
        }

        [Test]
        public void SetChildDirectedPreservesBlockedConsentInBothDirections()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            sdkClient.ConfigurePrivacy(new EasterAdPrivacyOptions
            {
                AdRequestsAllowed = false,
                PersonalizedAdsAllowed = true,
                ConsentString = "blocked-consent",
                PrivacyRegion = "fixture-region"
            });

            sdkClient.SetChildDirected(true);
            ItemClient childDirectedClient = CreateMobileItem(sdkClient, "child-directed-blocked");
            FunctionScheduler.FuncCall(ref childDirectedClient, "Load");

            sdkClient.SetChildDirected(false);
            ItemClient generalAudienceClient = CreateMobileItem(sdkClient, "general-audience-still-blocked");
            FunctionScheduler.FuncCall(ref generalAudienceClient, "Load");

            Assert.That(sdkClient.CanRequestAds, Is.False);
            Assert.That(childDirectedClient.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(generalAudienceClient.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(provider.LoadAndShowCount, Is.Zero,
                "Changing only child-directed treatment must never reset a blocked consent decision.");
        }

        [Test]
        public void ConfigurePrivacySnapshotsCallerObjectUntilAnExplicitPolicyChange()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            var options = new EasterAdPrivacyOptions
            {
                AdRequestsAllowed = true,
                PersonalizedAdsAllowed = true,
                BlockedAdCategories = new List<string> { "blocked-original" },
                AllowedAdCategories = new List<string> { "allowed-original" }
            };
            sdkClient.ConfigurePrivacy(options);
            ItemClient client = CreateMobileItem(sdkClient, "privacy-snapshot-active");

            FunctionScheduler.FuncCall(ref client, "Load");
            MockMobileAdHandle handle = provider.LastHandle;
            options.AdRequestsAllowed = false;
            options.ChildDirected = true;
            options.BlockedAdCategories.Clear();
            options.BlockedAdCategories.Add("blocked-mutated");
            options.AllowedAdCategories.Clear();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loading));
            Assert.That(handle.DisposeCount, Is.Zero,
                "Mutating the caller-owned options object must not cancel the snapshotted active operation.");

            sdkClient.ConfigurePrivacy(new EasterAdPrivacyOptions { AdRequestsAllowed = false });

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(handle.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfigurePrivacyRemainsLocalAndDoesNotInventLegacyWireFields()
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            var options = new EasterAdPrivacyOptions
            {
                ChildDirected = true,
                PersonalizedAdsAllowed = true,
                BlockedAdCategories = new List<string> { "blocked-original" },
                AllowedAdCategories = new List<string> { "allowed-original" }
            };
            mobileSdkClient.ConfigurePrivacy(options);
            options.BlockedAdCategories[0] = "blocked-mutated";
            options.AllowedAdCategories.Clear();
            options.PersonalizedAdsAllowed = true;
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "privacy-request-snapshot");

            FunctionScheduler.FuncCall(ref client, "Load");

            Assert.That(componentManager.RequestCount, Is.EqualTo(1));
            StringAssert.DoesNotContain("blocked-original", componentManager.LastBody);
            StringAssert.DoesNotContain("allowed-original", componentManager.LastBody);
            StringAssert.DoesNotContain("blocked-mutated", componentManager.LastBody);
            StringAssert.DoesNotContain("personalizedAdsAllowed", componentManager.LastBody);
            StringAssert.DoesNotContain("childDirected", componentManager.LastBody);
            StringAssert.DoesNotContain("privacy", componentManager.LastBody.ToLowerInvariant());
        }

        [Test]
        public void PrivacyPolicyCannotChangeSessionOrIntrinsicProtobufBytes()
        {
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "privacy-protobuf-boundary");
            var systemInfo = new MockSessionSystemInfo();
            byte[] sessionBefore = EasterAdSdkClient.BuildSessionRequest(
                "111111111111111111111111", "fixture-key", systemInfo).ToByteArray();
            Assert.That(EasterAdSdkClient.TryCreateContractAdRequestForTests(
                client, out ContractAdRequest adBefore), Is.True);

            mobileSdkClient.ConfigurePrivacy(new EasterAdPrivacyOptions
            {
                AdRequestsAllowed = true,
                ChildDirected = true,
                PersonalizedAdsAllowed = true,
                ConsentString = "private-consent-marker",
                PrivacyRegion = "private-region-marker",
                BlockedAdCategories = new List<string> { "private-blocked-marker" },
                AllowedAdCategories = new List<string> { "private-allowed-marker" }
            });

            byte[] sessionAfter = EasterAdSdkClient.BuildSessionRequest(
                "111111111111111111111111", "fixture-key", systemInfo).ToByteArray();
            Assert.That(EasterAdSdkClient.TryCreateContractAdRequestForTests(
                client, out ContractAdRequest adAfter), Is.True);
            CollectionAssert.AreEqual(sessionBefore, sessionAfter);
            CollectionAssert.AreEqual(adBefore.ToByteArray(), adAfter.ToByteArray());
            string wireText = Encoding.UTF8.GetString(sessionAfter) +
                              Encoding.UTF8.GetString(adAfter.ToByteArray());
            StringAssert.DoesNotContain("private-consent-marker", wireText);
            StringAssert.DoesNotContain("private-region-marker", wireText);
            StringAssert.DoesNotContain("private-blocked-marker", wireText);
            StringAssert.DoesNotContain("private-allowed-marker", wireText);
            StringAssert.DoesNotContain("privacy", wireText.ToLowerInvariant());
        }

        [Test]
        public void SetPrivacyConsentEnforcesProviderGateWithoutInventingWireFields()
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            sdkClient.SetAdCategoryPolicy(new[] { "blocked-preserved" }, new[] { "allowed-preserved" });
            sdkClient.SetPrivacyConsent(false, true, "blocked-consent", "fixture-region", false);
            ItemClient blockedClient = CreateMobileItem(sdkClient, "consent-category-blocked");

            FunctionScheduler.FuncCall(ref blockedClient, "Load");

            Assert.That(blockedClient.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(provider.LoadAndShowCount, Is.Zero);

            sdkClient.SetPrivacyConsent(true, true, "allowed-consent", "fixture-region", true);
            runtimePlatformOverride.Dispose();
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            EasterAdSdkClient.SetSessionForTests(sdkClient, "session=fixture");
            ItemClient firstPartyClient = CreateFirstPartyItem(sdkClient, "consent-category-body");

            FunctionScheduler.FuncCall(ref firstPartyClient, "Load");

            Assert.That(componentManager.RequestCount, Is.EqualTo(1));
            StringAssert.DoesNotContain("blocked-preserved", componentManager.LastBody);
            StringAssert.DoesNotContain("allowed-preserved", componentManager.LastBody);
            StringAssert.DoesNotContain("allowed-consent", componentManager.LastBody);
            StringAssert.DoesNotContain("childDirected", componentManager.LastBody);
            StringAssert.DoesNotContain("personalizedAdsAllowed", componentManager.LastBody);
        }

        [TestCase("InvalidServerResponse", ItemStatus.Failed)]
        [TestCase("UnsupportedContent", ItemStatus.NoFill)]
        [TestCase("NoFill", ItemStatus.NoFill)]
        [TestCase("", ItemStatus.Failed)]
        [TestCase("Unknown", ItemStatus.Failed)]
        public void RetryableFlagCannotOverrideNonNetworkErrorType(string errorType, ItemStatus expectedStatus)
        {
            var componentManager = new DeferredComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "adversarial-retry-" + errorType);

            FunctionScheduler.FuncCall(ref client, "Load");
            componentManager.Complete(new Dictionary<string, object>
            {
                { "adError", "untrusted raw error" },
                { "adErrorType", errorType },
                { "retryable", true }
            });
            FunctionScheduler.FailFuncCall();

            Assert.That(client.GetStatus(), Is.EqualTo(expectedStatus));
            Assert.That(componentManager.RequestCount, Is.EqualTo(1),
                "Only an allowlisted network error may enter the retry queue.");
        }

        [Test]
        public void NetworkErrorTypeRetriesEvenWhenUntrustedFlagIsFalse()
        {
            var componentManager = new DeferredComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "allowlisted-network-retry");

            FunctionScheduler.FuncCall(ref client, "Load");
            componentManager.Complete(new Dictionary<string, object>
            {
                { "adError", "untrusted raw error" },
                { "adErrorType", "NetworkError" },
                { "retryable", false }
            });
            FunctionScheduler.FailFuncCall();

            Assert.That(componentManager.RequestCount, Is.EqualTo(2));
        }

        [Test]
        public void UnmarkedImageErrorNeverRetries()
        {
            var componentManager = new DeferredComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "unmarked-image-error");

            FunctionScheduler.FuncCall(ref client, "Load");
            componentManager.Complete(new Dictionary<string, object>
            {
                { "_id", "untrusted-image-error" },
                { "url", "https://fixtures.invalid/image.png" },
                { "mime", "image/png" },
                { "width", 2 },
                { "height", 2 },
                { "imageError", "ImageLoadError" },
                { "retryable", true }
            });
            FunctionScheduler.FailFuncCall();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(componentManager.RequestCount, Is.EqualTo(1));
        }

        [TestCase("offline")]
        [TestCase("kill-switch")]
        [TestCase("privacy")]
        public void ActiveMobileOperationIsCanceledWhenRequestPolicyBecomesBlocked(string policy)
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "active-policy-" + policy);

            FunctionScheduler.FuncCall(ref client, "Load");
            MockMobileAdHandle handle = provider.LastHandle;
            switch (policy)
            {
                case "offline":
                    sdkClient.SetOfflineMode(true);
                    break;
                case "kill-switch":
                    sdkClient.SetAdRequestsEnabled(false);
                    break;
                case "privacy":
                    sdkClient.ConfigurePrivacy(new EasterAdPrivacyOptions { AdRequestsAllowed = false });
                    break;
            }

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(handle.DisposeCount, Is.EqualTo(1));
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
            Assert.That(componentManager.RequestCount, Is.Zero);
            Assert.That(EasterAdSdkClient.UnregisterMobileAdProvider(provider), Is.True);

            provider.Complete(global::EasterAd.EasterAdMobileAdResult.Displayed());
            sdkClient.MobileAdRoutine();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Disabled));
            Assert.That(handle.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void MobileProviderCompletionIsMainThreadDrainedAndObserverExceptionsAreIsolated()
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "logical-plane-placement");
            int healthyObserverCalls = 0;
            sdkClient.AdRequestObserved += _ => throw new InvalidOperationException("observer fixture failure");
            sdkClient.AdRequestObserved += diagnostics =>
            {
                healthyObserverCalls++;
                Assert.That(diagnostics.Outcome, Is.EqualTo("Loaded"));
                Assert.That(diagnostics.Error, Is.Empty);
            };

            FunctionScheduler.FuncCall(ref client, "Load");

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loading));
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
            Assert.That(provider.LastRequest.PlacementKey, Is.EqualTo("logical-plane-placement"));
            Assert.That(provider.LastRequest.Surface, Is.EqualTo(global::EasterAd.EasterAdMobileAdSurface.Plane));
            Assert.That(componentManager.RequestCount, Is.Zero);
            Assert.That(EasterAdSdkClient.UnregisterMobileAdProvider(provider), Is.False,
                "An active provider lease must make unregistration fail without canceling the operation.");

            Task.Run(() => provider.Complete(global::EasterAd.EasterAdMobileAdResult.Displayed()))
                .GetAwaiter().GetResult();
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loading),
                "Provider threads must not mutate Unity-facing state before the main-thread routine drains them.");

            sdkClient.MobileAdRoutine();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loaded));
            Assert.That(provider.LastHandle.DisposeCount, Is.EqualTo(1));
            Assert.That(healthyObserverCalls, Is.EqualTo(1),
                "A throwing observer must not prevent later observers from receiving the terminal result.");
            Assert.That(EasterAdSdkClient.UnregisterMobileAdProvider(provider), Is.True);

            provider.Complete(global::EasterAd.EasterAdMobileAdResult.NoFill());
            sdkClient.MobileAdRoutine();
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loaded),
                "Duplicate terminal callbacks must be ignored.");
            Assert.That(provider.LastHandle.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void MobilePresentationLeaseRejectsSecondItemWithoutDisturbingFirst()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient firstClient = CreateMobileItem(sdkClient, "first-canvas-placement", true);
            ItemClient secondClient = CreateMobileItem(sdkClient, "second-plane-placement");

            FunctionScheduler.FuncCall(ref firstClient, "Load");
            MockMobileAdHandle firstHandle = provider.LastHandle;
            FunctionScheduler.FuncCall(ref secondClient, "Load");

            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
            Assert.That(provider.LastRequest.Surface, Is.EqualTo(global::EasterAd.EasterAdMobileAdSurface.Canvas));
            Assert.That(firstClient.GetStatus(), Is.EqualTo(ItemStatus.Loading));
            Assert.That(secondClient.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(firstHandle.DisposeCount, Is.Zero,
                "Rejecting a busy second item must not dispose or replace the first operation handle.");

            provider.Complete(global::EasterAd.EasterAdMobileAdResult.Displayed());
            sdkClient.MobileAdRoutine();

            Assert.That(firstClient.GetStatus(), Is.EqualTo(ItemStatus.Loaded));
            Assert.That(firstHandle.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantCancelDuringProviderInvocationDefersLeaseReleaseUntilHandleCleanup()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient firstClient = CreateMobileItem(sdkClient, "reentrant-cancel-first");
            ItemClient secondClient = CreateMobileItem(sdkClient, "reentrant-cancel-second");
            ItemClient disposeReentryClient = CreateMobileItem(sdkClient, "dispose-reentry-second");
            int providerCallsDuringReentry = 0;
            int providerCallsDuringDispose = 0;
            ItemStatus secondStatusDuringReentry = ItemStatus.None;
            ItemStatus disposeReentryStatus = ItemStatus.None;
            bool unregisteredDuringInvocation = true;
            provider.DuringInvocation = () =>
            {
                unregisteredDuringInvocation = EasterAdSdkClient.UnregisterMobileAdProvider(provider);
                sdkClient.DestroyItem(firstClient);
                ItemClient secondReference = secondClient;
                FunctionScheduler.FuncCall(ref secondReference, "Load");
                providerCallsDuringReentry = provider.LoadAndShowCount;
                secondStatusDuringReentry = secondClient.GetStatus();
            };
            provider.DuringHandleDispose = () =>
            {
                ItemClient disposeReentryReference = disposeReentryClient;
                FunctionScheduler.FuncCall(ref disposeReentryReference, "Load");
                providerCallsDuringDispose = provider.LoadAndShowCount;
                disposeReentryStatus = disposeReentryClient.GetStatus();
            };

            FunctionScheduler.FuncCall(ref firstClient, "Load");

            Assert.That(firstClient.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(secondStatusDuringReentry, Is.EqualTo(ItemStatus.Failed));
            Assert.That(providerCallsDuringReentry, Is.EqualTo(1),
                "A reentrant load must not invoke the provider before the canceled invocation returns its handle.");
            Assert.That(unregisteredDuringInvocation, Is.False,
                "The provider lease must remain active throughout the provider invocation.");
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
            Assert.That(provider.LastHandle.DisposeCount, Is.EqualTo(1),
                "The returned handle must be disposed before the process-wide presentation lease is released.");
            Assert.That(providerCallsDuringDispose, Is.EqualTo(1),
                "The process-wide presentation lease must remain held until handle Dispose returns.");
            Assert.That(disposeReentryStatus, Is.EqualTo(ItemStatus.Failed));

            provider.DuringInvocation = null;
            provider.DuringHandleDispose = null;
            ItemClient thirdClient = CreateMobileItem(sdkClient, "post-cancel-third");
            FunctionScheduler.FuncCall(ref thirdClient, "Load");
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(2),
                "A successful deferred cleanup must release the presentation lease for a later request.");
        }

        [Test]
        public void CancellationDisposeFailureBlocksProviderAcrossSdkRecreation()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            provider.ThrowOnHandleDispose = true;
            EasterAdSdkClient firstSdkClient = CreateMobileSdk();
            ItemClient firstClient = CreateMobileItem(firstSdkClient, "dispose-fault-first");
            ItemClient disposeReentryClient = CreateMobileItem(firstSdkClient, "dispose-fault-reentry");
            int providerCallsDuringDispose = 0;
            ItemStatus disposeReentryStatus = ItemStatus.None;
            provider.DuringInvocation = () => firstSdkClient.DestroyItem(firstClient);
            provider.DuringHandleDispose = () =>
            {
                ItemClient disposeReentryReference = disposeReentryClient;
                FunctionScheduler.FuncCall(ref disposeReentryReference, "Load");
                providerCallsDuringDispose = provider.LoadAndShowCount;
                disposeReentryStatus = disposeReentryClient.GetStatus();
            };

            FunctionScheduler.FuncCall(ref firstClient, "Load");

            Assert.That(firstClient.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
            Assert.That(provider.LastHandle.DisposeCount, Is.EqualTo(1));
            Assert.That(providerCallsDuringDispose, Is.EqualTo(1),
                "A cleanup failure must not open a presentation overlap before the fault latch is set.");
            Assert.That(disposeReentryStatus, Is.EqualTo(ItemStatus.Failed));
            Assert.That(EasterAdSdkClient.UnregisterMobileAdProvider(provider), Is.False,
                "A failed cancellation cleanup must retain the provider lease.");

            firstSdkClient.OnApplicationQuit();
            provider.DuringInvocation = null;
            provider.DuringHandleDispose = null;
            mobileSdkClient = EasterAdSdkClient.CreateClient(this);
            mobileSdkClient.Initialize("fixture-game", false, "fixture-sdk-key", 2, "Windows", "en");
            ItemClient secondClient = CreateMobileItem(mobileSdkClient, "dispose-fault-recreated-sdk");

            FunctionScheduler.FuncCall(ref secondClient, "Load");

            Assert.That(secondClient.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1),
                "A process-wide cleanup fault must block provider invocation after SDK recreation.");
        }

        [Test]
        public void MobileNoFillAndFailureNeverFallBackOrScheduleRetry()
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "mobile-terminal-results");

            FunctionScheduler.FuncCall(ref client, "Load");
            provider.Complete(global::EasterAd.EasterAdMobileAdResult.NoFill());
            sdkClient.MobileAdRoutine();
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.NoFill));

            FunctionScheduler.FuncCall(ref client, "Load");
            provider.Complete(global::EasterAd.EasterAdMobileAdResult.Failed(
                global::EasterAd.EasterAdMobileAdFailure.ProviderRejected));
            sdkClient.MobileAdRoutine();
            FunctionScheduler.FailFuncCall();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(2));
            Assert.That(componentManager.RequestCount, Is.Zero);
        }

        [Test]
        public void SynchronousSuccessFollowedByNullHandleIsForcedToInvalidResultFailure()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            provider.SynchronousResult = global::EasterAd.EasterAdMobileAdResult.Displayed();
            provider.ReturnNullHandle = true;
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            provider.AfterSynchronousCompletion = sdkClient.MobileAdRoutine;
            ItemClient client = CreateMobileItem(sdkClient, "sync-null-handle");
            string observedError = "";
            sdkClient.AdRequestObserved += diagnostics => observedError = diagnostics.Error;

            FunctionScheduler.FuncCall(ref client, "Load");
            sdkClient.MobileAdRoutine();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(observedError, Is.EqualTo(global::EasterAd.EasterAdMobileAdFailure.InvalidResult.ToString()));
        }

        [Test]
        public void SynchronousSuccessFollowedByProviderThrowIsForcedToProviderError()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            provider.SynchronousResult = global::EasterAd.EasterAdMobileAdResult.Displayed();
            provider.ThrowAfterSynchronousCompletion = true;
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            provider.AfterSynchronousCompletion = sdkClient.MobileAdRoutine;
            ItemClient client = CreateMobileItem(sdkClient, "sync-provider-throw");
            string observedError = "";
            sdkClient.AdRequestObserved += diagnostics => observedError = diagnostics.Error;

            FunctionScheduler.FuncCall(ref client, "Load");
            sdkClient.MobileAdRoutine();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(observedError, Is.EqualTo(global::EasterAd.EasterAdMobileAdFailure.ProviderError.ToString()));
        }

        [Test]
        public void DestroyedMobileItemDisposesHandleAndIgnoresLateCompletionAndRetry()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "destroyed-mobile-item");

            FunctionScheduler.FuncCall(ref client, "Load");
            MockMobileAdHandle handle = provider.LastHandle;
            sdkClient.DestroyItem(client);

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(handle.DisposeCount, Is.EqualTo(1));
            Assert.That(EasterAdSdkClient.UnregisterMobileAdProvider(provider), Is.True,
                "Destroy must release the provider lease after disposing the per-request handle.");

            provider.Complete(global::EasterAd.EasterAdMobileAdResult.Displayed());
            sdkClient.MobileAdRoutine();
            FunctionScheduler.FuncCall(ref client, "Load");
            FunctionScheduler.FailFuncCall();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
            Assert.That(handle.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void MobileSdkShutdownCancelsActiveOperationExactlyOnceAndIgnoresLateCompletion()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "mobile-sdk-shutdown");

            FunctionScheduler.FuncCall(ref client, "Load");
            MockMobileAdHandle handle = provider.LastHandle;
            sdkClient.OnApplicationQuit();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(handle.DisposeCount, Is.EqualTo(1));
            Assert.That(EasterAdSdkClient.UnregisterMobileAdProvider(provider), Is.True);

            provider.Complete(global::EasterAd.EasterAdMobileAdResult.Displayed());
            sdkClient.MobileAdRoutine();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
            Assert.That(handle.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void MobileImpressionRoutineNeverInvokesFirstPartyLogging()
        {
            int impressionLogCalls = 0;
            int impressionResultLogCalls = 0;
            EasterAdSdkClient sdkClient = CreateMobileSdk(
                _ => impressionLogCalls++, _ => impressionResultLogCalls++);
            ItemClient client = CreateMobileItem(sdkClient, "mobile-impression-suppressed");
            SetItemStatus(client, ItemStatus.Loaded);

            for (int index = 0; index < 5; index++)
            {
                sdkClient.ImpressionRoutine();
            }

            Assert.That(impressionLogCalls, Is.Zero);
            Assert.That(impressionResultLogCalls, Is.Zero);
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loaded));
        }

        [Test]
        public void RetryScheduledForOlderLoadGenerationCannotRestartNewerResult()
        {
            RetryThenSuccessComponentManager componentManager = new RetryThenSuccessComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "stale-retry-generation");

            FunctionScheduler.FuncCall(ref client, "Load");
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));

            FunctionScheduler.FuncCall(ref client, "Load");
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loaded));

            FunctionScheduler.FailFuncCall();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Loaded));
            Assert.That(componentManager.RequestCount, Is.EqualTo(2),
                "A retry scheduled by an older failed generation must not restart a newer successful load.");
        }

        [Test]
        public void DestroyedItemIgnoresLateFirstPartyResponseBeforeSessionOrStateMutation()
        {
            DeferredComponentManager componentManager = new DeferredComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "late-first-party-response");

            FunctionScheduler.FuncCall(ref client, "Load");
            Assert.That(componentManager.RequestCount, Is.EqualTo(1));
            mobileSdkClient.DestroyItem(client);
            componentManager.Complete(new Dictionary<string, object>
            {
                { "session", "late-session-must-be-ignored" },
                { "_id", "late-fill" },
                { "url", "https://fixtures.invalid/late-image.png" },
                { "mime", "image/png" },
                { "width", 2 },
                { "height", 2 }
            });

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
        }

        [Test]
        public void FirstPartyLoadRejectsUnsafeImageUrlThroughPublicLoadPath()
        {
            DeferredComponentManager componentManager = new DeferredComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "unsafe-first-party-image");

            FunctionScheduler.FuncCall(ref client, "Load");
            componentManager.Complete(new Dictionary<string, object>
            {
                { "_id", "444444444444444444444444" },
                { "url", "javascript:alert(1)" },
                { "mime", "image/png" },
                { "width", 2 },
                { "height", 2 }
            });

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
        }

        [TestCase("image/svg+xml")]
        [TestCase("application/octet-stream")]
        public void FirstPartyLoadTreatsUnsupportedCreativeMimeAsNoFillWithoutRetry(string mime)
        {
            DeferredComponentManager componentManager = new DeferredComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "unsupported-first-party-mime");

            FunctionScheduler.FuncCall(ref client, "Load");
            componentManager.Complete(new Dictionary<string, object>
            {
                { "_id", "444444444444444444444444" },
                { "url", "https://fixtures.invalid/unsupported-creative" },
                { "mime", mime },
                { "width", 2 },
                { "height", 2 }
            });
            FunctionScheduler.FailFuncCall();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.NoFill));
            Assert.That(componentManager.RequestCount, Is.EqualTo(1),
                "Unsupported creative formats must not enter the retry queue.");
        }

        [Test]
        public void FirstPartyLoadRejectsOversizedDeclaredDimensionsWithoutRetry()
        {
            DeferredComponentManager componentManager = new DeferredComponentManager();
            ReplaceComponentManager(componentManager);
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
            EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
            ItemClient client = CreateFirstPartyItem(mobileSdkClient, "oversized-first-party-image");

            FunctionScheduler.FuncCall(ref client, "Load");
            componentManager.Complete(new Dictionary<string, object>
            {
                { "_id", "444444444444444444444444" },
                { "url", "https://fixtures.invalid/oversized-image.png" },
                { "mime", "image/png" },
                { "width", 8192 },
                { "height", 4096 }
            });
            FunctionScheduler.FailFuncCall();

            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Failed));
            Assert.That(componentManager.RequestCount, Is.EqualTo(1),
                "Invalid creative dimensions must never be retried.");
        }

        [UnityTest]
        public IEnumerator ComponentManagerCancelsLateImageBeforeTextureOrCallbackMutation()
        {
            unityObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            Renderer renderer = unityObject.GetComponent<Renderer>();
            assignedMaterial = renderer.material;
            sourceTexture = CreateSolidTexture(new Color32(33, 44, 55, 255));
            assignedMaterial.mainTexture = sourceTexture;
            bool isActive = true;
            int callbackCount = 0;
            var server = new BlockingImageServer();

            try
            {
                var componentManager = new UnityComponentManager();
                componentManager.RunRequest(
                    server.RootUrl,
                    server.AdRequestUrl,
                    string.Empty,
                    "{}",
                    new DependencyGameObject(unityObject),
                    () => isActive,
                    _ => callbackCount++);

                float deadline = Time.realtimeSinceStartup + 5f;
                while (!server.ImageRequested && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(server.ImageRequested, Is.True,
                    "The local fixture must reach the delayed image request before cancellation.");
                isActive = false;
                server.ReleaseImageResponse();

                yield return null;
                yield return null;
                yield return null;

                Assert.That(assignedMaterial.mainTexture, Is.SameAs(sourceTexture),
                    "A canceled generation must not replace the target texture.");
                Assert.That(callbackCount, Is.Zero,
                    "A canceled generation must not reach the first-party completion callback.");
            }
            finally
            {
                server.Dispose();
            }

            server.ThrowIfFaulted();
        }

        [UnityTest]
        public IEnumerator ComponentManagerAcceptsValidPngCreativeEndToEnd()
        {
            Texture2D fixture = CreateSolidTexture(new Color32(91, 12, 203, 255));
            byte[] png = fixture.EncodeToPNG();
            Object.Destroy(fixture);

            yield return VerifyComponentManagerCreative(
                png, "image/png", 2, 2, true, string.Empty);
        }

        [UnityTest]
        public IEnumerator ComponentManagerAcceptsValidJpegCreativeEndToEnd()
        {
            Texture2D fixture = CreateSolidTexture(new Color32(19, 177, 42, 255));
            byte[] jpeg = fixture.EncodeToJPG();
            Object.Destroy(fixture);

            yield return VerifyComponentManagerCreative(
                jpeg, "image/jpeg", 2, 2, true, string.Empty);
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsUnsupportedSvgCreativeWithoutRetry()
        {
            yield return VerifyComponentManagerCreative(
                Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
                "image/svg+xml", 2, 2, false, "UnsupportedContent");
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsUnknownCreativeMimeWithoutRetry()
        {
            yield return VerifyComponentManagerCreative(
                new byte[] { 1, 2, 3, 4 }, "application/octet-stream", 2, 2, false,
                "UnsupportedContent");
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsMimeMagicMismatchWithoutRetry()
        {
            Texture2D fixture = CreateSolidTexture(new Color32(4, 5, 6, 255));
            byte[] png = fixture.EncodeToPNG();
            Object.Destroy(fixture);

            yield return VerifyComponentManagerCreative(
                png, "image/jpeg", 2, 2, false, "InvalidServerResponse");
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsDeclaredAndEncodedDimensionMismatchWithoutRetry()
        {
            Texture2D fixture = CreateSolidTexture(new Color32(7, 8, 9, 255));
            byte[] png = fixture.EncodeToPNG();
            Object.Destroy(fixture);

            yield return VerifyComponentManagerCreative(
                png, "image/png", 3, 2, false, "InvalidServerResponse");
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsMalformedPngHeaderWithoutRetry()
        {
            yield return VerifyComponentManagerCreative(
                new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png", 2, 2, false,
                "InvalidServerResponse");
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsOversizedEncodedHeaderWithoutRetry()
        {
            yield return VerifyComponentManagerCreative(
                CreatePngHeader(8193, 1), "image/png", 2, 2, false,
                "InvalidServerResponse");
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsEncodedBodyOverEightMiBWithoutRetry()
        {
            byte[] oversizedBody = new byte[(8 * 1024 * 1024) + 1024];
            byte[] header = CreatePngHeader(2, 2);
            Array.Copy(header, oversizedBody, header.Length);

            yield return VerifyComponentManagerCreative(
                oversizedBody, "image/png", 2, 2, false, "InvalidServerResponse");
        }

        [UnityTest]
        public IEnumerator ComponentManagerAbortsSlowUnknownLengthBodyAtEightMiBWithoutRetry()
        {
            byte[] oversizedBody = new byte[(8 * 1024 * 1024) + 1024];
            byte[] header = CreatePngHeader(2, 2);
            Array.Copy(header, oversizedBody, header.Length);

            yield return VerifyComponentManagerCreative(
                oversizedBody, "image/png", 2, 2, false, "InvalidServerResponse",
                true, 32 * 1024, 1);
        }

        [UnityTest]
        public IEnumerator ComponentManagerRejectsEveryImageRedirectWithoutFollowingIt()
        {
            string[] redirectLocations =
            {
                "javascript:alert(1)",
                "http://user:password@ads.invalid/image.png",
                "http://127.0.0.1/private-image.png"
            };

            foreach (string location in redirectLocations)
            {
                yield return VerifyComponentManagerCreative(
                    Array.Empty<byte>(), "image/png", 2, 2, false, "InvalidServerResponse",
                    imageRedirectLocation: location);
            }
        }

        [UnityTest]
        public IEnumerator ComponentManagerDoesNotDeleteHostCookiesForCreativeOrigin()
        {
            Texture2D fixture = CreateSolidTexture(new Color32(17, 29, 41, 255));
            byte[] png = fixture.EncodeToPNG();
            Object.Destroy(fixture);
            var server = new BlockingImageServer(png, "image/png", 2, 2,
                preserveCookieFixture: true);
            UnityEngine.GameObject target = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            int callbackCount = 0;
            Dictionary<string, object> response = null;

            try
            {
                using (var seed = UnityEngine.Networking.UnityWebRequest.Get(server.CookieSeedUrl))
                {
                    yield return seed.SendWebRequest();
                    Assert.That(seed.result, Is.EqualTo(UnityEngine.Networking.UnityWebRequest.Result.Success));
                }

                var componentManager = new UnityComponentManager();
                componentManager.RunRequest(server.RootUrl, server.AdRequestUrl, string.Empty, "{}",
                    new DependencyGameObject(target), () => true, result =>
                    {
                        callbackCount++;
                        response = result;
                    });

                float deadline = Time.realtimeSinceStartup + 12f;
                while (callbackCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(response, Is.Not.Null);
                Assert.That(response.ContainsKey("adError"), Is.False);

                using (var probe = UnityEngine.Networking.UnityWebRequest.Get(server.CookieProbeUrl))
                {
                    yield return probe.SendWebRequest();
                    Assert.That(probe.result, Is.EqualTo(UnityEngine.Networking.UnityWebRequest.Result.Success));
                }

                StringAssert.Contains("easterad_fixture_cookie=preserve", server.CookieProbeRequestHeaders,
                    "Creative loading must not erase cookies owned by the host or another vendor.");
            }
            finally
            {
                server.Dispose();
                Object.DestroyImmediate(target);
            }

            server.ThrowIfFaulted();
        }

        [UnityTest]
        public IEnumerator ComponentManagerReusesOwnedRendererMaterialAndReleasesRefreshResources()
        {
            Texture2D firstFixture = CreateSolidTexture(new Color32(11, 22, 33, 255));
            Texture2D secondFixture = CreateSolidTexture(new Color32(44, 55, 66, 255));
            byte[] firstPng = firstFixture.EncodeToPNG();
            byte[] secondPng = secondFixture.EncodeToPNG();
            Object.Destroy(firstFixture);
            Object.Destroy(secondFixture);

            UnityEngine.GameObject target = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            Renderer renderer = target.GetComponent<Renderer>();
            Material hostMaterial = renderer.sharedMaterial;
            Material ownedMaterial = null;
            Texture firstTexture = null;
            Texture secondTexture = null;

            try
            {
                yield return RequestCreativeOnTarget(target, firstPng, "image/png", response =>
                    Assert.That(response.ContainsKey("adError"), Is.False));
                ownedMaterial = renderer.sharedMaterial;
                firstTexture = ownedMaterial.mainTexture;
                Assert.That(ownedMaterial, Is.Not.SameAs(hostMaterial));

                yield return RequestCreativeOnTarget(target, secondPng, "image/png", response =>
                    Assert.That(response.ContainsKey("adError"), Is.False));
                secondTexture = renderer.sharedMaterial.mainTexture;
                Assert.That(renderer.sharedMaterial, Is.SameAs(ownedMaterial),
                    "Refresh must reuse the one SDK-owned renderer material.");
                Assert.That(secondTexture, Is.Not.SameAs(firstTexture));

                yield return null;
                Assert.That(firstTexture == null, Is.True,
                    "Replacing a creative must release the prior SDK-owned texture.");

                Object.Destroy(target);
                yield return null;
                yield return null;
                Assert.That(ownedMaterial == null, Is.True,
                    "Destroying the target must release the SDK-created material clone.");
                Assert.That(secondTexture == null, Is.True,
                    "Destroying the target must release the current SDK-owned texture.");
                Assert.That(hostMaterial == null, Is.False,
                    "SDK cleanup must never destroy the host-owned shared material.");
                target = null;
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator ComponentManagerReleasesOwnedUiSpriteAndTextureAcrossRefreshAndDestroy()
        {
            Type imageType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("UnityEngine.UI.Image", false))
                .FirstOrDefault(type => type != null);
            Assert.That(imageType, Is.Not.Null);

            Texture2D firstFixture = CreateSolidTexture(new Color32(70, 80, 90, 255));
            Texture2D secondFixture = CreateSolidTexture(new Color32(100, 110, 120, 255));
            byte[] firstPng = firstFixture.EncodeToPNG();
            byte[] secondPng = secondFixture.EncodeToPNG();
            Object.Destroy(firstFixture);
            Object.Destroy(secondFixture);

            var target = new UnityEngine.GameObject("Owned UI creative", typeof(RectTransform), imageType);
            UnityEngine.Component image = target.GetComponent(imageType);
            PropertyInfo spriteProperty = imageType.GetProperty("sprite");
            Sprite firstSprite = null;
            Sprite secondSprite = null;
            Texture firstTexture = null;
            Texture secondTexture = null;

            try
            {
                yield return RequestCreativeOnTarget(target, firstPng, "image/png", response =>
                    Assert.That(response.ContainsKey("adError"), Is.False));
                firstSprite = (Sprite)spriteProperty.GetValue(image);
                firstTexture = firstSprite.texture;

                yield return RequestCreativeOnTarget(target, secondPng, "image/png", response =>
                    Assert.That(response.ContainsKey("adError"), Is.False));
                secondSprite = (Sprite)spriteProperty.GetValue(image);
                secondTexture = secondSprite.texture;
                Assert.That(secondSprite, Is.Not.SameAs(firstSprite));

                yield return null;
                Assert.That(firstSprite == null, Is.True);
                Assert.That(firstTexture == null, Is.True);

                Object.Destroy(target);
                yield return null;
                yield return null;
                Assert.That(secondSprite == null, Is.True);
                Assert.That(secondTexture == null, Is.True);
                target = null;
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator ComponentManagerFailsClosedWhenTargetHasNoRenderableSurface()
        {
            Texture2D fixture = CreateSolidTexture(new Color32(131, 141, 151, 255));
            byte[] png = fixture.EncodeToPNG();
            Object.Destroy(fixture);
            var target = new UnityEngine.GameObject("No creative surface");
            Dictionary<string, object> response = null;

            try
            {
                yield return RequestCreativeOnTarget(target, png, "image/png", value => response = value);
                Assert.That(response["adErrorType"], Is.EqualTo("InvalidServerResponse"));
                Assert.That(response["retryable"], Is.EqualTo(false));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator ActualLocalImageNetworkFailureIsTheOnlyRetryableImageFailure()
        {
            var server = new BlockingImageServer(Array.Empty<byte>(), "image/png", 2, 2,
                closeImageWithoutResponse: true);
            UnityEngine.GameObject target = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            Dictionary<string, object> response = null;
            int callbackCount = 0;

            try
            {
                var componentManager = new UnityComponentManager();
                componentManager.RunRequest(server.RootUrl, server.AdRequestUrl, string.Empty, "{}",
                    new DependencyGameObject(target), () => true, result =>
                    {
                        callbackCount++;
                        response = result;
                    });

                float deadline = Time.realtimeSinceStartup + 12f;
                while (callbackCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(response["imageErrorType"], Is.EqualTo("NetworkError"));

                var deferred = new DeferredComponentManager();
                ReplaceComponentManager(deferred);
                runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("WindowsEditor");
                impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                    _ => { }, _ => { }, DateTime.MinValue, out mobileSdkClient);
                EasterAdSdkClient.SetSessionForTests(mobileSdkClient, "session=fixture");
                ItemClient client = CreateFirstPartyItem(mobileSdkClient, "actual-image-network-retry");

                FunctionScheduler.FuncCall(ref client, "Load");
                deferred.Complete(response);
                FunctionScheduler.FailFuncCall();

                Assert.That(deferred.RequestCount, Is.EqualTo(2),
                    "Only a locally observed image network failure may enter the retry queue.");
            }
            finally
            {
                server.Dispose();
                Object.DestroyImmediate(target);
            }

            server.ThrowIfFaulted();
        }

        [Test]
        public void SdkShutdownDuringObserverCallbackStopsStaleObserverSnapshot()
        {
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            provider.SynchronousResult = global::EasterAd.EasterAdMobileAdResult.Displayed();
            EasterAdSdkClient sdkClient = CreateMobileSdk();
            ItemClient client = CreateMobileItem(sdkClient, "observer-shutdown");
            int staleObserverCalls = 0;
            sdkClient.AdRequestObserved += _ => sdkClient.OnApplicationQuit();
            sdkClient.AdRequestObserved += _ => staleObserverCalls++;

            FunctionScheduler.FuncCall(ref client, "Load");
            sdkClient.MobileAdRoutine();

            Assert.That(staleObserverCalls, Is.Zero,
                "SDK shutdown must invalidate the outer listener snapshot before later listeners run.");
            Assert.That(client.GetStatus(), Is.EqualTo(ItemStatus.Destroyed));
        }

        [UnityTest]
        public IEnumerator PackagedDependencyWrapperPreservesUnityObjectIdentityAcrossFrame()
        {
            unityObject = new UnityEngine.GameObject("EasterAd identity wrapper boundary");
            var wrapper = new DependencyGameObject(unityObject);
            int expectedInstanceId = unityObject.GetInstanceID();

            yield return null;

            Assert.That(wrapper.GetInstanceID, Is.EqualTo(expectedInstanceId),
                "The packaged dependency wrapper must preserve Unity object identity across a player-loop frame.");

            Object.Destroy(unityObject);
            yield return null;

            Assert.That(unityObject == null, Is.True,
                "The PlayMode player loop must process destruction of the wrapped Unity object.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mobileSdkClient != null && mobileItemClients != null)
            {
                foreach (ItemClient itemClient in mobileItemClients.ToList())
                {
                    mobileSdkClient.DestroyItem(itemClient);
                }
            }

            if (mobileProvider != null)
            {
                EasterAdSdkClient.ResetMobilePresentationStateForTests();
                EasterAdSdkClient.UnregisterMobileAdProvider(mobileProvider);
            }

            if (runtimePlatformOverride != null)
            {
                runtimePlatformOverride.Dispose();
            }

            if (impressionLoggingOverride != null)
            {
                impressionLoggingOverride.Dispose();
            }

            if (componentManagerOverride != null)
            {
                componentManagerOverride.Dispose();
            }

            if (!string.IsNullOrEmpty(preExistingClientKey))
            {
                EasterAdSdkClient sdkClient = EasterAdSdkClient.CreateClient(this);
                sdkClient.RemoveItemClient(preExistingClientKey);
                if (originalPreExistingClient != null)
                {
                    sdkClient.AddItemClient(preExistingClientKey, ref originalPreExistingClient);
                }
            }

            if (registeredSegmentationId > 0)
            {
                InstanceManager.AdSegmentationManager.UnregisterAd(registeredSegmentationId);
            }

            InstanceManager.CameraManager.SetMainCamera(originalMainCamera);
            InstanceManager.DebugLogger.LogEnable = originalLogEnable;
            if (originalDebugLogs != null)
            {
                InstanceManager.DebugLogger.DebugLogs.Clear();
                InstanceManager.DebugLogger.DebugLogs.AddRange(originalDebugLogs);
            }

            if (originalDebugMeshes != null)
            {
                RestoreDebugMeshes(originalDebugMeshes);
            }

            if (assignedMaterial != null)
            {
                Object.Destroy(assignedMaterial);
            }

            if (unityObject != null)
            {
                Object.Destroy(unityObject);
            }

            if (cameraObject != null)
            {
                Object.Destroy(cameraObject);
            }

            if (preExistingClientObject != null)
            {
                Object.Destroy(preExistingClientObject);
            }

            if (sourceTexture != null)
            {
                Object.Destroy(sourceTexture);
            }

            if (readbackTexture != null)
            {
                Object.Destroy(readbackTexture);
            }

            if (testRenderTexture != null)
            {
                testRenderTexture.Release();
                Object.Destroy(testRenderTexture);
            }

            if (mobileItemObjects != null)
            {
                foreach (UnityEngine.GameObject mobileItemObject in mobileItemObjects)
                {
                    if (mobileItemObject != null)
                    {
                        Object.Destroy(mobileItemObject);
                    }
                }
            }

            FunctionScheduler.ClearScheduledCalls();
            assignedMaterial = null;
            testCamera = null;
            testRenderTexture = null;
            sourceTexture = null;
            readbackTexture = null;
            unityObject = null;
            cameraObject = null;
            preExistingClientObject = null;
            registeredClientKey = null;
            preExistingClientKey = null;
            registeredSegmentationId = 0;
            componentManagerOverride = null;
            impressionLoggingOverride = null;
            originalMainCamera = null;
            originalLogEnable = false;
            originalDebugLogs = null;
            originalDebugMeshes = null;
            originalPreExistingClient = null;
            runtimePlatformOverride = null;
            mobileSdkClient = null;
            mobileProvider = null;
            mobileItemClients = null;
            mobileItemObjects = null;
            yield return null;
        }

        private void ReplaceComponentManager(IComponentManager replacement)
        {
            componentManagerOverride?.Dispose();
            componentManagerOverride =
                EasterAdSdkClient.OverrideComponentManagerForTests(replacement);
        }

        private MockMobileAdProvider RegisterMockMobileProvider()
        {
            mobileProvider = new MockMobileAdProvider();
            EasterAdSdkClient.RegisterMobileAdProvider(mobileProvider);
            return mobileProvider;
        }

        private IEnumerator VerifyActualMobileItemRendering(string runtimePlatform, bool canvas)
        {
            CountingComponentManager componentManager = new CountingComponentManager();
            ReplaceComponentManager(componentManager);
            MockMobileAdProvider provider = RegisterMockMobileProvider();
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests(runtimePlatform);
            var httpOperations = new List<string>();
            IDisposable httpObserver = EasterAdSdkClient.ObserveHttpRequestsForTests(httpOperations.Add);
            UnityEngine.GameObject sdkObject = null;
            UnityEngine.GameObject itemObject = null;

            try
            {
                sdkObject = new UnityEngine.GameObject("Actual EasterAdSdk mobile lifecycle");
                EasterAdSdk sdk = sdkObject.AddComponent<EasterAdSdk>();
                Assert.That(sdk.UsesExternalMobileAds, Is.True);

                Func<bool> isVisualEnabled;
                global::EasterAd.Item item;
                if (canvas)
                {
                    itemObject = new UnityEngine.GameObject(
                        "Actual CanvasItem mobile lifecycle", typeof(RectTransform));
                    Type rawImageType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(assembly => assembly.GetType("UnityEngine.UI.RawImage", false))
                        .FirstOrDefault(type => type != null);
                    Assert.That(rawImageType, Is.Not.Null,
                        "The generated package must resolve UnityEngine.UI.RawImage through its uGUI dependency.");
                    Behaviour visual = (Behaviour)itemObject.AddComponent(rawImageType);
                    isVisualEnabled = () => visual.enabled;
                    item = itemObject.AddComponent<global::EasterAd.CanvasItem>();
                }
                else
                {
                    itemObject = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
                    Renderer visual = itemObject.GetComponent<Renderer>();
                    isVisualEnabled = () => visual.enabled;
                    item = itemObject.AddComponent<global::EasterAd.Plane>();
                }

                item.loadOnStart = false;
                item.enableRefresh = true;
                item.refreshTime = 0f;
                item.InitializeWithAdUnitId("actual-mobile-rendering-" + runtimePlatform);

                Assert.That(item.IsInitialized, Is.True);
                Assert.That(isVisualEnabled(), Is.False,
                    "Awake and initialization must hide the in-game renderer when the provider owns mobile presentation.");
                item.SetRenderingVisible(true);
                Assert.That(isVisualEnabled(), Is.False,
                    "The public rendering API must refuse to reveal an externally presented mobile ad surface.");

                item.Load();
                Assert.That(provider.LoadAndShowCount, Is.EqualTo(1));
                Assert.That(componentManager.RequestCount, Is.Zero);
                provider.Complete(global::EasterAd.EasterAdMobileAdResult.Displayed());
                yield return null;

                Assert.That(item.Client.GetStatus(), Is.EqualTo(ItemStatus.Loaded));
                Assert.That(provider.LastHandle.DisposeCount, Is.EqualTo(1));
                SetItemStatus(item.Client, ItemStatus.Impressed);
                yield return null;

                Assert.That(provider.LoadAndShowCount, Is.EqualTo(1),
                    "The wrapper Update loop must not start internal refresh loads on the external mobile route.");
                Assert.That(componentManager.RequestCount, Is.Zero,
                    "The actual Plane/CanvasItem lifecycle must never enter first-party serving on mobile.");
                Assert.That(isVisualEnabled(), Is.False);
                Assert.That(httpOperations, Is.Empty);
            }
            finally
            {
                httpObserver.Dispose();
                if (itemObject != null) Object.DestroyImmediate(itemObject);
                if (sdkObject != null) Object.DestroyImmediate(sdkObject);
                EasterAdSdkClient.UnregisterMobileAdProvider(provider);
            }

            yield return null;
        }

        private EasterAdSdkClient CreateMobileSdk(Action<object> addImpressionLog = null,
            Action<object> addImpressionResultLog = null)
        {
            runtimePlatformOverride = EasterAdSdkClient.OverrideRuntimePlatformForTests("Android");
            impressionLoggingOverride = EasterAdSdkClient.OverrideImpressionLogging(this,
                addImpressionLog ?? (_ => { }), addImpressionResultLog ?? (_ => { }),
                DateTime.MinValue, out mobileSdkClient);
            mobileSdkClient.Initialize("fixture-game", false, "fixture-sdk-key", 2, "Windows", "en");
            return mobileSdkClient;
        }

        private ItemClient CreateMobileItem(EasterAdSdkClient sdkClient, string placementKey, bool canvas = false)
        {
            UnityEngine.GameObject itemObject = canvas
                ? new UnityEngine.GameObject(placementKey, typeof(RectTransform))
                : new UnityEngine.GameObject(placementKey);
            mobileItemObjects.Add(itemObject);
            ItemClient itemClient = canvas
                ? (ItemClient)new CanvasItemClient(new DependencyGameObject(itemObject), placementKey)
                : new PlaneClient(new DependencyGameObject(itemObject), placementKey);
            mobileItemClients.Add(itemClient);
            sdkClient.AddItemClient(placementKey, ref itemClient);
            return itemClient;
        }

        private ItemClient CreateFirstPartyItem(EasterAdSdkClient sdkClient, string placementKey,
            string adUnitId = "222222222222222222222222")
        {
            UnityEngine.GameObject itemObject =
                UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Plane);
            itemObject.name = placementKey;
            mobileItemObjects.Add(itemObject);
            ItemClient itemClient = new PlaneClient(new DependencyGameObject(itemObject), adUnitId);
            mobileItemClients.Add(itemClient);
            sdkClient.AddItemClient(placementKey, ref itemClient);
            return itemClient;
        }

        private static ItemClient CreateImpressionFixtureClient(UnityEngine.GameObject itemObject,
            string adUnitId, string fillId, float impressionSize)
        {
            ItemClient itemClient = new PlaneClient(new DependencyGameObject(itemObject), adUnitId);
            EasterAdSdkClient.SeedImpressionItemForTests(
                itemClient, fillId, impressionSize, 45f, 0.1f);
            SetItemStatus(itemClient, ItemStatus.Loaded);
            return itemClient;
        }

        private static IEnumerator VerifyComponentManagerCreative(byte[] imageBody, string mime,
            int declaredWidth, int declaredHeight, bool expectedAccepted, string expectedErrorType,
            bool omitImageContentLength = false, int imageChunkSize = 0,
            int imageChunkDelayMilliseconds = 0, string imageRedirectLocation = null)
        {
            UnityEngine.GameObject target = UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Quad);
            Renderer renderer = target.GetComponent<Renderer>();
            Material hostMaterial = renderer.material;
            Texture2D sentinelTexture = CreateSolidTexture(new Color32(1, 2, 3, 255));
            hostMaterial.mainTexture = sentinelTexture;
            Dictionary<string, object> response = null;
            int callbackCount = 0;
            var server = new BlockingImageServer(imageBody, mime, declaredWidth, declaredHeight,
                omitImageContentLength, imageChunkSize, imageChunkDelayMilliseconds,
                imageRedirectLocation);

            try
            {
                var componentManager = new UnityComponentManager();
                componentManager.RunRequest(
                    server.RootUrl,
                    server.AdRequestUrl,
                    string.Empty,
                    "{}",
                    new DependencyGameObject(target),
                    () => true,
                    result =>
                    {
                        callbackCount++;
                        response = result;
                    });

                float deadline = Time.realtimeSinceStartup + 12f;
                while (callbackCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(callbackCount, Is.EqualTo(1),
                    "The actual ComponentManager must complete the local creative request exactly once.");
                Assert.That(response, Is.Not.Null);
                if (expectedAccepted)
                {
                    Assert.That(response.ContainsKey("adError"), Is.False);
                    Material presentedMaterial = renderer.sharedMaterial;
                    Assert.That(presentedMaterial, Is.Not.SameAs(hostMaterial));
                    Assert.That(presentedMaterial.mainTexture, Is.Not.SameAs(sentinelTexture),
                        "A validated raster creative must be applied to the target renderer.");
                    Assert.That(presentedMaterial.mainTexture.width, Is.EqualTo(declaredWidth));
                    Assert.That(presentedMaterial.mainTexture.height, Is.EqualTo(declaredHeight));
                    Assert.That(hostMaterial.mainTexture, Is.SameAs(sentinelTexture),
                        "Applying an SDK creative must not mutate the host-owned source material.");
                }
                else
                {
                    Assert.That(response["adErrorType"], Is.EqualTo(expectedErrorType));
                    Assert.That(response["retryable"], Is.EqualTo(false));
                    Assert.That(renderer.sharedMaterial, Is.SameAs(hostMaterial));
                    Assert.That(hostMaterial.mainTexture, Is.SameAs(sentinelTexture),
                        "Rejected creative bytes must never mutate the target texture.");
                }
            }
            finally
            {
                server.Dispose();
                Object.DestroyImmediate(target);
                Object.Destroy(sentinelTexture);
                Object.Destroy(hostMaterial);
            }

            server.ThrowIfFaulted();
            yield return null;
        }

        private static IEnumerator VerifyProtobufTransport(int statusCode, string contentType,
            byte[] body, string setCookie, string expectedError, bool expectedRetryable,
            string expectedSession)
        {
            var server = new SingleResponseServer(statusCode, contentType, body, setCookie);
            var target = new UnityEngine.GameObject("Protobuf transport fixture");
            byte[] responseBody = null;
            string error = null;
            bool retryable = false;
            string session = null;
            int callbackCount = 0;

            try
            {
                var componentManager = new UnityComponentManager();
                componentManager.RunProtobufRequest(
                    server.Url,
                    "POST",
                    "session=original",
                    new ContractAdRequest
                    {
                        AdUnitId = "222222222222222222222222",
                        Width = 2,
                        Height = 2
                    }.ToByteArray(),
                    new DependencyGameObject(target),
                    () => true,
                    (value, requestError, requestRetryable, updatedSession) =>
                    {
                        callbackCount++;
                        responseBody = value;
                        error = requestError;
                        retryable = requestRetryable;
                        session = updatedSession;
                    });

                float deadline = Time.realtimeSinceStartup + 12f;
                while (callbackCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(error, Is.EqualTo(expectedError));
                Assert.That(retryable, Is.EqualTo(expectedRetryable));
                Assert.That(session, Is.EqualTo(expectedSession));
                Assert.That(responseBody, Is.Not.Null);
                if (string.IsNullOrEmpty(expectedError))
                {
                    CollectionAssert.AreEqual(body, responseBody);
                }
                StringAssert.Contains("Accept: application/proto", server.RequestHeaders);
                StringAssert.Contains("Content-Type: application/proto", server.RequestHeaders);
                StringAssert.Contains("Cookie: session=original", server.RequestHeaders);
                Assert.That(ContractAdRequest.Parser.ParseFrom(server.RequestBody).AdUnitId,
                    Is.EqualTo("222222222222222222222222"));
            }
            finally
            {
                server.Dispose();
                Object.DestroyImmediate(target);
            }

            server.ThrowIfFaulted();
        }

        private static IEnumerator RequestCreativeOnTarget(UnityEngine.GameObject target, byte[] imageBody,
            string mime, Action<Dictionary<string, object>> captureResponse)
        {
            Dictionary<string, object> response = null;
            int callbackCount = 0;
            var server = new BlockingImageServer(imageBody, mime, 2, 2);

            try
            {
                var componentManager = new UnityComponentManager();
                componentManager.RunRequest(
                    server.RootUrl,
                    server.AdRequestUrl,
                    string.Empty,
                    "{}",
                    new DependencyGameObject(target),
                    () => true,
                    result =>
                    {
                        callbackCount++;
                        response = result;
                    });

                float deadline = Time.realtimeSinceStartup + 12f;
                while (callbackCount == 0 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(response, Is.Not.Null);
                captureResponse(response);
            }
            finally
            {
                server.Dispose();
            }

            server.ThrowIfFaulted();
        }

        private static ContractAdResponse CreateValidContractAdResponse()
        {
            return new ContractAdResponse
            {
                Id = "444444444444444444444444",
                Mime = "image/png",
                Width = 2,
                Height = 2,
                AspectRatio = 1,
                Url = "https://fixtures.invalid/ad.png"
            };
        }

        private static byte[] CreatePngHeader(uint width, uint height)
        {
            var data = new byte[24]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };
            WriteBigEndianUInt32(data, 16, width);
            WriteBigEndianUInt32(data, 20, height);
            return data;
        }

        private static void WriteBigEndianUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels32(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private static void RestoreDebugMeshes(IEnumerable<RuntimeUI.DebugMesh> debugMeshes)
        {
            RuntimeUI.DebugMeshes.Clear();
            RuntimeUI.DebugMeshes.AddRange(debugMeshes);
        }

        private static void SetItemStatus(ItemClient client, ItemStatus status)
        {
            for (Type type = client.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (field.FieldType == typeof(ItemStatus))
                    {
                        field.SetValue(client, status);
                    }
                }
            }
        }

    }
}
