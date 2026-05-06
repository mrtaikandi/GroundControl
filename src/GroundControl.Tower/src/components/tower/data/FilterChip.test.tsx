import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { FilterChip } from './FilterChip';

describe('FilterChip', () => {
  it('calls onToggle when clicked', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();

    render(<FilterChip count={3} label="ConfigEntry" onToggle={onToggle} />);

    await user.click(screen.getByRole('button', { name: 'ConfigEntry3' }));

    expect(onToggle).toHaveBeenCalledOnce();
  });
});