import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useFieldArray, useForm } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useCreateEntry, useUpdateEntry, type ConfigEntry } from '@/queries/useConfigEntries';
import { useScopes } from '@/queries/useScopes';

const valueTypes = ['string', 'number', 'boolean', 'json'] as const;

const entrySchema = z.object({
  defaultValue: z.string(),
  description: z.string().max(500, 'Use 500 characters or fewer').optional(),
  isSensitive: z.boolean(),
  key: z.string().min(1, 'Entry key is required').regex(/^[a-zA-Z0-9._-]+$/, 'Use letters, numbers, dots, underscores, and hyphens only'),
  scopedValues: z.array(z.object({ dimension: z.string().optional(), scopeValue: z.string().optional(), value: z.string() })),
  type: z.enum(valueTypes),
});

type EntryFormValues = z.infer<typeof entrySchema>;

interface EntryModalProps {
  entry?: ConfigEntry;
  mode: 'create' | 'edit';
  onOpenChange: (open: boolean) => void;
  open: boolean;
  projectId: string;
}

export function EntryModal({ entry, mode, onOpenChange, open, projectId }: EntryModalProps) {
  const scopes = useScopes();
  const createEntry = useCreateEntry(projectId);
  const updateEntry = useUpdateEntry(projectId);
  const form = useForm<EntryFormValues>({
    defaultValues: toFormValues(entry),
    resolver: zodResolver(entrySchema),
    values: toFormValues(entry),
  });
  const scopedValues = useFieldArray({ control: form.control, name: 'scopedValues' });
  const isSensitive = form.watch('isSensitive');
  const selectedType = form.watch('type');
  const pending = createEntry.isPending || updateEntry.isPending;

  async function submit(values: EntryFormValues) {
    const body = toRequest(values);

    if (mode === 'create') {
      await createEntry.mutateAsync({ ...body, key: values.key, ownerId: projectId, ownerType: 1 });
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
          <DialogTitle>{mode === 'create' ? 'New entry' : 'Edit entry'}</DialogTitle>
          <DialogDescription>Define the default value and any scope-specific overrides.</DialogDescription>
        </DialogHeader>

        <form className="grid max-h-[70vh] gap-4 overflow-auto pr-1" onSubmit={form.handleSubmit(submit)}>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-key">Key</label>
            <Input disabled={mode === 'edit'} id="entry-key" placeholder="feature.checkout.enabled" {...form.register('key')} />
            {form.formState.errors.key ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.key.message}</p> : null}
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="grid gap-1.5">
              <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-type">Type</label>
              <Controller control={form.control} name="type" render={({ field }) => <Select onValueChange={field.onChange} value={field.value}><SelectTrigger id="entry-type"><SelectValue /></SelectTrigger><SelectContent>{valueTypes.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select>} />
            </div>
            <label className="mt-6 flex h-9 items-center gap-2 rounded-lg border border-stroke-subtle bg-bg-container px-3 text-[13px] text-fg-body">
              <input className="size-4 accent-[var(--tower-stroke-field-focus)]" type="checkbox" {...form.register('isSensitive')} />
              Sensitive value
            </label>
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-default-value">Default value</label>
            <Input id="entry-default-value" inputMode={selectedType === 'number' ? 'decimal' : undefined} type={isSensitive ? 'password' : selectedType === 'number' ? 'number' : 'text'} {...form.register('defaultValue')} />
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="entry-description">Description</label>
            <Textarea id="entry-description" placeholder="Optional context for this key" {...form.register('description')} />
          </div>

          <div className="grid gap-3 rounded-xl border border-stroke-subtle p-4">
            <div className="flex items-center justify-between gap-3">
              <div>
                <div className="text-[13px] font-semibold text-fg-heading">Scoped values</div>
                <div className="text-[11.5px] text-fg-caption">Overrides apply when a client matches the selected scope.</div>
              </div>
              <Button onClick={() => scopedValues.append({ dimension: '', scopeValue: '', value: '' })} type="button" variant="secondary">Add scope override</Button>
            </div>

            {scopedValues.fields.map((field, index) => {
              const dimension = form.watch(`scopedValues.${index}.dimension`);
              const selectedScope = (scopes.data?.data ?? []).find((scope) => scope.dimension === dimension);

              return (
                <div className="grid gap-2 rounded-lg bg-bg-container p-3 md:grid-cols-[1fr_1fr_1.5fr_auto]" key={field.id}>
                  <Controller control={form.control} name={`scopedValues.${index}.dimension`} render={({ field: dimensionField }) => <Select onValueChange={dimensionField.onChange} value={dimensionField.value}><SelectTrigger><SelectValue placeholder="Dimension" /></SelectTrigger><SelectContent>{(scopes.data?.data ?? []).map((scope) => <SelectItem key={scope.id} value={scope.dimension}>{scope.dimension}</SelectItem>)}</SelectContent></Select>} />
                  <Controller control={form.control} name={`scopedValues.${index}.scopeValue`} render={({ field: valueField }) => <Select disabled={!selectedScope} onValueChange={valueField.onChange} value={valueField.value}><SelectTrigger><SelectValue placeholder="Value" /></SelectTrigger><SelectContent>{(selectedScope?.allowedValues ?? []).map((allowedValue) => <SelectItem key={allowedValue} value={allowedValue}>{allowedValue}</SelectItem>)}</SelectContent></Select>} />
                  <Input placeholder="Override value" type={isSensitive ? 'password' : 'text'} {...form.register(`scopedValues.${index}.value`)} />
                  <Button onClick={() => scopedValues.remove(index)} type="button" variant="ghost">Remove</Button>
                </div>
              );
            })}
          </div>

          <DialogFooter>
            <Button disabled={pending} type="submit">{pending ? 'Saving…' : mode === 'create' ? 'Create entry' : 'Save entry'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function toFormValues(entry?: ConfigEntry): EntryFormValues {
  const defaultScopedValue = entry?.values.find((value) => !value.scopes || Object.keys(value.scopes).length === 0);
  const scopedValues = entry?.values.filter((value) => value !== defaultScopedValue).map((value) => {
    const [dimension = '', scopeValue = ''] = Object.entries(value.scopes ?? {})[0] ?? [];

    return { dimension, scopeValue, value: value.value };
  }) ?? [];

  return {
    defaultValue: defaultScopedValue?.value ?? '',
    description: entry?.description ?? '',
    isSensitive: entry?.isSensitive ?? false,
    key: entry?.key ?? '',
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
      { scopes: {}, value: values.defaultValue },
      ...values.scopedValues.filter((value) => value.dimension && value.scopeValue).map((value) => ({ scopes: { [value.dimension!]: value.scopeValue! }, value: value.value })),
    ],
  };
}

function normalizeType(value?: string): EntryFormValues['type'] {
  return valueTypes.includes(value as EntryFormValues['type']) ? value as EntryFormValues['type'] : 'string';
}