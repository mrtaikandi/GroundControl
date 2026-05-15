import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { EntryModal } from './EntryModal';
import type { ConfigEntry } from '@/queries/useConfigEntries';

const scopesMock = vi.fn();
vi.mock('@/queries/useScopes', () => ({
  useScopes: () => scopesMock(),
}));

const updateEntryMock = vi.fn();
vi.mock('@/queries/useConfigEntries', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/queries/useConfigEntries')>();
  return {
    ...actual,
    useCreateEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
    useDeleteEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
    useUpdateEntry: () => ({ isPending: false, mutateAsync: updateEntryMock }),
  };
});

const getConfigEntryMock = vi.fn();
vi.mock('@/api/endpoints/config-entries', () => ({
  getConfigEntry: (...args: unknown[]) => getConfigEntryMock(...args),
}));

function renderWithClient(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const SENSITIVE_MASK = '***';

function buildEntry(overrides: Partial<ConfigEntry> = {}): ConfigEntry {
  return {
    createdAt: '2026-01-01T00:00:00Z',
    createdBy: '00000000-0000-0000-0000-000000000000',
    description: null,
    id: '33333333-3333-3333-3333-333333333333',
    isSensitive: false,
    key: 'App:ServiceName',
    ownerId: '22222222-2222-2222-2222-222222222222',
    ownerType: 1,
    updatedAt: '2026-01-01T00:00:00Z',
    updatedBy: '00000000-0000-0000-0000-000000000000',
    valueType: 'String',
    values: [{ scopes: {}, value: 'checkout-api' }],
    version: '1',
    ...overrides,
  } as ConfigEntry;
}

function buildSensitiveEntry(overrides: Partial<ConfigEntry> = {}): ConfigEntry {
  return {
    createdAt: '2026-01-01T00:00:00Z',
    createdBy: '00000000-0000-0000-0000-000000000000',
    description: null,
    id: '11111111-1111-1111-1111-111111111111',
    isSensitive: true,
    key: 'Secret:ApiKey',
    ownerId: '22222222-2222-2222-2222-222222222222',
    ownerType: 1,
    updatedAt: '2026-01-01T00:00:00Z',
    updatedBy: '00000000-0000-0000-0000-000000000000',
    valueType: 'String',
    values: [{ scopes: {}, value: SENSITIVE_MASK }],
    version: '1',
    ...overrides,
  } as ConfigEntry;
}

describe('EntryModal', () => {
  beforeEach(() => {
    updateEntryMock.mockReset();
    updateEntryMock.mockResolvedValue(undefined);
    getConfigEntryMock.mockReset();
    scopesMock.mockReset();
    scopesMock.mockReturnValue({ data: { data: [] }, isSuccess: true });
  });

  it('renders the canonical dimension name when the stored key only differs by case', async () => {
    // Backend stores the scope as "Environment" but the entry was written with a lowercase
    // dimension key ("environment"). The Select trigger should still display "Environment" since
    // the dimensions are case-insensitive on the server.
    scopesMock.mockReturnValue({
      data: {
        data: [
          { id: 'dim-env', dimension: 'Environment', allowedValues: ['dev', 'prod'] },
        ],
      },
      isSuccess: true,
    });

    const entry = buildSensitiveEntry({
      values: [
        { scopes: {}, value: SENSITIVE_MASK },
        { scopes: { environment: 'prod' }, value: SENSITIVE_MASK },
      ],
    });

    renderWithClient(
      <EntryModal entry={entry} mode="edit" onOpenChange={vi.fn()} open projectId="project-1" />,
    );

    await waitFor(() => {
      const triggers = screen.getAllByRole('combobox');
      const triggerTexts = triggers.map((trigger) => trigger.textContent?.trim());
      expect(triggerTexts).toContain('Environment');
      expect(triggerTexts.some((text) => text?.includes('(deleted)'))).toBe(false);
      expect(triggerTexts).toContain('prod');
    });
  });

  it('does not label the dimension as deleted while the scopes query is still loading', async () => {
    // While useScopes() is in-flight (isSuccess === false), the dimensions list is empty but we
    // cannot conclude the scope is gone — the stored value should render plainly until the query
    // resolves.
    scopesMock.mockReturnValue({ data: undefined, isSuccess: false });

    const entry = buildSensitiveEntry({
      values: [
        { scopes: {}, value: SENSITIVE_MASK },
        { scopes: { environment: 'prod' }, value: SENSITIVE_MASK },
      ],
    });

    renderWithClient(
      <EntryModal entry={entry} mode="edit" onOpenChange={vi.fn()} open projectId="project-1" />,
    );

    await waitFor(() => {
      const triggers = screen.getAllByRole('combobox');
      const triggerTexts = triggers.map((trigger) => trigger.textContent?.trim());
      expect(triggerTexts.some((text) => text === 'environment')).toBe(true);
      expect(triggerTexts.some((text) => text?.includes('(deleted)'))).toBe(false);
    });
  });

  it('keeps the scoped row visible when the referenced scope is no longer in the scopes list', async () => {
    // The entry was created when "environment" was a defined scope. The scope has since been
    // deleted or renamed in /api/scopes, so the dimensions list returned by useScopes() doesn't
    // include it. The stored dimension/value must still render so the user can see and clean it up.
    scopesMock.mockReturnValue({
      data: {
        data: [
          { id: 'dim-region', dimension: 'region', allowedValues: ['us', 'eu'] },
        ],
      },
      isSuccess: true,
    });

    const entry = buildSensitiveEntry({
      values: [
        { scopes: {}, value: SENSITIVE_MASK },
        { scopes: { environment: 'prod' }, value: SENSITIVE_MASK },
      ],
    });

    renderWithClient(
      <EntryModal entry={entry} mode="edit" onOpenChange={vi.fn()} open projectId="project-1" />,
    );

    await waitFor(() => {
      const triggers = screen.getAllByRole('combobox');
      const triggerTexts = triggers.map((trigger) => trigger.textContent?.trim());
      expect(triggerTexts.some((text) => text?.includes('environment'))).toBe(true);
      expect(triggerTexts.some((text) => text?.includes('prod'))).toBe(true);
    });
  });

  it('renders dimension and scope value for a sensitive entry that has scoped values', async () => {
    scopesMock.mockReturnValue({
      data: {
        data: [
          { id: 'dim-env', dimension: 'environment', allowedValues: ['dev', 'prod'] },
        ],
      },
      isSuccess: true,
    });

    const entry = buildSensitiveEntry({
      values: [
        { scopes: {}, value: SENSITIVE_MASK },
        { scopes: { environment: 'prod' }, value: SENSITIVE_MASK },
      ],
    });

    renderWithClient(
      <EntryModal entry={entry} mode="edit" onOpenChange={vi.fn()} open projectId="project-1" />,
    );

    // Drain the open-useEffect that calls form.reset(formValues) — the bug surfaces after that fires.
    await waitFor(() => {
      const triggers = screen.getAllByRole('combobox');
      const triggerTexts = triggers.map((trigger) => trigger.textContent?.trim());
      expect(triggerTexts).toContain('environment');
      expect(triggerTexts).toContain('prod');
    });
  });

  it('rejects invalid key characters', async () => {
    const user = userEvent.setup();

    renderWithClient(<EntryModal mode="create" onOpenChange={vi.fn()} open projectId="project-1" />);

    await user.type(screen.getByLabelText('Key'), 'bad key!');
    await user.click(screen.getByRole('button', { name: 'Create entry' }));

    expect(
      await screen.findByText('Key must start with a letter and contain only letters, digits, colons, dots, underscores, or hyphens'),
    ).toBeInTheDocument();
  });

  it('rejects keys that do not start with a letter', async () => {
    const user = userEvent.setup();

    renderWithClient(<EntryModal mode="create" onOpenChange={vi.fn()} open projectId="project-1" />);

    await user.type(screen.getByLabelText('Key'), '1leading-digit');
    await user.click(screen.getByRole('button', { name: 'Create entry' }));

    expect(
      await screen.findByText('Key must start with a letter and contain only letters, digits, colons, dots, underscores, or hyphens'),
    ).toBeInTheDocument();
  });

  it('submits the renamed key when editing an entry', async () => {
    const user = userEvent.setup();

    renderWithClient(
      <EntryModal entry={buildEntry()} mode="edit" onOpenChange={vi.fn()} open projectId="project-1" />,
    );

    const keyInput = screen.getByLabelText('Key') as HTMLInputElement;
    expect(keyInput).not.toBeDisabled();
    expect(keyInput.value).toBe('App:ServiceName');

    await user.clear(keyInput);
    await user.type(keyInput, 'App:RenamedServiceName');
    await user.click(screen.getByRole('button', { name: 'Save entry' }));

    await waitFor(() => expect(updateEntryMock).toHaveBeenCalledTimes(1));
    const [variables] = updateEntryMock.mock.calls[0];
    expect(variables.body.key).toBe('App:RenamedServiceName');
    expect(variables.id).toBe('33333333-3333-3333-3333-333333333333');
    expect(variables.version).toBe('1');
  });

  it('locks the form for masked sensitive entries until reveal succeeds', async () => {
    // Arrange
    const user = userEvent.setup();
    getConfigEntryMock.mockResolvedValue({
      id: '11111111-1111-1111-1111-111111111111',
      values: [{ scopes: {}, value: 'real-secret' }],
    });

    // Act
    renderWithClient(
      <EntryModal entry={buildSensitiveEntry()} mode="edit" onOpenChange={vi.fn()} open projectId="project-1" />,
    );

    // Assert — masked state
    expect(screen.getByText('Reveal to Edit Values')).toBeInTheDocument();
    const defaultValueInput = screen.getByLabelText('Default value') as HTMLInputElement;
    expect(defaultValueInput).toBeDisabled();
    expect(defaultValueInput.value).toBe(SENSITIVE_MASK);
    expect(screen.getByRole('button', { name: 'Save entry' })).toBeDisabled();

    // Act — reveal
    await user.click(screen.getByRole('button', { name: 'Reveal Sensitive Values' }));

    // Assert — unlocked
    await waitFor(() => expect(getConfigEntryMock).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111', { decrypt: true }));
    await waitFor(() => expect(screen.queryByText('Reveal to Edit Values')).not.toBeInTheDocument());
    expect(defaultValueInput).not.toBeDisabled();
    expect(defaultValueInput.value).toBe('real-secret');
    expect(screen.getByRole('button', { name: 'Save entry' })).not.toBeDisabled();
  });

  it('shows delete entry actions inside edit mode', async () => {
    const user = userEvent.setup();

    renderWithClient(
      <EntryModal entry={buildSensitiveEntry()} mode="edit" onOpenChange={vi.fn()} open projectId="project-1" />,
    );

    await user.click(screen.getByRole('button', { name: 'Delete entry' }));

    expect(await screen.findByRole('heading', { name: 'Delete Entry' })).toBeInTheDocument();
    expect(screen.getByText(/Secret:ApiKey/)).toBeInTheDocument();
  });
});
