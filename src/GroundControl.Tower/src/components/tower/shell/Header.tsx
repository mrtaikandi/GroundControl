import { DropdownMenu, DropdownMenuContent, DropdownMenuLabel, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { NotificationsPopover } from '@/components/tower/shell/NotificationsPopover';
import { SYSTEM_USER_LABEL } from '@/lib/user';
import { useTweaksStore, type Theme } from '@/store/tweaks';
import { Moon, Search, Sun } from 'lucide-react';

export function Header() {
  const theme = useTweaksStore((state) => state.theme);
  const setTheme = useTweaksStore((state) => state.setTheme);
  const userName = SYSTEM_USER_LABEL;
  const userInitial = userName.charAt(0).toUpperCase();

  return (
    <header className="flex h-14 items-center justify-between gap-5 border-b border-stroke-subtle bg-bg-surface px-6">
      <label className="flex h-9 w-full max-w-[540px] items-center gap-2 rounded-lg border border-stroke-field-initial bg-bg-surface px-3 text-fg-caption focus-within:border-stroke-field-focus">
        <Search aria-hidden="true" className="size-4 text-fg-icon-subtle" strokeWidth={1.8} />
        <span className="sr-only">Search</span>
        <input
          className="min-w-0 flex-1 border-0 bg-transparent text-[13px] text-fg-body outline-none placeholder:text-fg-caption"
          placeholder="Search projects, keys, snapshots…"
          type="search"
        />
        <kbd className="rounded-md border border-stroke-subtle bg-bg-container px-1.5 py-0.5 font-mono text-[11px] text-fg-caption">⌘K</kbd>
      </label>

      <div className="flex items-center gap-3">
        <NotificationsPopover />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              aria-label={`Account menu for ${userName}`}
              className="grid size-7 place-items-center rounded-full bg-bg-chip-selected text-[12px] font-semibold text-fg-chip-selected outline-none focus-visible:ring-2 focus-visible:ring-stroke-field-focus"
              type="button"
            >
              {userInitial}
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="min-w-52">
            <DropdownMenuLabel>
              <div className="text-[12.5px] font-medium text-fg-heading">{userName}</div>
              <div className="text-[11px] font-normal text-fg-caption">Signed in</div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuLabel>Theme</DropdownMenuLabel>
            <DropdownMenuRadioGroup onValueChange={(value) => setTheme(value as Theme)} value={theme}>
              <DropdownMenuRadioItem value="light">
                <Sun aria-hidden="true" className="size-4 text-fg-icon-subtle" strokeWidth={1.8} />
                Light
              </DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="dark">
                <Moon aria-hidden="true" className="size-4 text-fg-icon-subtle" strokeWidth={1.8} />
                Dark
              </DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
