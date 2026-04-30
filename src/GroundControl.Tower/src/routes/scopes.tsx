import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/scopes')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
