﻿using System;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EtwEvents.WebClient
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class EtwController: ControllerBase
    {
        readonly TraceSessionManager _sessionManager;
        readonly IOptionsMonitor<EventSessionOptions> _optionsMonitor;

        public EtwController(TraceSessionManager sessionManager, IOptionsMonitor<EventSessionOptions> optionsMonitor) {
            this._sessionManager = sessionManager;
            this._optionsMonitor = optionsMonitor;
        }

        [HttpPost]
        public async Task<IActionResult> OpenSession(TraceSessionRequest request) {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            var credentials = ChannelCredentials.Insecure;
            try {
                var session = await _sessionManager.OpenSession(request.Name, request.Host, credentials, request.Providers, request.LifeTime.ToDuration()).ConfigureAwait(false);
                return Ok(new SessionResult(session.EnabledProviders, session.FailedProviders));
            }
            catch (Exception ex) {
                return Problem(title: ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CloseRemoteSession([FromQuery]string name) {
            var credentials = ChannelCredentials.Insecure;
            try {
                var success = await _sessionManager.CloseRemoteSession(name).ConfigureAwait(false);
                return Ok(success);
            }
            catch (Exception ex) {
                return Problem(title: ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> StartEvents(string sessionName) {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                    var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                    await session.StartEvents(webSocket, _optionsMonitor).ConfigureAwait(false);
                    return new EmptyResult();  // OkResult not right here, tries to set status code which is not good in this scenario
                }
                else {
                    return Problem(title: "Session not found");
                }
            }
            else {
                return BadRequest();
            }
        }

        [HttpPost]
        public async Task<IActionResult> StopEvents(string sessionName) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                await session.StopEvents().ConfigureAwait(false);
                return Ok();
            }
            else {
                return Problem(title: "Session not found");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetCSharpFilter([FromBody]FilterRequest request) {
            if (request == null)
                return BadRequest();
            string? csharpFilter = null;
            if (!string.IsNullOrWhiteSpace(request.CSharpFilter)) {
                csharpFilter = request.CSharpFilter;
            }

            if (_sessionManager.TryGetValue(request.SessionName!, out var sessionEntry)) {
                var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                var result = await session.SetCSharpFilter(csharpFilter).ConfigureAwait(false);
                return Ok(result);
            }
            else {
                return Problem(title: "Session not found");
            }
        }
    }
}
