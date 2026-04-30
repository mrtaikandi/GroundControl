import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/projects/$projectId/config')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
