import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { PublishModal } from './PublishModal';

const sharedEntry = { isSensitive: false, key: 'feature.enabled', valueType: 'boolean', values: [{ scopes: {}, value: 'true' }] };

vi.mock('@/queries/useSnapshots', () => ({
  usePublishSnapshot: () => ({ isPending: false, mutateAsync: vi.fn() }),
  useSnapshotDetail: () => ({ data: { entries: [sharedEntry] }, isLoading: false }),
  useSnapshotPreview: () => ({ data: { bsonSizeBytes: 0, diffHash: 'hash-1', entries: [sharedEntry], nextVersion: 1, projectId: 'project-1' }, isError: false, isFetching: false, isLoading: false, refetch: vi.fn() }),
}));

describe('PublishModal', () => {
  it('disables continue when the resolved JSON is identical', () => {
    render(<PublishModal activeSnapshotId="snapshot-1" onOpenChange={vi.fn()} open projectId="project-1" />);

    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
  });
});
