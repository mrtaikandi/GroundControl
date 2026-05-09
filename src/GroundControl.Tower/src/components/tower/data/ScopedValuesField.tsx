import { Controller, useFieldArray, type Control, type FieldValues, type UseFormRegister, type UseFormWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useScopes } from '@/queries/useScopes';

export interface ScopedValueRow {
  dimension?: string;
  scopeValue?: string;
  value: string;
}

interface ScopedValuesFieldProps<T extends FieldValues> {
  // The parent form must contain a `scopedValues` field array of { dimension, scopeValue, value }.
  control: Control<T>;
  description?: string;
  disabled?: boolean;
  isSensitive: boolean;
  register: UseFormRegister<T>;
  title?: string;
  watch: UseFormWatch<T>;
}

export function ScopedValuesField<T extends FieldValues>({
  control,
  description = 'Overrides apply when a client matches the selected scope.',
  disabled = false,
  isSensitive,
  register,
  title = 'Scoped values',
  watch,
}: ScopedValuesFieldProps<T>) {
  const scopes = useScopes();
  const dimensions = scopes.data?.data ?? [];
  const scopedValues = useFieldArray({ control, name: 'scopedValues' as never });

  return (
    <div className="grid gap-3 rounded-xl border border-stroke-subtle p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="text-[13px] font-semibold text-fg-heading">{title}</div>
          <div className="text-[11.5px] text-fg-caption">{description}</div>
        </div>
        <Button
          disabled={disabled}
          onClick={() => scopedValues.append({ dimension: '', scopeValue: '', value: '' } as never)}
          type="button"
          variant="secondary"
        >
          Add scope
        </Button>
      </div>

      {scopedValues.fields.map((field, index) => {
        const dimension = watch(`scopedValues.${index}.dimension` as never) as unknown as string | undefined;
        const selectedScope = dimensions.find((scope) => scope.dimension === dimension);

        return (
          <div className="grid gap-2 rounded-lg bg-bg-container p-3 md:grid-cols-[1fr_1fr_1.5fr_auto]" key={field.id}>
            <Controller
              control={control}
              name={`scopedValues.${index}.dimension` as never}
              render={({ field: dimensionField }) => {
                const stringValue = (dimensionField.value as string | undefined) || undefined;

                return (
                  <Select
                    disabled={disabled}
                    onValueChange={dimensionField.onChange}
                    value={stringValue}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Dimension">{stringValue}</SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {dimensions.map((scope) => (
                        <SelectItem key={scope.id} value={scope.dimension}>{scope.dimension}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                );
              }}
            />
            <Controller
              control={control}
              name={`scopedValues.${index}.scopeValue` as never}
              render={({ field: valueField }) => {
                const stringValue = (valueField.value as string | undefined) || undefined;

                return (
                  <Select
                    disabled={disabled || !selectedScope}
                    onValueChange={valueField.onChange}
                    value={stringValue}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Value">{stringValue}</SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {(selectedScope?.allowedValues ?? []).map((allowed) => (
                        <SelectItem key={allowed} value={allowed}>{allowed}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                );
              }}
            />
            <Input
              disabled={disabled}
              placeholder="Override value"
              type={isSensitive ? 'password' : 'text'}
              {...register(`scopedValues.${index}.value` as never)}
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
