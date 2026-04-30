import { apiFetch, type ApiQuery, type ApiRequestBody, type ApiResponse } from '../client';

export function getGroups(query?: ApiQuery<'ListGroupsHandler'>) {
  return apiFetch<ApiResponse<'ListGroupsHandler'>>('/api/groups', { query });
}

export function createGroup(body: ApiRequestBody<'CreateGroupHandler'>) {
  return apiFetch<ApiResponse<'CreateGroupHandler'>>('/api/groups', { method: 'POST', body });
}

export function getGroup(id: string) {
  return apiFetch<ApiResponse<'GetGroupHandler'>>(`/api/groups/${encodeURIComponent(id)}`);
}

export function updateGroup(id: string, body: ApiRequestBody<'UpdateGroupHandler'>, version: string) {
  return apiFetch<ApiResponse<'UpdateGroupHandler'>>(`/api/groups/${encodeURIComponent(id)}`, { method: 'PUT', body, version });
}

export function deleteGroup(id: string, version: string) {
  return apiFetch<ApiResponse<'DeleteGroupHandler'>>(`/api/groups/${encodeURIComponent(id)}`, { method: 'DELETE', expectType: 'empty', version });
}

export function getGroupMembers(id: string) {
  return apiFetch<ApiResponse<'ListGroupMembersHandler'>>(`/api/groups/${encodeURIComponent(id)}/members`);
}

export function setGroupMember(id: string, userId: string, body: ApiRequestBody<'SetGroupMemberHandler'>) {
  return apiFetch<ApiResponse<'SetGroupMemberHandler'>>(`/api/groups/${encodeURIComponent(id)}/members/${encodeURIComponent(userId)}`, { method: 'PUT', body, expectType: 'empty' });
}

export function removeGroupMember(id: string, userId: string) {
  return apiFetch<ApiResponse<'RemoveGroupMemberHandler'>>(`/api/groups/${encodeURIComponent(id)}/members/${encodeURIComponent(userId)}`, { method: 'DELETE', expectType: 'empty' });
}