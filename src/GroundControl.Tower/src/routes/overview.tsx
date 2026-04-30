import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/overview')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
