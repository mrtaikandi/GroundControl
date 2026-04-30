import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { useTweaksStore } from '@/store/tweaks';
import { DriftBanner } from './DriftBanner';

describe('DriftBanner', () => {
  it('dismisses through the tweaks store', async () => {
    const user = userEvent.setup();
    useTweaksStore.setState({ driftBannerVisible: true });

    render(<DriftBanner />);

    expect(screen.getByRole('alert')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Dismiss drift banner' }));

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(useTweaksStore.getState().driftBannerVisible).toBe(false);
  });
});