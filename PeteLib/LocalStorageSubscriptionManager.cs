using Microsoft.JSInterop;

namespace PeteLib;

public sealed class LocalStorageSubscriptionManager : IAsyncDisposable
{
	private bool IsSubscribedToJSLocalStorage;
	private readonly DotNetObjectReference<LocalStorageSubscriptionManager> JSObjectReference;
	private readonly ReaderWriterLockSlim Locker = new();
	private readonly IJSRuntime JSRuntime;
	private readonly Dictionary<object, Subscriber> OwnerToSubscriberLookup = new();
	private readonly Dictionary<string, HashSet<Subscriber>> KeyToSubscriberSetLookup = new();

	public LocalStorageSubscriptionManager(IJSRuntime jSRuntime)
	{
		JSRuntime = jSRuntime ?? throw new ArgumentNullException(nameof(jSRuntime));
		JSObjectReference = DotNetObjectReference.Create(this);
	}

	[JSInvokable("ValueChanged")]
	public void NotifyChange(string key)
	{
		ArgumentNullException.ThrowIfNull(key);

		Locker.EnterReadLock();
		try
		{
			if (!KeyToSubscriberSetLookup.TryGetValue(key, out HashSet<Subscriber>? subscriberSet))
				return;

			foreach (Subscriber subscriber in subscriberSet)
				subscriber.NotifyChange(key);
		}
		finally
		{
			Locker.ExitReadLock();
		}
	}

	public async ValueTask SubscribeAsync(object owner, string key, Action callback)
	{
		ArgumentNullException.ThrowIfNull(owner);
		ArgumentNullException.ThrowIfNull(key);
		ArgumentNullException.ThrowIfNull(callback);

		Locker.EnterWriteLock();
		try
		{
			if (!IsSubscribedToJSLocalStorage)
			{
				await JSRuntime.InvokeVoidAsync("blazorLocalStorage.subscribe", JSObjectReference);
				IsSubscribedToJSLocalStorage = true;
			}

			if (!OwnerToSubscriberLookup.TryGetValue(owner, out Subscriber? subscriber))
			{
				subscriber = new Subscriber();
				OwnerToSubscriberLookup.Add(owner, subscriber);
			}
			subscriber.Subscribe(key, callback);

			if (!KeyToSubscriberSetLookup.TryGetValue(key, out HashSet<Subscriber>? subscriptionsSet))
			{
				subscriptionsSet = new HashSet<Subscriber>();
				KeyToSubscriberSetLookup.Add(key, subscriptionsSet);
			}
			if (!subscriptionsSet.Contains(subscriber))
				subscriptionsSet.Add(subscriber);
		}
		finally
		{
			Locker.ExitWriteLock();
		}
	}

	public void Unsubscribe(object owner, string key, Action callback)
	{
		ArgumentNullException.ThrowIfNull(owner);
		ArgumentNullException.ThrowIfNull(key);
		ArgumentNullException.ThrowIfNull(callback);

		Locker.EnterWriteLock();
		try
		{
			if (!OwnerToSubscriberLookup.TryGetValue(owner, out Subscriber? subscriber))
				return;

			subscriber.Unsubscribe(key, callback, out bool isEmpty);
			if (isEmpty)
			{
				if (!KeyToSubscriberSetLookup.TryGetValue(key, out HashSet<Subscriber>? subscriberSet))
					return;
				RemoveSubscriber(key, subscriberSet, subscriber);
			}
		}
		finally
		{
			Locker.ExitWriteLock();
		}
	}

	public void RemoveAll(object owner)
	{
		ArgumentNullException.ThrowIfNull(owner);

		Locker.EnterWriteLock();
		try
		{
			if (!OwnerToSubscriberLookup.Remove(owner, out Subscriber? subscriber))
				return;

			foreach (string key in subscriber.Keys)
			{
				if (KeyToSubscriberSetLookup.TryGetValue(key, out HashSet<Subscriber>? subscriberSet))
					RemoveSubscriber(key, subscriberSet, subscriber);
			}
		}
		finally
		{
			Locker.ExitWriteLock();
		}
	}

	async ValueTask IAsyncDisposable.DisposeAsync()
	{
		if (IsSubscribedToJSLocalStorage)
		{
			await JSRuntime.InvokeVoidAsync("blazorLocalStorage.unsubscribe");
			IsSubscribedToJSLocalStorage = false;
		}
		JSObjectReference.Dispose();
	}

	private void RemoveSubscriber(string key, HashSet<Subscriber> subscriberSet, Subscriber subscriber)
	{
		subscriberSet.Remove(subscriber);
		if (subscriberSet.Count == 0)
			KeyToSubscriberSetLookup.Remove(key);
	}
}

internal class Subscriber
{
	private readonly Dictionary<string, HashSet<Action>> KeyToCallbackSetLookup = new();

	public IEnumerable<string> Keys => KeyToCallbackSetLookup.Keys;

	public void NotifyChange(string key)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (!KeyToCallbackSetLookup.TryGetValue(key, out HashSet<Action>? callbackSet))
			return;

		foreach (Action callback in callbackSet)
			callback();
	}

	public void Subscribe(string key, Action callback)
	{
		ArgumentNullException.ThrowIfNull(key);
		ArgumentNullException.ThrowIfNull(callback);

		if (!KeyToCallbackSetLookup.TryGetValue(key, out HashSet<Action>? callbackSet))
		{
			callbackSet = new HashSet<Action>();
			KeyToCallbackSetLookup.Add(key, callbackSet);
		}
		callbackSet.Add(callback);
	}

	public void Unsubscribe(string key, Action callback, out bool isEmpty)
	{
		ArgumentNullException.ThrowIfNull(key);
		ArgumentNullException.ThrowIfNull(callback);

		if (!KeyToCallbackSetLookup.TryGetValue(key, out HashSet<Action>? callbackSet))
		{
			isEmpty = true;
			return;
		}

		callbackSet.Remove(callback);
		isEmpty = callbackSet.Count == 0;
	}
}

