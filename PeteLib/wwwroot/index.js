(function () {
	window.blazorLocalStorage = {
		_listeners: [],
		get: key => key in localStorage ? JSON.parse(localStorage[key]) : null,
		set: (key, value) => { localStorage[key] = JSON.stringify(value); },
		subscribe: (subscriber) => window.blazorLocalStorage._listeners.push(subscriber),
		unsubscribe: (subscriber) => {
			const index = window.blazorLocalStorage._listeners.indexOf(subscriber);
			if (index > -1) {
				window.blazorLocalStorage._listeners.splice(index, 1);
			}
		},
		delete: key => { delete localStorage[key]; }
	};
	window.addEventListener("storage", function (e) {
		window.blazorLocalStorage._listeners.forEach((listener) => {
			listener.invokeMethodAsync("ValueChanged", e.key);
		});
	});
})();