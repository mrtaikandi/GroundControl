import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { EntryModal } from './EntryModal';
import type { ConfigEntry } from '@/queries/useConfigEntries';

vi.mock('@/queries/useScopes', () => ({
  useScopes: () => ({ data: { data: [] } }),
}));

vi.mock('@/queries/useConfigEntries', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/queries/useConfigEntries')>();
  return {
    ...actual,
    useCreateEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
    useUpdateEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
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

function buildSensitiveEntry(): ConfigEntry {
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
  } as ConfigEntry;
}

describe('EntryModal', () => {
  it('rejects invalid key characters', async () => {
    const user = userEvent.setup();

    renderWithClient(<EntryModal mode="create" onOpenChange={vi.fn()} open projectId="project-1" />);

    await user.type(screen.getByLabelText('Key'), 'bad key!');
    await user.click(screen.getByRole('button', { name: 'Create entry' }));

    expect(await screen.findByText('Use letters, numbers, colons, dots, underscores, and hyphens only')).toBeInTheDocument();
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
});
