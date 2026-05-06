import '@testing-library/jest-dom/vitest';

const storage = new Map<string, string>();

const testStorage: Storage = {
	get length() {
		return storage.size;
	},
	clear: () => storage.clear(),
	getItem: (key) => storage.get(key) ?? null,
	key: (index) => Array.from(storage.keys())[index] ?? null,
	removeItem: (key) => storage.delete(key),
	setItem: (key, value) => storage.set(key, value),
};

Object.defineProperty(globalThis, 'localStorage', {
	configurable: true,
	value: testStorage,
});

Object.defineProperty(window, 'localStorage', {
	configurable: true,
	value: testStorage,
});

class TestResizeObserver implements ResizeObserver {
	observe() {}

	unobserve() {}

	disconnect() {}
}

globalThis.ResizeObserver = TestResizeObserver;