﻿using System.Collections.Immutable;

namespace KdSoft.EtwEvents
{
    public class AggregatingNotifier<T> where T : notnull
    {
        readonly Func<Task<T>> _getNotificationData;

        public AggregatingNotifier(Func<Task<T>> getNotificationData) {
            this._getNotificationData = getNotificationData;
        }

        public async ValueTask PostNotification() {
            var changeEnumerators = Volatile.Read(ref _changeEnumerators);
            foreach (var enumerator in changeEnumerators) {
                try {
                    await enumerator.Advance().ConfigureAwait(false);
                }
                catch { }
            }
        }

        ImmutableList<ChangeEnumerator> _changeEnumerators = ImmutableList<ChangeEnumerator>.Empty;

        void AddEnumerator(ChangeEnumerator enumerator) {
            ImmutableInterlocked.Update(ref _changeEnumerators, ce => ce.Add(enumerator));
        }

        void RemoveEnumerator(ChangeEnumerator enumerator) {
            ImmutableInterlocked.Update(ref _changeEnumerators, ce => ce.Remove(enumerator));
        }

        public IAsyncEnumerable<T> GetNotifications() {
            return new ChangeListener(this);
        }

        // https://anthonychu.ca/post/async-streams-dotnet-core-3-iasyncenumerable/
        class ChangeListener: IAsyncEnumerable<T>
        {
            readonly AggregatingNotifier<T> _changeNotifier;

            public ChangeListener(AggregatingNotifier<T> changeNotifier) {
                this._changeNotifier = changeNotifier;
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) {
                return new ChangeEnumerator(_changeNotifier, cancellationToken);
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/async-streams
        class ChangeEnumerator: PendingAsyncEnumerator<T>
        {
            readonly AggregatingNotifier<T> _changeNotifier;

            public ChangeEnumerator(AggregatingNotifier<T> changeNotifier, CancellationToken cancelToken) : base(cancelToken) {
                this._changeNotifier = changeNotifier;
                changeNotifier.AddEnumerator(this);
            }

            public override ValueTask DisposeAsync() {
                _changeNotifier.RemoveEnumerator(this);
                return default;
            }

            protected override Task<T> GetNext() {
                return _changeNotifier._getNotificationData();
            }
        }
    }
}
