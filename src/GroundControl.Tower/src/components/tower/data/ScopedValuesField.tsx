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
  const scopesLoaded = scopes.isSuccess;
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
        const scopeValue = watch(scopeValuePath) as string | undefined;
        // Stored entries can use a different case for the dimension key than the canonical scope
        // (validator + index are case-insensitive by collation). Match case-insensitively so the
        // canonical scope still resolves; the SelectItem render preserves the stored case so Radix
        // can match the form's value verbatim.
        const selectedScope = dimensions.find((scope) => scope.dimension.toLowerCase() === dimension?.toLowerCase());
        const dimensionMissing = Boolean(dimension) && !selectedScope;
        const scopeValueMissing = Boolean(scopeValue) && !!selectedScope && !selectedScope.allowedValues.includes(scopeValue!);
        const fallbackDimension = dimensionMissing ? dimension! : null;
        const fallbackScopeValue = !selectedScope && Boolean(scopeValue) ? scopeValue! : scopeValueMissing ? scopeValue! : null;
        // Only label as "(deleted) / (no longer allowed)" once /api/scopes has actually resolved.
        // Otherwise the brief loading window flashes the stored value as deleted.
        const dimensionConfirmedDeleted = scopesLoaded && dimensionMissing;
        const scopeValueConfirmedRemoved = scopesLoaded && scopeValueMissing;

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
                    {dimensions.map((scope) => {
                      // If the stored dimension matches this canonical scope only by case, render
                      // the SelectItem with the stored value so Radix's strict comparison succeeds.
                      // The displayed text stays canonical. Backend write-side normalizes future
                      // saves so this branch is a transitional render for legacy entries.
                      const usesStoredCase = !!dimension
                        && dimension !== scope.dimension
                        && dimension.toLowerCase() === scope.dimension.toLowerCase();
                      const itemValue = usesStoredCase ? dimension! : scope.dimension;

                      return <SelectItem key={scope.id} value={itemValue}>{scope.dimension}</SelectItem>;
                    })}
                    {fallbackDimension ? (
                      <SelectItem key={`__fallback-${fallbackDimension}`} value={fallbackDimension}>
                        {fallbackDimension}{dimensionConfirmedDeleted ? ' (deleted)' : ''}
                      </SelectItem>
                    ) : null}
                  </SelectContent>
                </Select>
              )}
            />
            <Controller
              control={control}
              name={scopeValuePath}
              render={({ field: valueField }) => (
                <Select
                  disabled={disabled || (!selectedScope && !fallbackDimension)}
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
                    {fallbackScopeValue ? (
                      <SelectItem key={`__fallback-${fallbackScopeValue}`} value={fallbackScopeValue}>
                        {fallbackScopeValue}{scopeValueConfirmedRemoved ? ' (no longer allowed)' : ''}
                      </SelectItem>
                    ) : null}
                  </SelectContent>
                </Select>
              )}
            />
            <Input
              disabled={disabled}
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
