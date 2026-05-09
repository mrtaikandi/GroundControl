import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { EntryModal } from './EntryModal';

vi.mock('@/queries/useScopes', () => ({
  useScopes: () => ({ data: { data: [] } }),
}));

vi.mock('@/queries/useConfigEntries', () => ({
  useCreateEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
  useUpdateEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
}));

function renderWithClient(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('EntryModal', () => {
  it('rejects invalid key characters', async () => {
    const user = userEvent.setup();

    renderWithClient(<EntryModal mode="create" onOpenChange={vi.fn()} open projectId="project-1" />);

    await user.type(screen.getByLabelText('Key'), 'bad key!');
    await user.click(screen.getByRole('button', { name: 'Create entry' }));

    expect(await screen.findByText('Use letters, numbers, colons, dots, underscores, and hyphens only')).toBeInTheDocument();
  });
});