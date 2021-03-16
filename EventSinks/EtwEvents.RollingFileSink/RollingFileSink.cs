﻿using System;
using System.Buffers;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;
using KdSoft.Logging;

namespace KdSoft.EtwEvents.EventSinks {
    public class RollingFileSink: IEventSink
    {
        static readonly EtwEvent _emptyEvent = new EtwEvent();

        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;
        readonly ReadOnlyMemory<byte> _newLine = new byte[] { 10 };
        readonly RollingFileFactory _fileFactory;
        readonly Channel<(EtwEvent evt, long sequenceNo)> _channel;

        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public RollingFileSink(RollingFileFactory fileFactory) {
            this._fileFactory = fileFactory;
            this._channel = Channel.CreateUnbounded<(EtwEvent evt, long sequenceNo)>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            });

            _jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            _bufferWriter = new ArrayBufferWriter<byte>(1024);
            _jsonWriter = new Utf8JsonWriter(_bufferWriter, _jsonOptions);

            RunTask = Process();
        }

        public bool IsDisposed {
            get {
                Interlocked.MemoryBarrier();
                var isDisposed = this._isDisposed;
                Interlocked.MemoryBarrier();
                return isDisposed > 0;
            }
        }

        // returns true if actually disposed, false if already disposed
        bool InternalDispose() {
            var oldDisposed = Interlocked.CompareExchange(ref _isDisposed, 99, 0);
            if (oldDisposed == 0) {
                _channel.Writer.TryComplete();
                return true;
            }
            return false;
        }

        public void Dispose() {
            if (InternalDispose()) {
                _channel.Reader.Completion.Wait();
                _jsonWriter.Dispose();
                _fileFactory.Dispose();
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public async ValueTask DisposeAsync() {
            if (InternalDispose()) {
                await _channel.Reader.Completion.ConfigureAwait(false);
                await _jsonWriter.DisposeAsync().ConfigureAwait(false);
                await _fileFactory.DisposeAsync().ConfigureAwait(false);
            }
        }

        // writes a JSON object to the buffer, terminating with a new line
        // see https://github.com/serilog/serilog-formatting-compact
        void WriteEventJson(EtwEvent evt, long sequenceNo) {
            _jsonWriter.Reset();
            _jsonWriter.WriteStartObject();

            _jsonWriter.WriteNumber("sequenceNo", sequenceNo);
            _jsonWriter.WriteString("providerName", evt.ProviderName);
            _jsonWriter.WriteNumber("channel", evt.Channel);
            _jsonWriter.WriteNumber("id", evt.Id);
            _jsonWriter.WriteNumber("keywords", evt.Keywords);
            _jsonWriter.WriteString("level", evt.Level.ToString());
            _jsonWriter.WriteNumber("opcode", evt.Opcode);
            _jsonWriter.WriteString("opcodeName", evt.OpcodeName);
            _jsonWriter.WriteString("taskName", evt.TaskName);
            if (evt.TimeStamp == null)
                _jsonWriter.WriteString("timeStamp", DateTimeOffset.UtcNow.ToString("o"));
            else {
                _jsonWriter.WriteString("timeStamp", evt.TimeStamp.ToDateTimeOffset().ToString("o"));
            }
            _jsonWriter.WriteNumber("version", evt.Version);

            _jsonWriter.WriteStartObject("payload");
            foreach (var payload in evt.Payload) {
                _jsonWriter.WriteString(payload.Key, payload.Value);
            }
            _jsonWriter.WriteEndObject();

            _jsonWriter.WriteEndObject();
            _jsonWriter.Flush();

            _bufferWriter.Write(_newLine.Span);
        }

        public ValueTask<bool> FlushAsync() {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            var posted = _channel.Writer.TryWrite((_emptyEvent, 0));
            return new ValueTask<bool>(posted);
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            var posted = _channel.Writer.TryWrite((evt, sequenceNo));
            return new ValueTask<bool>(posted);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            bool posted = true;
            foreach (var evt in evtBatch.Events) {
                posted = _channel.Writer.TryWrite((evt, sequenceNo++));
                if (!posted)
                    break;
            }
            return new ValueTask<bool>(posted);
        }

        // returns true if reading is complete
        async Task<bool> ProcessBatchToBuffer() {
            await foreach (var (evt, sequenceNo) in _channel.Reader.ReadAllAsync().ConfigureAwait(false)) {
                if (object.ReferenceEquals(evt, _emptyEvent))
                    return false;
                WriteEventJson(evt, sequenceNo);
            }
            // batch is complete, we can write it now to the file
            return true;
        }

        async Task<bool> WriteBatchAsync(FileStream stream) {
            var eventBatch = _bufferWriter.WrittenMemory;
            if (eventBatch.IsEmpty)
                return true;

            await stream.WriteAsync(eventBatch).ConfigureAwait(false);
            _bufferWriter.Clear();

            await stream.FlushAsync().ConfigureAwait(false);
            return true;
        }

        async Task ProcessBatches() {
            bool isCompleted;
            do {
                // checks rollover conditions and returns appropriate file stream
                var stream = await _fileFactory.GetCurrentFileStream().ConfigureAwait(false);
                isCompleted = await ProcessBatchToBuffer().ConfigureAwait(false);
                await WriteBatchAsync(stream).ConfigureAwait(false);
            } while (!isCompleted);
        }

        public async Task<bool> Process() {
            var processTask = ProcessBatches();
            await _channel.Reader.Completion.ConfigureAwait(false);
            await processTask.ConfigureAwait(false);
            return IsDisposed;
        }
    }
}