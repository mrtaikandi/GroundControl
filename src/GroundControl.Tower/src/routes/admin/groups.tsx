import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/admin/groups')({
  component: GroupsLayout,
});

function GroupsLayout() {
  return <Outlet />;
}
