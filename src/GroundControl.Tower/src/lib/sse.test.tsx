import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useLiveActivity } from './sse';

const fetchEventSourceMock = vi.hoisted(() => vi.fn());

vi.mock('@microsoft/fetch-event-source', () => ({
  fetchEventSource: fetchEventSourceMock,
}));

describe('useLiveActivity', () => {
  const originalFetch = globalThis.fetch;
  const originalReadableStream = globalThis.ReadableStream;
  const supportedReadableStream = originalReadableStream ?? class ReadableStreamStub {};

  beforeEach(() => {
    fetchEventSourceMock.mockReset();
    vi.useFakeTimers();
    Object.defineProperty(globalThis, 'ReadableStream', { configurable: true, value: undefined });
  });

  afterEach(() => {
    vi.useRealTimers();
    globalThis.fetch = originalFetch;
    Object.defineProperty(globalThis, 'ReadableStream', { configurable: true, value: originalReadableStream });
  });

  it('polls activity summary when SSE streams are unavailable', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      json: vi.fn().mockResolvedValue({ clients: 7, rate: 3 }),
      ok: true,
    });
    globalThis.fetch = fetchMock as unknown as typeof fetch;

    const { result, unmount } = renderHook(() => useLiveActivity());

    await flushPromises();

    expect(result.current.clientCount).toBe(7);
    expect(result.current.eventsPerSecond).toBe(3);
    expect(result.current.isConnected).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/activity/summary'), expect.objectContaining({ headers: expect.objectContaining({ 'api-version': '1.0' }) }));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);

    unmount();

    act(() => {
      vi.advanceTimersByTime(30_000);
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('updates from activity SSE events when streams are available', async () => {
    Object.defineProperty(globalThis, 'ReadableStream', { configurable: true, value: supportedReadableStream });
    fetchEventSourceMock.mockImplementation(async (_url, options) => {
      await options.onopen(new Response(null, { status: 200 }));
      options.onmessage({ data: '{"clients":4,"rate":9}', event: 'activity' });
    });

    const { result } = renderHook(() => useLiveActivity());

    await flushPromises();

    expect(fetchEventSourceMock).toHaveBeenCalledWith(expect.stringContaining('/activity/stream'), expect.objectContaining({ headers: expect.objectContaining({ 'api-version': '1.0' }) }));
    expect(result.current.clientCount).toBe(4);
    expect(result.current.eventsPerSecond).toBe(9);
    expect(result.current.isConnected).toBe(true);
  });

  it('aborts and stays disconnected on authentication failure', async () => {
    Object.defineProperty(globalThis, 'ReadableStream', { configurable: true, value: supportedReadableStream });
    fetchEventSourceMock.mockImplementation(async (_url, options) => {
      await options.onopen(new Response(null, { status: 401 }));
      expect(options.signal.aborted).toBe(true);
    });

    const { result } = renderHook(() => useLiveActivity());

    await flushPromises();

    expect(result.current.isConnected).toBe(false);
    expect(result.current.clientCount).toBe(0);
  });
});

async function flushPromises() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}