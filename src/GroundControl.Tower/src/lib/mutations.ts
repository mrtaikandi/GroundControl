import { useMutation, type UseMutationOptions } from '@tanstack/react-query';
import { useCallback, useRef } from 'react';
import { ApiError } from '@/api/client';
import { showConflictToast } from '@/components/tower/feedback/ConflictToast';

type VersionedVariables<TVariables> = TVariables & { version: string };

export function useConflictMutation<TVariables, TData>(
  mutationFn: (variables: VersionedVariables<TVariables>) => Promise<TData>,
  options?: UseMutationOptions<TData, ApiError, VersionedVariables<TVariables>>,
) {
  const latestVariables = useRef<VersionedVariables<TVariables> | undefined>(undefined);
  const mutation = useMutation<TData, ApiError, VersionedVariables<TVariables>>({
    ...options,
    mutationFn: (variables) => {
      latestVariables.current = variables;

      return mutationFn(variables);
    },
    onError: (error, variables, onMutateResult, context) => {
      latestVariables.current = variables;

      if (error.status === 412) {
        showConflictToast({
          latestVersion: error.currentVersion,
          retryWithLatest,
        });
      }

      options?.onError?.(error, variables, onMutateResult, context);
    },
  });

  const retryWithLatest = useCallback(
    (newVersion: string) => {
      if (!latestVariables.current) {
        return;
      }

      mutation.mutate({ ...latestVariables.current, version: newVersion });
    },
    [mutation],
  );

  return {
    ...mutation,
    retryWithLatest,
  };
}