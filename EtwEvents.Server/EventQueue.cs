﻿using System;
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
        readonly EtwEvent _emtpyEvent = new EtwEvent();

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
        }

        public Task Completion => _block.Completion;

        void PostEvent(tracing.TraceEvent evt) {
            var posted = _block.Post(new EtwEvent(evt));
            if (!posted)
                _logger.LogInformation($"Could not post trace event {evt.EventIndex}.");

        }

        void TimerCallback(object? state) {
            _block.Post(_emtpyEvent);
            _block.TriggerBatch();
        }

        async Task ProcessBatches(Timer timer, TimeSpan maxWriteDelay) {
            var writeOptions = new WriteOptions(WriteFlags.NoCompress | WriteFlags.BufferHint);
            var flushWriteOptions = new WriteOptions(WriteFlags.NoCompress);

            while (await _block.OutputAvailableAsync()) { //blocks here until data arrives or .Complete is called
                while (_block.TryReceive(null, out var etwEvents)) {
                    _logger.LogInformation($"Received batch with {etwEvents.Length} events.");
                    _responseStream.WriteOptions = flushWriteOptions;
                    var batch = new EtwEventBatch();
                    batch.Events.AddRange(etwEvents);
                    await _responseStream.WriteAsync(batch).ConfigureAwait(false);
                }
                timer.Change(maxWriteDelay, Timeout.InfiniteTimeSpan);
            }
        }

        public async Task Process(RealTimeTraceSession session, TimeSpan maxWriteDelay) {
            Task processTask;
            using (var timer = new Timer(TimerCallback)) {
                processTask = ProcessBatches(timer, maxWriteDelay);
                timer.Change(maxWriteDelay, Timeout.InfiniteTimeSpan);
                await session.StartEvents(PostEvent, _context.CancellationToken).ConfigureAwait(false);
            }
            await _block.Completion.ConfigureAwait(false);
            await processTask.ConfigureAwait(false);
        }
    }
}