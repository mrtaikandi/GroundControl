import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { PublishModal } from './PublishModal';

vi.mock('@/queries/useSnapshots', () => ({
  usePublishSnapshot: () => ({ isPending: false, mutateAsync: vi.fn() }),
  useSnapshotDetail: () => ({ data: { entries: [{ isSensitive: false, key: 'feature.enabled', valueType: 'boolean', values: [{ scopes: {}, value: 'true' }] }] }, isLoading: false }),
}));

vi.mock('@/queries/useResolvedConfig', () => ({
  useResolvedConfig: () => ({ data: [{ isSensitive: false, key: 'feature.enabled', value: 'true', valueType: 'boolean' }], isLoading: false }),
}));

describe('PublishModal', () => {
  it('disables continue when the resolved JSON is identical', () => {
    render(<PublishModal activeSnapshotId="snapshot-1" onOpenChange={vi.fn()} open projectId="project-1" />);

    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
  });
});