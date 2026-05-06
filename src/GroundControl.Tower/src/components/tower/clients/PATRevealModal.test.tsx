import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { PATRevealModal } from './PATRevealModal';

describe('PATRevealModal', () => {
  it('keeps Done disabled until the copy confirmation is checked', () => {
    render(<PATRevealModal onConfirm={vi.fn()} open rawToken="client-secret" />);

    const done = screen.getByRole('button', { name: 'Done' });

    expect(done).toBeDisabled();

    fireEvent.click(screen.getByLabelText('I have copied this token'));

    expect(done).toBeEnabled();
  });
});