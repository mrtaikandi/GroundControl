import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SegmentedControl } from './SegmentedControl';

describe('SegmentedControl', () => {
  it('calls onChange with the selected option', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();

    render(
      <SegmentedControl
        onChange={onChange}
        options={[
          { label: 'Flat', value: 'flat' },
          { label: 'Tree', value: 'tree' },
        ]}
        value="flat"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Tree' }));

    expect(onChange).toHaveBeenCalledWith('tree');
  });
});