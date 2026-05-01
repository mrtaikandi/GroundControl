import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { createFileRoute } from '@tanstack/react-router';
import { useMemo, useRef, useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PATRevealModal } from '@/components/tower/clients/PATRevealModal';
import { Badge } from '@/components/tower/data/Badge';
import { FilterChip } from '@/components/tower/data/FilterChip';
import { InlineCode } from '@/components/tower/data/InlineCode';
import { useCreateToken, useRevokeToken, useTokens, type Token } from '@/queries/useTokens';

const columnHelper = createColumnHelper<Token>();

export const Route = createFileRoute('/admin/tokens')({
  component: TokensRoute,
});

function TokensRoute() {
  const tokens = useTokens();
  const [tokenToRevoke, setTokenToRevoke] = useState<Token | null>(null);
  const [showRevoked, setShowRevoked] = useState(true);
  const revokedCount = (tokens.data ?? []).filter((token) => token.isRevoked).length;
  const data = useMemo(() => showRevoked ? tokens.data ?? [] : (tokens.data ?? []).filter((token) => !token.isRevoked), [showRevoked, tokens.data]);
  const columns = [
    columnHelper.display({ cell: (info) => <InlineCode>{info.row.original.tokenPrefix}••••••••</InlineCode>, header: 'Prefix', id: 'prefix' }),
    columnHelper.accessor('name', { cell: (info) => <span className="font-medium text-fg-heading">{info.getValue()}</span>, header: 'Name' }),
    columnHelper.display({ cell: (info) => <Permissions permissions={info.row.original.permissions} />, header: 'Permissions', id: 'permissions' }),
    columnHelper.accessor('createdAt', { cell: (info) => formatDate(info.getValue()), header: 'Created' }),
    columnHelper.accessor('lastUsedAt', { cell: (info) => info.getValue() ? formatDate(info.getValue()!) : 'Never', header: 'Last used' }),
    columnHelper.accessor('expiresAt', { cell: (info) => info.getValue() ? formatDate(info.getValue()!) : 'Never', header: 'Expires' }),
    columnHelper.display({ cell: (info) => info.row.original.isRevoked ? <Badge variant="critical">revoked</Badge> : <div className="flex justify-end"><Button onClick={() => setTokenToRevoke(info.row.original)} size="sm" type="button" variant="ghost">Revoke</Button></div>, header: '', id: 'actions' }),
  ];
  const table = useReactTable({ columns, data, getCoreRowModel: getCoreRowModel() });

  return (
    <div className="grid gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[34px] font-bold leading-tight text-fg-heading">Access tokens</h1>
          <p className="mt-2 text-[14.5px] text-fg-caption">Personal access tokens authenticate the admin API. The raw token is only returned once at creation.</p>
        </div>
        <NewTokenModal />
      </div>

      {tokens.isLoading ? <Skeleton className="h-96" /> : (
        <div className="grid gap-3">
          <div className="flex justify-end"><FilterChip count={revokedCount} label="show revoked" onToggle={() => setShowRevoked((current) => !current)} selected={showRevoked} /></div>
          <div className="overflow-hidden rounded-xl border border-stroke-subtle bg-bg-surface">
          <Table>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>{headerGroup.headers.map((header) => <TableHead key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</TableHead>)}</TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {table.getRowModel().rows.map((row) => (
                <TableRow className={row.original.isRevoked ? 'opacity-60' : undefined} key={row.id}>{row.getVisibleCells().map((cell) => <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>)}</TableRow>
              ))}
              {table.getRowModel().rows.length === 0 ? <TableRow><TableCell className="py-10 text-center text-fg-caption" colSpan={columns.length}>No personal access tokens found.</TableCell></TableRow> : null}
            </TableBody>
          </Table>
          </div>
        </div>
      )}

      <RevokeTokenDialog onOpenChange={(open) => { if (!open) { setTokenToRevoke(null); } }} open={tokenToRevoke !== null} token={tokenToRevoke} />
    </div>
  );
}

function NewTokenModal() {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [expiresInDays, setExpiresInDays] = useState('');
  const [permissions, setPermissions] = useState('');
  const [revealOpen, setRevealOpen] = useState(false);
  const rawTokenRef = useRef<string | null>(null);
  const createToken = useCreateToken((rawToken) => {
    rawTokenRef.current = rawToken;
    setRevealOpen(true);
  });

  async function submit() {
    if (!name.trim()) {
      return;
    }

    await createToken.mutateAsync({
      expiresInDays: expiresInDays ? Number(expiresInDays) : null,
      name: name.trim(),
      permissions: parsePermissions(permissions),
    });
    setOpen(false);
    setName('');
    setExpiresInDays('');
    setPermissions('');
  }

  function confirmReveal() {
    rawTokenRef.current = null;
    setRevealOpen(false);
  }

  return (
    <>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger asChild><Button type="button">New token</Button></DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New token</DialogTitle>
            <DialogDescription>Create a personal access token for admin API access.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">Name<Input onChange={(event) => setName(event.target.value)} placeholder="deployment automation" value={name} /></label>
            <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">Expires in days<Input min={1} onChange={(event) => setExpiresInDays(event.target.value)} placeholder="Never" type="number" value={expiresInDays} /></label>
            <label className="grid gap-1.5 text-[12px] font-medium text-fg-caption">Permissions<Input onChange={(event) => setPermissions(event.target.value)} placeholder="inherits caller" value={permissions} /></label>
          </div>
          <DialogFooter>
            <Button disabled={createToken.isPending} onClick={() => setOpen(false)} type="button" variant="secondary">Cancel</Button>
            <Button disabled={!name.trim() || createToken.isPending} onClick={() => { void submit(); }} type="button">Create token</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <PATRevealModal onConfirm={confirmReveal} open={revealOpen} rawToken={rawTokenRef.current ?? ''} />
    </>
  );
}

function RevokeTokenDialog({ onOpenChange, open, token }: { onOpenChange: (open: boolean) => void; open: boolean; token: Token | null }) {
  const revokeToken = useRevokeToken();

  async function confirmRevoke() {
    if (!token) {
      return;
    }

    await revokeToken.mutateAsync(token.id);
    onOpenChange(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Revoke {token?.name ?? 'token'}?</AlertDialogTitle>
          <AlertDialogDescription>This token will immediately stop authenticating requests. This cannot be undone.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={revokeToken.isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction disabled={!token || revokeToken.isPending} onClick={(event) => { event.preventDefault(); void confirmRevoke(); }}>Revoke</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function Permissions({ permissions }: { permissions?: null | string[] }) {
  if (!permissions || permissions.length === 0) {
    return <span className="text-fg-caption">inherits caller</span>;
  }

  return <div className="flex flex-wrap gap-1.5">{permissions.map((permission) => <InlineCode key={permission}>{permission}</InlineCode>)}</div>;
}

function parsePermissions(value: string) {
  const permissions = value.split(',').map((permission) => permission.trim()).filter(Boolean);

  return permissions.length > 0 ? permissions : null;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
