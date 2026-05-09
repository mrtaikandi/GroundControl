import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, useId, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { ScopedValuesField } from '@/components/tower/data/ScopedValuesField';
import { getVariable } from '@/api/endpoints/variables';
import { useGroups } from '@/queries/useGroups';
import { useCreateVariable, useDeleteVariable, useUpdateVariable, type Variable } from '@/queries/useVariables';

const SENSITIVE_MASK = '***';

export type VariableTier =
  | { kind: 'global' }
  | { kind: 'project'; projectId: string };

const variableSchema = z.object({
  defaultValue: z.string(),
  description: z.string().max(500, 'Use 500 characters or fewer').optional(),
  groupId: z.string().nullable(),
  isSensitive: z.boolean(),
  name: z.string().min(1, 'Name is required'),
  scopedValues: z.array(z.object({
    dimension: z.string().optional(),
    scopeValue: z.string().optional(),
    value: z.string(),
  })),
});

type VariableFormValues = z.infer<typeof variableSchema>;

interface VariableEditorModalProps {
  mode: 'create' | 'edit';
  onOpenChange: (open: boolean) => void;
  open: boolean;
  tier: VariableTier;
  variable?: Variable;
}

export function VariableEditorModal({ mode, onOpenChange, open, tier, variable }: VariableEditorModalProps) {
  const groups = useGroups();
  const createVariable = useCreateVariable();
  const updateVariable = useUpdateVariable();
  const deleteVariable = useDeleteVariable();
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const deleteConfirmInputId = useId();
  const isEdit = mode === 'edit';
  const formValues = useMemo(() => toFormValues(variable), [variable]);
  const form = useForm<VariableFormValues>({
    defaultValues: formValues,
    resolver: zodResolver(variableSchema),
  });
  const isSensitive = form.watch('isSensitive');
  const groupId = form.watch('groupId');
  const defaultValue = form.watch('defaultValue');
  const scopedValues = form.watch('scopedValues');
  const name = form.watch('name');
  const pending = createVariable.isPending || updateVariable.isPending;
  const isDeleteConfirmed = !!variable && deleteConfirmText === variable.name;

  const decryptValues = useMutation({
    mutationFn: () => {
      if (!variable) {
        throw new Error('NO_VARIABLE');
      }

      return getVariable(variable.id, { decrypt: true });
    },
    onError: () => toast.error("Couldn't reveal sensitive values."),
    onSuccess: (data) => {
      if (!data) {
        return;
      }

      if (data.values.some((value) => value.value === SENSITIVE_MASK)) {
        toast.error("You don't have permission to reveal sensitive values.");

        return;
      }

      const decrypted = toFormValues({ ...variable!, values: data.values });
      form.reset({
        ...form.getValues(),
        defaultValue: decrypted.defaultValue,
        scopedValues: decrypted.scopedValues,
      });
    },
  });

  const valuesAreMasked = useMemo(() => {
    if (!isEdit || !isSensitive || decryptValues.isSuccess) {
      return false;
    }

    if (defaultValue === SENSITIVE_MASK) {
      return true;
    }

    return scopedValues.some((row) => row.value === SENSITIVE_MASK);
  }, [decryptValues.isSuccess, defaultValue, isEdit, isSensitive, scopedValues]);

  useEffect(() => {
    if (!open) {
      return;
    }

    form.reset(formValues);
    setConfirmingDelete(false);
    setDeleteConfirmText('');
    decryptValues.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, formValues]);

  async function submit(values: VariableFormValues) {
    if (valuesAreMasked) {
      return;
    }

    const trimmedDescription = values.description?.trim() || null;
    const apiValues = toScopedValues(values);

    if (mode === 'create') {
      if (tier.kind === 'project') {
        await createVariable.mutateAsync({
          description: trimmedDescription,
          groupId: null,
          isSensitive: values.isSensitive,
          name: values.name.trim(),
          projectId: tier.projectId,
          scope: 1 as const,
          values: apiValues,
        });
      } else {
        await createVariable.mutateAsync({
          description: trimmedDescription,
          groupId: values.groupId,
          isSensitive: values.isSensitive,
          name: values.name.trim(),
          projectId: null,
          scope: 0 as const,
          values: apiValues,
        });
      }
    } else if (variable) {
      await updateVariable.mutateAsync({
        body: { description: trimmedDescription, isSensitive: values.isSensitive, values: apiValues },
        id: variable.id,
        version: variable.version.toString(),
      });
    }

    onOpenChange(false);
  }

  async function confirmDelete() {
    if (!variable) {
      return;
    }

    await deleteVariable.mutateAsync({ id: variable.id, version: variable.version.toString() });
    setConfirmingDelete(false);
    onOpenChange(false);
  }

  const tierLabel = tier.kind === 'project'
    ? 'Project tier'
    : groupId ? 'Global · group-owned' : 'Global · global';
  const tierDescription = tier.kind === 'project'
    ? 'This variable belongs to one project. It shadows any global with the same name when that project resolves a snapshot.'
    : 'Global variables are shared across projects. Group-owned globals are limited only to their group and projects within that group.';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[min(820px,calc(100vh-32px))] overflow-y-auto w-[min(calc(100vw-32px),720px)]">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'New Variable' : 'Edit Variable'}</DialogTitle>
          <DialogDescription>{tierDescription}</DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={form.handleSubmit(submit)}>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-name">Name</label>
            <Input
              disabled={isEdit}
              id="variable-name"
              placeholder="ApiBase"
              {...form.register('name')}
            />
            <p className="text-[11.5px] text-fg-caption">Reference as <span className="font-mono">{`{{${name?.trim() || 'Name'}}}`}</span> in config entry values.</p>
            {form.formState.errors.name ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.name.message}</p> : null}
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body">Tier</label>
            <div className="rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2 text-[12.5px] text-fg-caption">
              {tierLabel}
            </div>
          </div>

          {tier.kind === 'global' ? (
            <div className="grid gap-1.5">
              <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-group">Group</label>
              {isEdit ? (
                <div className="rounded-lg border border-stroke-subtle bg-bg-container px-3 py-2 text-[12.5px] text-fg-caption [overflow-wrap:anywhere]">
                  {groupId ? groups.data?.data.find((g) => g.id === groupId)?.name ?? groupId : 'Global (no group)'}
                </div>
              ) : (
                <Select
                  onValueChange={(next) => form.setValue('groupId', next === '__system__' ? null : next, { shouldDirty: true })}
                  value={groupId ?? '__system__'}
                >
                  <SelectTrigger id="variable-group"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__system__">Global (no group)</SelectItem>
                    {(groups.data?.data ?? []).map((group) => <SelectItem key={group.id} value={group.id}>{group.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              )}
            </div>
          ) : null}

          <label className="flex items-center gap-2 text-[13px] text-fg-body">
            <input
              className="size-4 accent-[var(--tower-stroke-field-focus)]"
              disabled={valuesAreMasked || decryptValues.isPending}
              type="checkbox"
              {...form.register('isSensitive')}
            />
            Sensitive value
          </label>

          {valuesAreMasked ? (
            <div className="rounded-lg border border-badge-warning-bg bg-badge-warning-bg/30 p-3">
              <div className="text-[12.5px] font-semibold text-badge-warning-fg">Reveal to Edit Values</div>
              <p className="mt-1 text-[11.5px] text-fg-body">
                Values are masked as <span className="font-mono">***</span>. The API rejects saving the mask as a literal value, so reveal first.
              </p>
              <Button
                className="mt-2"
                disabled={decryptValues.isPending}
                onClick={() => decryptValues.mutate()}
                size="sm"
                type="button"
                variant="secondary"
              >
                {decryptValues.isPending ? 'Revealing…' : 'Reveal Sensitive Values'}
              </Button>
            </div>
          ) : null}

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-default-value">Default value</label>
            <Input
              disabled={valuesAreMasked || decryptValues.isPending}
              id="variable-default-value"
              placeholder="Used when no scoped variant matches"
              type={isSensitive ? 'password' : 'text'}
              {...form.register('defaultValue')}
            />
            <p className="text-[11.5px] text-fg-caption">Resolved when the requesting client matches none of the overrides below.</p>
          </div>

          <ScopedValuesField
            control={form.control}
            disabled={valuesAreMasked || decryptValues.isPending}
            isSensitive={isSensitive}
            register={form.register}
            watch={form.watch}
          />

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="variable-description">Description</label>
            <Textarea
              id="variable-description"
              placeholder="Optional context"
              {...form.register('description')}
            />
          </div>

          <DialogFooter className={isEdit ? 'sm:justify-between' : undefined}>
            {isEdit && variable ? (
              <Button
                disabled={pending || deleteVariable.isPending}
                onClick={() => setConfirmingDelete(true)}
                type="button"
                variant="destructive"
              >
                Delete Variable
              </Button>
            ) : null}
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:gap-2">
              <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
              <Button disabled={pending || deleteVariable.isPending || valuesAreMasked} type="submit">
                {pending ? 'Saving…' : 'Save Variable'}
              </Button>
            </div>
          </DialogFooter>
        </form>
      </DialogContent>

      <AlertDialog open={confirmingDelete} onOpenChange={setConfirmingDelete}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {variable?.name ?? 'variable'}?</AlertDialogTitle>
            <AlertDialogDescription>Existing config values that reference this variable may fail to publish. This cannot be undone.</AlertDialogDescription>
          </AlertDialogHeader>
          {variable ? (
            <div className="grid gap-1.5">
              <label className="text-[12px] text-fg-body" htmlFor={deleteConfirmInputId}>
                Type <span className="font-mono font-semibold text-fg-heading">{variable.name}</span> to confirm.
              </label>
              <Input
                autoComplete="off"
                disabled={deleteVariable.isPending}
                id={deleteConfirmInputId}
                onChange={(event) => setDeleteConfirmText(event.target.value)}
                placeholder={variable.name}
                value={deleteConfirmText}
              />
            </div>
          ) : null}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteVariable.isPending}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              disabled={!isDeleteConfirmed || deleteVariable.isPending}
              onClick={(event) => { event.preventDefault(); void confirmDelete(); }}
            >
              {deleteVariable.isPending ? 'Deleting…' : 'Delete'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Dialog>
  );
}

function toFormValues(variable?: Variable): VariableFormValues {
  const defaultScopedValue = variable?.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0);
  const scopedValues = variable?.values
    .filter((value) => value !== defaultScopedValue)
    .map((value) => {
      const [dimension = '', scopeValue = ''] = Object.entries(value.scopes ?? {})[0] ?? [];

      return { dimension, scopeValue, value: value.value };
    }) ?? [];

  return {
    defaultValue: defaultScopedValue?.value ?? '',
    description: variable?.description ?? '',
    groupId: variable?.groupId ?? null,
    isSensitive: variable?.isSensitive ?? false,
    name: variable?.name ?? '',
    scopedValues,
  };
}

function toScopedValues(values: VariableFormValues) {
  return [
    ...(values.defaultValue ? [{ scopes: {}, value: values.defaultValue }] : []),
    ...values.scopedValues
      .filter((row) => row.dimension && row.scopeValue)
      .map((row) => ({ scopes: { [row.dimension!]: row.scopeValue! }, value: row.value })),
  ];
}
