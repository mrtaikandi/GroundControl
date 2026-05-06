import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { EntryModal } from './EntryModal';

vi.mock('@/queries/useScopes', () => ({
  useScopes: () => ({ data: { data: [] } }),
}));

vi.mock('@/queries/useConfigEntries', () => ({
  useCreateEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
  useUpdateEntry: () => ({ isPending: false, mutateAsync: vi.fn() }),
}));

describe('EntryModal', () => {
  it('rejects invalid key characters', async () => {
    const user = userEvent.setup();

    render(<EntryModal mode="create" onOpenChange={vi.fn()} open projectId="project-1" />);

    await user.type(screen.getByLabelText('Key'), 'bad key!');
    await user.click(screen.getByRole('button', { name: 'Create entry' }));

    expect(await screen.findByText('Use letters, numbers, colons, dots, underscores, and hyphens only')).toBeInTheDocument();
  });
});