import { createFileRoute, Link } from '@tanstack/react-router';
import { ExternalLink } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/tower/data/Badge';
import { SearchFilterPopover } from '@/components/tower/data/SearchFilterPopover';
import { ScopeTag } from '@/components/tower/data/ScopeTag';
import { EditClientModal } from '@/components/tower/clients/EditClientModal';
import { NewClientModal } from '@/components/tower/clients/NewClientModal';
import { PageContent } from '@/components/tower/shell/PageContent';
import { PageHeader } from '@/components/tower/shell/PageHeader';
import { formatRelativeTime } from '@/lib/relative-time';
import { useAllClients, type ClientWithProject } from '@/queries/useAllClients';
import { useProjects } from '@/queries/useProjects';

export const Route = createFileRoute('/clients')({
  component: ClientsRoute,
});

function ClientsRoute() {
  const projects = useProjects();
  const allClients = useAllClients();
  const [search, setSearch] = useState<string | undefined>(undefined);
  const [editingClient, setEditingClient] = useState<ClientWithProject | null>(null);
  const projectNames = useMemo(() => new Map((projects.data?.data ?? []).map((project) => [project.id, project.name])), [projects.data]);

  const filtered = useMemo(() => {
    const needle = search?.trim().toLowerCase();
    if (!needle) {
      return allClients.data;
    }

    return allClients.data.filter((client) => {
      const name = client.name?.toLowerCase() ?? '';
      const projectName = projectNames.get(client.projectId)?.toLowerCase() ?? '';
      return name.includes(needle) || projectName.includes(needle);
    });
  }, [allClients.data, projectNames, search]);

  const totalActive = allClients.data.filter((client) => client.isActive).length;

  return (
    <>
      <PageHeader
        actions={(
          <div className="flex items-center gap-2">
            <SearchFilterPopover
              appliedSearch={search}
              ariaLabel="Filter clients"
              onApply={setSearch}
              placeholder="Client name or project"
            />
            <NewClientModal />
          </div>
        )}
        description={`All credentials issued across projects. ${allClients.data.length} total · ${totalActive} active.`}
        title="Clients"
      />

      <PageContent>
        <div className="grid gap-8 pt-8">
          {allClients.isLoading ? <Skeleton className="h-80" /> : null}
          {!allClients.isLoading && allClients.data.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No clients yet.</div> : null}
          {!allClients.isLoading && allClients.data.length > 0 && filtered.length === 0 ? <div className="rounded-xl border border-stroke-subtle bg-bg-surface p-8 text-center text-fg-caption">No clients match the current filter.</div> : null}
          {filtered.length > 0 ? (
            <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
              <ul className="grid divide-y divide-stroke-subtle">
                {filtered.map((client) => {
                  const projectName = projectNames.get(client.projectId) ?? client.projectId;
                  const scopeEntries = Object.entries(client.scopes);
                  return (
                    <li className={client.isActive ? undefined : 'opacity-60'} key={client.id}>
                      <div
                        className="grid cursor-pointer grid-cols-[minmax(0,1fr)_auto] items-center gap-4 px-[18px] py-[14px] transition-colors hover:bg-bg-container focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-stroke-field-focus"
                        onClick={() => setEditingClient(client)}
                        onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setEditingClient(client); } }}
                        role="button"
                        tabIndex={0}
                      >
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                            <h2 className="font-mono text-[13.5px] font-semibold text-fg-heading [overflow-wrap:anywhere]">{client.name}</h2>
                            <span className="text-[12.5px] text-fg-caption">in</span>
                            <Link
                              className="text-[12.5px] text-fg-link transition-colors hover:underline [overflow-wrap:anywhere]"
                              onClick={(event) => event.stopPropagation()}
                              params={{ projectId: client.projectId }}
                              to="/projects/$projectId/clients"
                            >
                              {projectName}
                            </Link>
                            <Badge variant={client.isActive ? 'success' : 'critical'}>{client.isActive ? 'active' : 'revoked'}</Badge>
                          </div>
                          {scopeEntries.length > 0 ? (
                            <div className="mt-2 flex flex-wrap gap-1.5">
                              {scopeEntries.map(([dimension, value]) => <ScopeTag dimension={dimension} key={dimension} value={value} />)}
                            </div>
                          ) : null}
                          <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-[11.5px] text-fg-caption">
                            <span>Created at: {formatDateTime(client.createdAt)}</span>
                            <span>Updated at: {formatDateTime(client.updatedAt)}</span>
                            {client.expiresAt ? <span>Expires at: {formatDateTime(client.expiresAt)}</span> : null}
                          </div>
                        </div>
                        <div className="flex shrink-0 items-center gap-3">
                          <div className="text-right text-[12px] text-fg-caption">
                            Last used: {client.lastUsedAt ? formatRelativeTime(client.lastUsedAt) : 'never'}
                          </div>
                          <Button asChild aria-label={`Manage clients in ${projectName}`} className="size-8 rounded-full p-0" size="sm" type="button" variant="ghost">
                            <Link
                              onClick={(event) => event.stopPropagation()}
                              params={{ projectId: client.projectId }}
                              title={`Manage clients in ${projectName}`}
                              to="/projects/$projectId/clients"
                            >
                              <ExternalLink aria-hidden="true" className="size-3.5" strokeWidth={1.8} />
                            </Link>
                          </Button>
                        </div>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </div>
          ) : null}
        </div>
      </PageContent>

      <EditClientModal
        client={editingClient}
        onOpenChange={(open) => { if (!open) setEditingClient(null); }}
        open={editingClient !== null}
        projectId={editingClient?.projectId ?? ''}
      />
    </>
  );
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
