import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/projects/$projectId/clients')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
