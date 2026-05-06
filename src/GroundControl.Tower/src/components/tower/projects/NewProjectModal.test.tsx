import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { NewProjectModal } from './NewProjectModal';

const createProject = vi.hoisted(() => vi.fn());

vi.mock('@/queries/useGroups', () => ({
  useGroups: () => ({
    data: { data: [{ id: '11111111-1111-4111-8111-111111111111', name: 'Platform' }] },
    isLoading: false,
  }),
}));

vi.mock('@/queries/useProjects', () => ({
  useCreateProject: () => ({
    isPending: false,
    mutateAsync: createProject,
  }),
}));

describe('NewProjectModal', () => {
  beforeEach(() => {
    createProject.mockReset();
  });

  it('rejects an empty project name', async () => {
    const user = userEvent.setup();
    render(<NewProjectModal />);

    await user.click(screen.getByRole('button', { name: 'New project' }));
    await user.click(screen.getByRole('button', { name: 'Create project' }));

    expect(await screen.findByText('Project name is required')).toBeInTheDocument();
    expect(createProject).not.toHaveBeenCalled();
  });

  it('rejects project names with spaces', async () => {
    const user = userEvent.setup();
    render(<NewProjectModal />);

    await user.click(screen.getByRole('button', { name: 'New project' }));
    await user.type(screen.getByLabelText('Name'), 'Bad Name');
    await user.click(screen.getByRole('button', { name: 'Create project' }));

    expect(await screen.findByText('Lowercase, numbers, and hyphens only')).toBeInTheDocument();
    await waitFor(() => expect(createProject).not.toHaveBeenCalled());
  });
});