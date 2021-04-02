using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using LaunchDarkly.EventSource;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    public class Worker: BackgroundService
    {
        readonly IConfiguration _configuration;
        readonly IOptions<ControlOptions> _controlOptions;
        readonly IOptions<EventQueueOptions> _eventQueueOptions;
        readonly IOptions<EventSessionOptions> _sessionOptions;
        readonly IOptions<EventSinkOptions> _sinkOptions;
        readonly IEventSinkFactory _sinkFactory;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger<Worker> _logger;
        readonly HttpClient _http;
        readonly HttpClientCertificateHandler _httpCertHandler;
        readonly JsonSerializerOptions _jsonOptions;

        EventSource? _eventSource;
        RealTimeTraceSession? _session;

        public Worker(
            IConfiguration configuration,
            IOptions<ControlOptions> controlOptions,
            IOptions<EventQueueOptions> eventQueueOptions,
            IOptions<EventSessionOptions> sessionOptions,
            IOptions<EventSinkOptions> sinkOptions,
            IEventSinkFactory sinkFactory,
            ILoggerFactory loggerFactory
        ) {
            this._configuration = configuration;
            this._controlOptions = controlOptions;
            this._eventQueueOptions = eventQueueOptions;
            this._sessionOptions = sessionOptions;
            this._sinkOptions = sinkOptions;
            this._sinkFactory = sinkFactory;
            this._loggerFactory = loggerFactory;
            this._logger = loggerFactory.CreateLogger<Worker>();

            _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            _httpCertHandler = new HttpClientCertificateHandler(controlOptions.Value.ClientCertificate);
            _http = new HttpClient(_httpCertHandler);
        }

        #region Control Channel

        async Task ProcessEvent(ControlEvent sse) {
            var opts = _controlOptions.Value;
            var ses = _session;
            if (ses == null)
                return;

            ImmutableArray<Diagnostic> diagnostics;
            BuildFilterResult filterResult;

            switch (sse.Event) {
                case "ChangeLogLevel":
                    //
                    break;
                case "Start":
                    //
                    break;
                case "Stop":
                    //
                    break;
                case "GetState":
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "UpdateProviders":
                    // WithDiscardUnknownFields does currently not work, so we should fix this at source
                    var providerSettingsList = ProviderSettingsList.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    var providerSettings = providerSettingsList.ProviderSettings;
                    if (providerSettings != null && ses != null) {
                        var providersToBeDisabled = ses.EnabledProviders.Select(ep => ep.Name).ToHashSet();
                        foreach (var setting in providerSettings) {
                            ses.EnableProvider(setting);
                            providersToBeDisabled.Remove(setting.Name);
                        }
                        foreach (var providerName in providersToBeDisabled) {
                            ses.DisableProvider(providerName);
                        }
                    }
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "ApplyFilter":
                    var filterRequest = SetFilterRequest.Parser.ParseJson(sse.Data);
                    //var filterRequest = JsonSerializer.Deserialize<SetFilterRequest>(sse.Data, _jsonOptions);
                    if (filterRequest == null)
                        return;
                    diagnostics = ses.SetFilter(filterRequest.CsharpFilter);
                    filterResult = new BuildFilterResult().AddDiagnostics(diagnostics);
                    await PostMessage($"Agent/ApplyFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    if (diagnostics.Length == 0)
                        await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "TestFilter":
                    var testRequest = TestFilterRequest.Parser.ParseJson(sse.Data);
                    //var testRequest = JsonSerializer.Deserialize<TestFilterRequest>(sse.Data, _jsonOptions);
                    if (testRequest == null)
                        return;
                    diagnostics = RealTimeTraceSession.TestFilter(testRequest.CsharpFilter);
                    filterResult = new BuildFilterResult().AddDiagnostics(diagnostics);
                    await PostMessage($"Agent/TestFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    break;
                case "UpdateEventSink":
                    //
                    break;
                default:
                    break;
            }
        }

        Task PostMessage<T>(string path, T content) {
            var opts = _controlOptions.Value;
            var postUri = new Uri(opts.Uri, path);
            var httpMsg = new HttpRequestMessage(HttpMethod.Post, postUri);

            var mediaType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
            httpMsg.Content = JsonContent.Create<T>(content, mediaType, _jsonOptions);

            return _http.SendAsync(httpMsg);
        }

        Task SendStateUpdate() {
            //var agentName = _httpCertHandler.ClientCert.GetNameInfo(X509NameType.SimpleName, false);
            //var agentEmail = _httpCertHandler.ClientCert.GetNameInfo(X509NameType.EmailName, false);
            var state = new Models.AgentState {
                EnabledProviders = _session?.EnabledProviders.ToImmutableList() ?? ImmutableList<EtwLogging.ProviderSetting>.Empty,
                // Id = string.IsNullOrWhiteSpace(agentEmail) ? agentName : $"{agentName} ({agentEmail})",
                Id = string.Empty,  // will be filled in on server using the client certificate
                Host = Dns.GetHostName(),
                Site = _configuration["Site"],
                FilterBody = _session?.GetCurrentFilterBody()
            };
            return PostMessage("Agent/UpdateState", state);
        }

        async void EventReceived(object? sender, MessageReceivedEventArgs e) {
            try {
                var lastEventIdStr = string.IsNullOrEmpty(e.Message.LastEventId) ? "" : $"-{e.Message.LastEventId}";
                var messageDataStr = string.IsNullOrEmpty(e.Message.Data) ? "" : $", {e.Message.Data}";
                _logger?.LogInformation($"{nameof(EventReceived)}: {e.EventName}{lastEventIdStr}{messageDataStr}");
                await ProcessEvent(new ControlEvent { Event = e.EventName, Id = e.Message.LastEventId ?? "", Data = e.Message.Data ?? "" }).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger?.LogAllErrors(ex, $"Error in {nameof(EventReceived)}:\n");
            }
        }

        void EventError(object? sender, ExceptionEventArgs e) {
            _logger?.LogAllErrors(e.Exception, $"Error in {nameof(EventError)}:\n");
        }

        void EventSourceStateChanged(object? sender, StateChangedEventArgs e) {
            _logger?.LogInformation($"{nameof(EventSourceStateChanged)}: {e.ReadyState}");
        }

        public EventSource StartSSE() {
            var opts = _controlOptions.Value;
            var evtUri = new Uri(opts.Uri, "Agent/GetEvents");
            var cfgBuilder = Configuration.Builder(evtUri).HttpClient(_http);
            if (opts.InitialRetryDelay != null)
                cfgBuilder.InitialRetryDelay(opts.InitialRetryDelay.Value);
            if (opts.MaxRetryDelay != null)
                cfgBuilder.MaxRetryDelay(opts.MaxRetryDelay.Value);
            if (opts.BackoffResetThreshold != null)
                cfgBuilder.BackoffResetThreshold(opts.BackoffResetThreshold.Value);
            var config = cfgBuilder.Build();

            var evt = new EventSource(config);
            evt.MessageReceived += EventReceived;
            evt.Error += EventError;
            evt.Opened += EventSourceStateChanged;
            evt.Closed += EventSourceStateChanged;

            var startTask = evt.StartAsync();
            return evt;
        }

        #endregion

        #region ETW Events

        #endregion

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            _eventSource = StartSSE();

            var sopts = _sessionOptions.Value;

            var logger = _loggerFactory.CreateLogger<RealTimeTraceSession>();
            var session = new RealTimeTraceSession("default", TimeSpan.MaxValue, logger, false);
            this._session = session;

            stoppingToken.Register(() => {
                var ses = _session;
                if (ses != null) {
                    _session = null;
                    ses.Dispose();
                }
                var evt = _eventSource;
                if (evt != null) {
                    _eventSource = null;
                    evt.Close();
                }
            });


            session.GetLifeCycle().Used();

            var diagnostics = session.SetFilter(sopts.Filter);
            if (!diagnostics.IsEmpty) {
                logger.LogError("Filter compilation failed.");
            }

            // enable the providers
            foreach (var provider in _sessionOptions.Value.Providers) {
                var setting = new EtwLogging.ProviderSetting();
                setting.Name = provider.Name;
                setting.Level = (EtwLogging.TraceEventLevel)provider.Level;
                setting.MatchKeywords = provider.MatchKeyWords;
                session.EnableProvider(setting);
            }

            var sinkOpts = _sinkOptions.Value;
            var serOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var optsJson = JsonSerializer.Serialize(sinkOpts.Definition.Options, serOpts);
            var credsJson = JsonSerializer.Serialize(sinkOpts.Definition.Credentials, serOpts);

            await using (var sink = await _sinkFactory.Create(optsJson, credsJson).ConfigureAwait(false)) {
                var processorLogger = _loggerFactory.CreateLogger<PersistentEventProcessor>();
                using (var processor = new PersistentEventProcessor(sink, _eventQueueOptions, stoppingToken, processorLogger, _sessionOptions.Value.BatchSize)) {
                    var maxWriteDelay = TimeSpan.FromMilliseconds(_sessionOptions.Value.MaxWriteDelayMSecs);
                    await processor.Process(session, maxWriteDelay, stoppingToken).ConfigureAwait(false);
                }
            }
        }

        public override void Dispose() {
            base.Dispose();
            _http?.Dispose();
            _eventSource?.Dispose();
            _session?.Dispose();
        }
    }
}