import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError } from '@/api/client';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { useCreateUser } from '@/queries/useUsers';

const userSchema = z.object({
  email: z.string().min(1, 'Email is required').max(254, 'Use 254 characters or fewer').email('Enter a valid email address'),
  password: z.string().max(128, 'Use 128 characters or fewer').optional(),
  username: z.string().min(1, 'Username is required').max(100, 'Use 100 characters or fewer'),
});

type UserFormValues = z.infer<typeof userSchema>;

export function NewUserModal() {
  const [open, setOpen] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const createUser = useCreateUser();
  const form = useForm<UserFormValues>({
    defaultValues: { email: '', password: '', username: '' },
    resolver: zodResolver(userSchema),
  });

  function handleOpenChange(next: boolean) {
    if (!next) {
      form.reset({ email: '', password: '', username: '' });
      setSubmitError(null);
    }
    setOpen(next);
  }

  async function submit(values: UserFormValues) {
    setSubmitError(null);
    const password = values.password?.trim();
    try {
      await createUser.mutateAsync({
        email: values.email.trim(),
        password: password ? password : null,
        username: values.username.trim(),
      });
      handleOpenChange(false);
    } catch (error) {
      setSubmitError(extractErrorMessage(error));
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button type="button">New user</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New user</DialogTitle>
          <DialogDescription>Create a user account. Grants can be assigned after the user is created.</DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={form.handleSubmit(submit)}>
          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="user-username">Username</label>
            <Input autoComplete="off" id="user-username" placeholder="ada.lovelace" {...form.register('username')} />
            {form.formState.errors.username ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.username.message}</p> : null}
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="user-email">Email</label>
            <Input autoComplete="off" id="user-email" placeholder="ada@example.com" type="email" {...form.register('email')} />
            {form.formState.errors.email ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.email.message}</p> : null}
          </div>

          <div className="grid gap-1.5">
            <label className="text-[12px] font-medium text-fg-body" htmlFor="user-password">Password</label>
            <Input autoComplete="new-password" id="user-password" placeholder="Leave blank when using external auth" type="password" {...form.register('password')} />
            <div className="text-[11.5px] text-fg-caption">Required when the server is running in BuiltIn authentication mode.</div>
            {form.formState.errors.password ? <p className="text-[11.5px] text-badge-critical-fg">{form.formState.errors.password.message}</p> : null}
          </div>

          {submitError ? <div className="rounded-lg border border-badge-critical-bg bg-badge-critical-bg/20 px-3 py-2 text-[12px] text-badge-critical-fg">{submitError}</div> : null}

          <DialogFooter>
            <Button disabled={createUser.isPending} onClick={() => handleOpenChange(false)} type="button" variant="secondary">Cancel</Button>
            <Button disabled={createUser.isPending} type="submit">{createUser.isPending ? 'Creating…' : 'Create user'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function extractErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string; title?: string; errors?: Record<string, string[]> } | null;

    if (body?.errors) {
      const messages = Object.values(body.errors).flat().filter(Boolean);
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }

    if (body?.detail) {
      return body.detail;
    }

    if (body?.title) {
      return body.title;
    }
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'Unable to create user.';
}
