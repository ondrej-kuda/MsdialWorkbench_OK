﻿using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace CompMs.App.Msdial.Utility
{
    public static class ObservableExtensions
    {
        public static IObservable<bool> CombineLatestValuesAreAnyTrue(this IEnumerable<IObservable<bool>> source) {
            return source.CombineLatestValuesAreAllFalse().Inverse();
        }

        public static IObservable<T> Gate<T>(this IObservable<T> source, IObservable<bool> condition) {
            return source.WithLatestFrom(condition).Where(p => p.Second).Select(p => p.First);
        }

        public static IObservable<U> ObserveCollectionItems<T, U>(T collection) where T : IEnumerable<U>, INotifyCollectionChanged {
            return collection.ToObservable().Concat(collection.ObserveAddChanged<U>());
        }

        public static IObservable<Unit> ObserveRemoveFrom<T>(this T source, INotifyCollectionChanged collection) {
            return new[]
            {
                collection.ObserveResetChanged<T>(),
                collection.ObserveRemoveChanged<T>().Where(rm => EqualityComparer<T>.Default.Equals(source, rm)).ToUnit(),
            }.Merge().Take(1);
        }

        public static IObservable<V> ObserveUntilRemove<T, U, V>(T collection, Func<U, IObservable<V>> selector) where T : IEnumerable<U>, INotifyCollectionChanged {
            return ObserveCollectionItems<T, U>(collection)
                .SelectMany(item => selector(item).TakeUntil(item.ObserveRemoveFrom(collection)));
        }

        public static IObservable<U> ObserveUntilRemove<T, U>(this ReadOnlyObservableCollection<T> collection, Func<T, IObservable<U>> selector) {
            return ObserveUntilRemove<ReadOnlyObservableCollection<T>, T, U>(collection, selector);
        }

        public static IObservable<U> Switch<T, U>(this IObservable<T> source, Func<T, IObservable<U>> selector) {
            return source.Select(selector).Switch();
        }

        public static IObservable<U> ToConstant<T, U>(this IObservable<T> source, U constant) {
            return source.Select(_ => constant);
        }

        public static IObservable<T> TakeFirstAfterEach<T, U>(this IObservable<T> source, IObservable<U> other) {
            return source.SkipUntil(other).Take(1).Repeat();
        }
    }
}
