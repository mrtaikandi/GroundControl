export const SYSTEM_USER_ID = '00000000-0000-0000-0000-000000000000';
export const SYSTEM_USER_LABEL = 'Admin';

export function isSystemUser(value: string): boolean {
  return value === SYSTEM_USER_ID;
}

export function formatUserId(value: string): string {
  return isSystemUser(value) ? SYSTEM_USER_LABEL : value;
}
