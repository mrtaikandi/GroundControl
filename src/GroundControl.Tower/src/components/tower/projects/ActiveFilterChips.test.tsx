import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ActiveFilterChips } from './ActiveFilterChips';

describe('ActiveFilterChips', () => {
  it('renders nothing when no filters are active', () => {
    const { container } = render(<ActiveFilterChips onRemoveSearch={vi.fn()} search={undefined} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('renders the search chip with the value', () => {
    render(<ActiveFilterChips onRemoveSearch={vi.fn()} search="auth" />);

    expect(screen.getByText(/Search:/)).toBeInTheDocument();
    expect(screen.getByText('"auth"')).toBeInTheDocument();
  });

  it('invokes the dismiss callback when the chip is removed', async () => {
    const user = userEvent.setup();
    const onRemoveSearch = vi.fn();
    render(<ActiveFilterChips onRemoveSearch={onRemoveSearch} search="auth" />);

    await user.click(screen.getByRole('button', { name: 'Remove search filter' }));

    expect(onRemoveSearch).toHaveBeenCalledTimes(1);
  });
});