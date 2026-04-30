import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/audit')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
