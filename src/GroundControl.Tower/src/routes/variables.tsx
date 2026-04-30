import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/variables')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
