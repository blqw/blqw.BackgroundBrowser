using CefSharp;
using CefSharp.Handler;
using CefSharp.OffScreen;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace blqw
{
    public class BackgroundBrowser : IDisposable
    {
        private static readonly AutoResetEvent _ready = new AutoResetEvent(false);
        private static readonly AutoResetEvent _beforeShutdown = new AutoResetEvent(false);
        private static readonly AutoResetEvent _shutdownComplete = new AutoResetEvent(false);
        public static void Init()
        {
            if (Cef.IsInitialized)
            {
                return;
            }
            lock (typeof(Cef))
            {
                if (Cef.IsInitialized)
                {
                    return;
                }
                Task.Run(() =>
                {
                    Cef.Initialize(new CefSettings()
                    {
                        LogSeverity = LogSeverity.Disable,
                        IgnoreCertificateErrors = true,
                        Locale = "zh-cn",
                    });
                    _ready.Set();
                    _beforeShutdown.WaitOne();
                    Cef.Shutdown();
                    _shutdownComplete.Set();
                });
                _ready.WaitOne();
            }
        }


        public static void Abort()
        {
            _beforeShutdown.Set();
            _shutdownComplete.WaitOne();
        }

        public BackgroundBrowser()
        {
            Init();
        }


        public void Open(string url, int maxwait = 1000)
        {
            _awaitMillisecond = maxwait;
            _requests.Clear();
            ResetReadyTime();
            if (Browser == null)
            {
                Browser = new ChromiumWebBrowser(url);
                Browser.RequestHandler = new RequestHandler(this);
            }
            else
            {
                Browser.Load(url);
            }
        }

        private void ResetReadyTime(int? ms = null)
        {
            var newTime = DateTime.Now.AddMilliseconds(ms ?? _awaitMillisecond);
            if (newTime > _readyTime)
            {
                _readyTime = newTime;
            }
        }

        private readonly ConcurrentDictionary<ulong, long> _requests = new ConcurrentDictionary<ulong, long>();

        class RequestHandler : DefaultRequestHandler
        {
            private readonly BackgroundBrowser _backgroundBrowser;

            public RequestHandler(BackgroundBrowser backgroundBrowser) => _backgroundBrowser = backgroundBrowser;
            public override CefReturnValue OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
            {
                if (request.ResourceType == ResourceType.Image
                    || request.ResourceType == ResourceType.Favicon
                    || request.ResourceType == ResourceType.FontResource
                    || request.ResourceType == ResourceType.Media
                    || request.ResourceType == ResourceType.PluginResource
                    || request.ResourceType == ResourceType.SubResource
                    || request.ResourceType == ResourceType.Stylesheet)
                {
                    // 不加载无用的资源
                    return CefReturnValue.Cancel;
                }
                _backgroundBrowser.ResetReadyTime();
                _backgroundBrowser._requests.TryAdd(request.Identifier, DateTime.Now.Ticks);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{request.Identifier,-3}] BeforeLoad   ({request.ResourceType,-9}): {request.Url}");
                return base.OnBeforeResourceLoad(browserControl, browser, frame, request, callback);
            }

            public override void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
            {
                _backgroundBrowser.ResetReadyTime();
                _backgroundBrowser._requests.TryRemove(request.Identifier, out var ms);
                if (response.StatusCode == 0)
                {
                    return;
                }
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{request.Identifier,-3}] LoadComplete ({((DateTime.Now.Ticks - ms) / 10000) + " ms",-9}): {request.Url}");
                base.OnResourceLoadComplete(browserControl, browser, frame, request, response, status, receivedContentLength);
            }

        }


        private int _awaitMillisecond = 0;
        private DateTime _readyTime;


        public ChromiumWebBrowser Browser { get; private set; }

        public IEnumerable<IFrame> GetFrames()
        {
            var browser = Browser?.GetBrowser();
            if (browser != null)
            {
                foreach (var identity in browser.GetFrameIdentifiers())
                {
                    var frame = browser.GetFrame(identity);
                    if (frame != null)
                    {
                        yield return frame;
                    }
                }
            }
        }

        public void ExecuteScript(string javascript)
        {
            Browser?.ExecuteScriptAsync(javascript);
        }
        public async Task<object> EvaluateScript(string javascript)
        {
            if (Browser == null)
            {
                return null;
            }
            var rep = await Browser.EvaluateScriptAsync(javascript);
            if (!rep.Success)
            {
                throw new InvalidOperationException(rep.Message);
            }
            return rep.Result;
        }


        public async Task WaitAsync(CancellationToken cancellation = default(CancellationToken))
        {
            await Task.Delay(500, cancellation);
            while (true)
            {
                var diff = (_readyTime - DateTime.Now).TotalMilliseconds;
                if (diff < 0)
                {
                    if (_requests.IsEmpty)
                    {
                        return;
                    }
                    diff = 500;
                }
                cancellation.ThrowIfCancellationRequested();
                await Task.Delay((int)diff, cancellation);
            }
        }

        public void Dispose() => Browser.Dispose();
    }
}
