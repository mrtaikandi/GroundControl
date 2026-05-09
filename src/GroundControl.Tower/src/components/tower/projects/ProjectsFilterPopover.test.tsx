import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ProjectsFilterPopover } from './ProjectsFilterPopover';

describe('ProjectsFilterPopover', () => {
  it('does not render the count pill when no search filter is applied', () => {
    render(<ProjectsFilterPopover appliedSearch={undefined} onApply={vi.fn()} />);

    expect(screen.queryByText('1')).not.toBeInTheDocument();
  });

  it('renders the count pill when a search filter is applied', () => {
    render(<ProjectsFilterPopover appliedSearch="auth" onApply={vi.fn()} />);

    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('applies the trimmed search and closes when Done is clicked', async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<ProjectsFilterPopover appliedSearch={undefined} onApply={onApply} />);

    await user.click(screen.getByRole('button', { name: 'Filter projects' }));
    await user.type(screen.getByPlaceholderText('Project name or description'), '  auth  ');
    await user.click(screen.getByRole('button', { name: 'Done' }));

    expect(onApply).toHaveBeenCalledWith('auth');
  });

  it('applies an empty search as undefined', async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<ProjectsFilterPopover appliedSearch="auth" onApply={onApply} />);

    await user.click(screen.getByRole('button', { name: 'Filter projects' }));
    const input = screen.getByPlaceholderText('Project name or description');
    await user.clear(input);
    await user.click(screen.getByRole('button', { name: 'Done' }));

    expect(onApply).toHaveBeenCalledWith(undefined);
  });

  it('submits the filter when Enter is pressed', async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<ProjectsFilterPopover appliedSearch={undefined} onApply={onApply} />);

    await user.click(screen.getByRole('button', { name: 'Filter projects' }));
    const input = screen.getByPlaceholderText('Project name or description');
    await user.type(input, 'billing{Enter}');

    expect(onApply).toHaveBeenCalledWith('billing');
  });

  it('clear all resets the filter and closes the popover', async () => {
    const user = userEvent.setup();
    const onApply = vi.fn();
    render(<ProjectsFilterPopover appliedSearch="auth" onApply={onApply} />);

    await user.click(screen.getByRole('button', { name: 'Filter projects' }));
    await user.click(screen.getByRole('button', { name: 'Clear all' }));

    expect(onApply).toHaveBeenCalledWith(undefined);
  });

  it('disables clear all when no filter is active', async () => {
    const user = userEvent.setup();
    render(<ProjectsFilterPopover appliedSearch={undefined} onApply={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Filter projects' }));
    expect(screen.getByRole('button', { name: 'Clear all' })).toBeDisabled();
  });
});