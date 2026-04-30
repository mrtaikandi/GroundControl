import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/admin/groups')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
