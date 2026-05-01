import { useQueries } from '@tanstack/react-query';
import { useMemo } from 'react';
import { getConfigEntries } from '@/api/endpoints/config-entries';
import { configEntriesQueryKey, useConfigEntries, type ConfigEntry } from '@/queries/useConfigEntries';
import { useProjects } from '@/queries/useProjects';
import { useTemplates, type Template } from '@/queries/useTemplates';

export type EntrySource =
  | { kind: 'project' }
  | { kind: 'project-overrides'; templateId: string; templateName: string }
  | { kind: 'template'; templateId: string; templateName: string };

export interface EffectiveEntry {
  entry: ConfigEntry;
  source: EntrySource;
}

export interface EffectiveEntriesResult {
  attachedTemplates: Template[];
  entries: EffectiveEntry[];
  inheritedCount: number;
  isLoading: boolean;
  overrideCount: number;
  ownCount: number;
}

interface TemplateBundle {
  entries: ConfigEntry[];
  templateId: string;
}

interface CombinedTemplateData {
  bundles: TemplateBundle[];
  isLoading: boolean;
}

const EMPTY_RESULT: Omit<EffectiveEntriesResult, 'attachedTemplates' | 'isLoading'> = {
  entries: [],
  inheritedCount: 0,
  overrideCount: 0,
  ownCount: 0,
};

export function useEffectiveEntries(projectId: string): EffectiveEntriesResult {
  const projectEntries = useConfigEntries(projectId, 1);
  const projects = useProjects();
  const templates = useTemplates();
  const project = projects.data?.data.find((candidate) => candidate.id === projectId);
  const templateIds = useMemo(() => project?.templateIds ?? [], [project?.templateIds]);
  const attachedTemplates = useMemo(() => {
    const items = templates.data?.data ?? [];
    return templateIds
      .map((id) => items.find((template) => template.id === id))
      .filter((template): template is Template => template !== undefined);
  }, [templates.data?.data, templateIds]);

  const templateData: CombinedTemplateData = useQueries({
    queries: templateIds.map((templateId) => ({
      enabled: Boolean(templateId),
      queryFn: () => getConfigEntries({ Limit: 100, OwnerId: templateId, OwnerType: 0 as const, SortField: 'key', SortOrder: 'asc', decrypt: false }),
      queryKey: configEntriesQueryKey(templateId, 0),
    })),
    combine: (results): CombinedTemplateData => ({
      bundles: results
        .map((result, index) => ({ entries: result.data?.data ?? [], templateId: templateIds[index] ?? '' }))
        .filter((bundle): bundle is TemplateBundle => bundle.templateId !== ''),
      isLoading: results.some((result) => result.isLoading),
    }),
  });

  const isLoading = projectEntries.isLoading || projects.isLoading || templates.isLoading || templateData.isLoading;

  const result = useMemo(() => {
    const projectItems = projectEntries.data?.data;
    if (!projectItems) {
      return EMPTY_RESULT;
    }

    const projectKeys = new Set(projectItems.map((entry) => entry.key));
    const overriddenBy = new Map<string, { templateId: string; templateName: string }>();
    const inheritedItems: EffectiveEntry[] = [];

    for (const bundle of templateData.bundles) {
      const template = attachedTemplates.find((candidate) => candidate.id === bundle.templateId);
      const templateName = template?.name ?? bundle.templateId;

      for (const entry of bundle.entries) {
        if (projectKeys.has(entry.key)) {
          if (!overriddenBy.has(entry.key)) {
            overriddenBy.set(entry.key, { templateId: bundle.templateId, templateName });
          }
          continue;
        }

        inheritedItems.push({
          entry,
          source: { kind: 'template', templateId: bundle.templateId, templateName },
        });
      }
    }

    const projectEntriesWithSource: EffectiveEntry[] = projectItems.map((entry) => {
      const overrides = overriddenBy.get(entry.key);
      return {
        entry,
        source: overrides ? { kind: 'project-overrides', templateId: overrides.templateId, templateName: overrides.templateName } : { kind: 'project' },
      };
    });

    const merged = [...projectEntriesWithSource, ...inheritedItems].sort((left, right) => left.entry.key.localeCompare(right.entry.key));
    const ownCount = projectEntriesWithSource.length;
    const inheritedCount = inheritedItems.length;
    const overrideCount = projectEntriesWithSource.filter((item) => item.source.kind === 'project-overrides').length;

    return { entries: merged, inheritedCount, overrideCount, ownCount };
  }, [attachedTemplates, projectEntries.data?.data, templateData.bundles]);

  return useMemo(
    () => ({ attachedTemplates, isLoading, ...result }),
    [attachedTemplates, isLoading, result],
  );
}
