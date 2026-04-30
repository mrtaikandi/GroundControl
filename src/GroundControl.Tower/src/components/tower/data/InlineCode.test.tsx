import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { InlineCode } from './InlineCode';

describe('InlineCode', () => {
  it('copies the rendered text when copyable', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();

    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    render(<InlineCode copyable>project.alpha</InlineCode>);

    await user.click(screen.getByRole('button', { name: /project.alpha/i }));

    expect(writeText).toHaveBeenCalledWith('project.alpha');
  });
});