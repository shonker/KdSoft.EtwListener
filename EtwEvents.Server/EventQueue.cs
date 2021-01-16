﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;
using tracing = Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    class EventQueue
    {
        readonly IServerStreamWriter<EtwEventBatch> _responseStream;
        readonly ServerCallContext _context;
        readonly BatchBlock<EtwEvent> _block;
        readonly ILogger<EventQueue> _logger;

        long _lastCheckedTicks;
        long _maxWriteDelayTicks;

        public EventQueue(
            IServerStreamWriter<EtwEventBatch> responseStream,
            ServerCallContext context,
            ILogger<EventQueue> logger,
            int batchSize = 100
        ) {
            this._logger = logger;
            this._block = new BatchBlock<EtwEvent>(batchSize, new GroupingDataflowBlockOptions {
                CancellationToken = context.CancellationToken,
                EnsureOrdered = true,
                 
            });
            this._responseStream = responseStream;
            this._context = context;
            this._lastCheckedTicks = Environment.TickCount64;
        }

        public Task Completion => _block.Completion;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void PostEvent(tracing.TraceEvent evt) {
            var posted = _block.Post(new EtwEvent(evt));
            if (!posted)
                _logger.LogInformation($"Could not post trace event {evt.EventIndex}.");
            Volatile.Write(ref _lastCheckedTicks, Environment.TickCount64);
        }

        // TimerCallback get called periodically, but we do not always want to trigger a batch
        void TimerCallback(object? state) {
            var lastCheckedTicks = Interlocked.Exchange(ref _lastCheckedTicks, Environment.TickCount64);
            var deltaTicks = Environment.TickCount64 - lastCheckedTicks;
            if (deltaTicks > _maxWriteDelayTicks) {
                _block.TriggerBatch();
            }
        }

        async Task ProcessBatches(Timer timer, TimeSpan maxWriteDelay) {
            var writeOptions = new WriteOptions(WriteFlags.NoCompress | WriteFlags.BufferHint);
            var flushWriteOptions = new WriteOptions(WriteFlags.NoCompress);

            while (await _block.OutputAvailableAsync().ConfigureAwait(false)) { //blocks here until data arrives or .Complete is called
                while (_block.TryReceive(null, out var etwEvents)) {
                    _logger.LogInformation($"Received batch with {etwEvents.Length} events.");
                    _responseStream.WriteOptions = flushWriteOptions;
                    var batch = new EtwEventBatch();
                    batch.Events.AddRange(etwEvents);
                    await _responseStream.WriteAsync(batch).ConfigureAwait(false);
                    Volatile.Write(ref _lastCheckedTicks, Environment.TickCount64);
                }
            }
        }

        public async Task Process(RealTimeTraceSession session, TimeSpan maxWriteDelay) {
            this._maxWriteDelayTicks = (long)maxWriteDelay.TotalMilliseconds;
            Task processTask;
            using (var timer = new Timer(TimerCallback)) {
                processTask = ProcessBatches(timer, maxWriteDelay);
                timer.Change(maxWriteDelay, maxWriteDelay);
                await session.StartEvents(PostEvent, _context.CancellationToken).ConfigureAwait(false);
            }
            await _block.Completion.ConfigureAwait(false);
            await processTask.ConfigureAwait(false);
        }
    }
}
