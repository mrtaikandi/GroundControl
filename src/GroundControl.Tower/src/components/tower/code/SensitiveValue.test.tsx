import { act, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SensitiveProvider } from '@/lib/sensitive';
import { useTweaksStore } from '@/store/tweaks';
import { SensitiveValue } from './SensitiveValue';

describe('SensitiveValue', () => {
  it('updates between masked and plaintext output from sensitive context', () => {
    useTweaksStore.setState({ sensitiveMasked: true });

    render(
      <SensitiveProvider>
        <SensitiveValue isSensitive value="server-password" />
      </SensitiveProvider>,
    );

    expect(screen.getByText('••••••••')).toBeInTheDocument();
    expect(screen.getByLabelText('Sensitive value')).toBeInTheDocument();
    expect(screen.queryByText('server-password')).not.toBeInTheDocument();

    act(() => {
      useTweaksStore.setState({ sensitiveMasked: false });
    });

    expect(screen.getByText('server-password')).toBeInTheDocument();
    expect(screen.queryByText('••••••••')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Sensitive value')).not.toBeInTheDocument();
  });

  it('shows plaintext for non-sensitive values when masking is enabled', () => {
    useTweaksStore.setState({ sensitiveMasked: true });

    render(
      <SensitiveProvider>
        <SensitiveValue isSensitive={false} value="feature.enabled" />
      </SensitiveProvider>,
    );

    expect(screen.getByText('feature.enabled')).toBeInTheDocument();
    expect(screen.queryByText('••••••••')).not.toBeInTheDocument();
  });
});