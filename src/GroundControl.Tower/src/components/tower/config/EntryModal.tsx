import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { ScopedValuesField } from '@/components/tower/data/ScopedValuesField';
import { getConfigEntry } from '@/api/endpoints/config-entries';
import { useCreateEntry, useUpdateEntry, type ConfigEntry, type ConfigEntryOwnerType } from '@/queries/useConfigEntries';
import { DeleteEntryDialog } from './DeleteEntryDialog';

const SENSITIVE_MASK = '***';
const valueTypes = ['String', 'Int32', 'Int64', 'Double', 'Decimal', 'Boolean', 'DateTime', 'DateTimeOffset', 'DateOnly', 'TimeOnly'] as const;
const integerTypes: ReadonlySet<EntryFormValues['type']> = new Set(['Int32', 'Int64']);
const decimalTypes: ReadonlySet<EntryFormValues['type']> = new Set(['Double', 'Decimal']);

const entrySchema = z.object({
  defaultValue: z.string(),
  description: z.string().max(500, 'Use 500 characters or fewer').optional(),
  isSensitive: z.boolean(),
  key: z.string().min(1, 'Entry key is required').regex(/^[a-zA-Z0-9.:_-]+$/, 'Use letters, numbers, colons, dots, underscores, and hyphens only'),
  scopedValues: z.array(z.object({ dimension: z.string().optional(), scopeValue: z.string().optional(), value: z.string() })),
  type: z.enum(valueTypes),
});

type EntryFormValues = z.infer<typeof entrySchema>;

interface EntryModalProps {
  entry?: ConfigEntry;
  initialKey?: string;
  mode: 'create' | 'edit';
  onOpenChange: (open: boolean) => void;
  open: boolean;
  ownerId?: string;
  ownerType?: ConfigEntryOwnerType;
  projectId?: string;
}

export function EntryModal({ entry, initialKey, mode, onOpenChange, open, ownerId, ownerType = 1, projectId }: EntryModalProps) {
  const resolvedOwnerId = ownerId ?? projectId ?? '';
  const createEntry = useCreateEntry(resolvedOwnerId, ownerType);
  const updateEntry = useUpdateEntry(resolvedOwnerId, ownerType);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const formValues = useMemo(() => toFormValues(entry, initialKey), [entry, initialKey]);
  const form = useForm<EntryFormValues>({
    defaultValues: formValues,
    resolver: zodResolver(entrySchema),
  });
  const isSensitive = form.watch('isSensitive');
  const selectedType = form.watch('type');
  const defaultValue = form.watch('defaultValue');
  const scopedValues = form.watch('scopedValues');
  const isEdit = mode === 'edit';
  const pending = createEntry.isPending || updateEntry.isPending;
  // Recomputed each render — `scopedValues` from `watch` is a fresh array reference, so memoizing buys nothing.
  const valuesAreMasked = isEdit
    && isSensitive
    && (defaultValue === SENSITIVE_MASK || scopedValues.some((row) => row.value === SENSITIVE_MASK));

  const decryptValues = useMutation({
    mutationFn: () => {
      if (!entry) {
        throw new Error('NO_ENTRY');
      }

      return getConfigEntry(entry.id, { decrypt: true });
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

      const decrypted = toFormValues({ ...entry!, values: data.values });
      form.reset({
        ...form.getValues(),
        defaultValue: decrypted.defaultValue,
        scopedValues: decrypted.scopedValues,
      });
    },
  });

  useEffect(() => {
    if (!open) {
      setConfirmingDelete(false);
      return;
    }

    form.reset(formValues);
    decryptValues.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, formValues]);

  async function submit(values: EntryFormValues) {
    if (valuesAreMasked) {
      return;
    }

    const body = toRequest(values);

    if (mode === 'create') {
      await createEntry.mutateAsync({ ...body, key: values.key, ownerId: resolvedOwnerId, ownerType });
    } else if (entry) {
      await updateEntry.mutateAsync({ body, id: entry.id, version: entry.version.toString() });
    }

    form.reset(toFormValues());
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-[min(calc(100vw-32px),720px)]">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'New Configuration' : 'Edit Configuration'}</DialogTitle>
          <DialogDescription>Define the default value and any scope-specific overrides.</DialogDescription>
        </DialogHeader>

        <form className="grid max-h-[70vh] gap-4 overflow-auto pr-1" onSubmit={form.handleSubmit(submit)}>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-key">Key</label>
            <Input disabled={mode === 'edit'} id="entry-key" placeholder="Feature:Checkout:Enabled" {...form.register('key')} />
            {form.formState.errors.key ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.key.message}</p> : null}
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="grid gap-1.5">
              <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-type">Type</label>
              <Controller control={form.control} name="type" render={({ field }) => <Select disabled={valuesAreMasked || decryptValues.isPending} onValueChange={field.onChange} value={field.value}><SelectTrigger id="entry-type"><SelectValue /></SelectTrigger><SelectContent>{valueTypes.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select>} />
            </div>
            <label className="mt-6 flex h-9 items-center gap-2 text-[13px] text-fg-body">
              <input
                className="size-4 accent-[var(--tower-stroke-field-focus)]"
                disabled={valuesAreMasked || decryptValues.isPending}
                type="checkbox"
                {...form.register('isSensitive')}
              />
              Sensitive value
            </label>
          </div>

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
            <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-default-value">Default value</label>
            <Input
              disabled={valuesAreMasked || decryptValues.isPending}
              id="entry-default-value"
              inputMode={integerTypes.has(selectedType) ? 'numeric' : decimalTypes.has(selectedType) ? 'decimal' : undefined}
              type={isSensitive ? 'password' : integerTypes.has(selectedType) || decimalTypes.has(selectedType) ? 'number' : 'text'}
              {...form.register('defaultValue')}
            />
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-description">Description</label>
            <Textarea
              disabled={valuesAreMasked || decryptValues.isPending}
              id="entry-description"
              placeholder="Optional context for this key"
              {...form.register('description')}
            />
          </div>

          <ScopedValuesField
            control={form.control}
            disabled={valuesAreMasked || decryptValues.isPending}
            isSensitive={isSensitive}
            register={form.register}
            watch={form.watch}
          />

          <DialogFooter className={isEdit ? 'sm:justify-between' : undefined}>
            {isEdit && entry ? (
              <Button disabled={pending} onClick={() => setConfirmingDelete(true)} type="button" variant="destructive">Delete entry</Button>
            ) : null}
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:gap-2">
              <Button onClick={() => onOpenChange(false)} type="button" variant="secondary">Cancel</Button>
              <Button disabled={pending || valuesAreMasked} type="submit">{pending ? 'Saving…' : mode === 'create' ? 'Create entry' : 'Save entry'}</Button>
            </div>
          </DialogFooter>
        </form>
      </DialogContent>

      <DeleteEntryDialog
        entry={entry}
        onDeleted={() => onOpenChange(false)}
        onOpenChange={setConfirmingDelete}
        open={confirmingDelete}
        ownerId={ownerId}
        ownerType={ownerType}
        projectId={projectId}
      />
    </Dialog>
  );
}

function toFormValues(entry?: ConfigEntry, initialKey?: string): EntryFormValues {
  const defaultScopedValue = entry?.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0);
  const scopedValues = entry?.values.filter((value) => value !== defaultScopedValue).map((value) => {
    const [dimension = '', scopeValue = ''] = Object.entries(value.scopes ?? {})[0] ?? [];

    return { dimension, scopeValue, value: value.value };
  }) ?? [];

  return {
    defaultValue: defaultScopedValue?.value ?? '',
    description: entry?.description ?? '',
    isSensitive: entry?.isSensitive ?? false,
    key: entry?.key ?? initialKey ?? '',
    scopedValues,
    type: normalizeType(entry?.valueType),
  };
}

function toRequest(values: EntryFormValues) {
  return {
    description: values.description?.trim() ? values.description.trim() : null,
    isSensitive: values.isSensitive,
    valueType: values.type,
    values: [
      ...(values.defaultValue ? [{ scopes: {}, value: values.defaultValue }] : []),
      ...values.scopedValues.filter((value) => value.dimension && value.scopeValue).map((value) => ({ scopes: { [value.dimension!]: value.scopeValue! }, value: value.value })),
    ],
  };
}

function normalizeType(value?: string): EntryFormValues['type'] {
  if (!value) {
    return 'String';
  }

  const match = valueTypes.find((type) => type.toLowerCase() === value.toLowerCase());

  return match ?? 'String';
}