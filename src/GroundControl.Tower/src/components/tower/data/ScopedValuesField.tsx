import { Controller, useFieldArray, type ArrayPath, type Control, type FieldValues, type Path, type UseFormRegister, type UseFormWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useScopes } from '@/queries/useScopes';

export interface ScopedValueRow {
  dimension?: string;
  scopeValue?: string;
  value: string;
}

type FormWithScopedValues = FieldValues & { scopedValues: ScopedValueRow[] };

interface ScopedValuesFieldProps<T extends FormWithScopedValues> {
  control: Control<T>;
  description?: string;
  disabled?: boolean;
  isSensitive: boolean;
  register: UseFormRegister<T>;
  title?: string;
  watch: UseFormWatch<T>;
}

export function ScopedValuesField<T extends FormWithScopedValues>({
  control,
  description = 'Overrides apply when a client matches the selected scope.',
  disabled = false,
  isSensitive,
  register,
  title = 'Scoped Values',
  watch,
}: ScopedValuesFieldProps<T>) {
  const scopes = useScopes();
  const dimensions = scopes.data?.data ?? [];
  const scopedValues = useFieldArray({ control, name: 'scopedValues' as ArrayPath<T> });

  return (
    <div className="grid gap-3 rounded-xl border border-stroke-subtle p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="text-[13px] font-semibold text-fg-heading">{title}</div>
          <div className="text-[11.5px] text-fg-caption">{description}</div>
        </div>
        <Button
          disabled={disabled}
          onClick={() => scopedValues.append({ dimension: '', scopeValue: '', value: '' } as ScopedValueRow as Parameters<typeof scopedValues.append>[0])}
          type="button"
          variant="secondary"
        >
          Add scope
        </Button>
      </div>

      {scopedValues.fields.map((field, index) => {
        const dimensionPath = `scopedValues.${index}.dimension` as Path<T>;
        const scopeValuePath = `scopedValues.${index}.scopeValue` as Path<T>;
        const valuePath = `scopedValues.${index}.value` as Path<T>;
        const dimension = watch(dimensionPath) as string | undefined;
        const selectedScope = dimensions.find((scope) => scope.dimension === dimension);

        return (
          <div className="grid gap-2 rounded-lg bg-bg-container p-3 md:grid-cols-[1fr_1fr_1.5fr_auto]" key={field.id}>
            <Controller
              control={control}
              name={dimensionPath}
              render={({ field: dimensionField }) => (
                <Select
                  disabled={disabled}
                  onValueChange={dimensionField.onChange}
                  value={(dimensionField.value as string | undefined) ?? ''}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Dimension" />
                  </SelectTrigger>
                  <SelectContent>
                    {dimensions.map((scope) => (
                      <SelectItem key={scope.id} value={scope.dimension}>{scope.dimension}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            <Controller
              control={control}
              name={scopeValuePath}
              render={({ field: valueField }) => (
                <Select
                  disabled={disabled || !selectedScope}
                  onValueChange={valueField.onChange}
                  value={(valueField.value as string | undefined) ?? ''}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Value" />
                  </SelectTrigger>
                  <SelectContent>
                    {(selectedScope?.allowedValues ?? []).map((allowed) => (
                      <SelectItem key={allowed} value={allowed}>{allowed}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            <Input
              disabled={disabled}
              placeholder="Override value"
              type={isSensitive ? 'password' : 'text'}
              {...register(valuePath)}
            />
            <Button
              disabled={disabled}
              onClick={() => scopedValues.remove(index)}
              type="button"
              variant="ghost"
            >
              Remove
            </Button>
          </div>
        );
      })}
    </div>
  );
}
