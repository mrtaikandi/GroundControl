import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/templates')({
  component: ComingSoon,
});

function ComingSoon() {
  return <div>Coming soon</div>;
}
