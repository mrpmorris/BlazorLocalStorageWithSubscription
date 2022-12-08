using Microsoft.JSInterop;

namespace PeteLib;

public class LocalStorage
{
	private const string JSNamespace = "blazorLocalStorage";
	private readonly IJSRuntime JSRuntime;
	private readonly LocalStorageSubscriptionManager SubscriptionManager;

	public LocalStorage(IJSRuntime jSRuntime, LocalStorageSubscriptionManager subscriptionManager)
	{
		JSRuntime = jSRuntime ?? throw new ArgumentNullException(nameof(jSRuntime));
		SubscriptionManager = subscriptionManager ?? throw new ArgumentNullException(nameof(subscriptionManager));
	}

	public ValueTask<T> GetAsync<T>(string key)
			=> JSRuntime.InvokeAsync<T>($"{JSNamespace}.get", key);

	public async ValueTask SetAsync(string key, object value)
	{
		await JSRuntime.InvokeVoidAsync($"{JSNamespace}.set", key, value);
		SubscriptionManager.NotifyChange(key);
	}

	public ValueTask DeleteAsync(string key)
			=> JSRuntime.InvokeVoidAsync($"{JSNamespace}.delete", key);
}

