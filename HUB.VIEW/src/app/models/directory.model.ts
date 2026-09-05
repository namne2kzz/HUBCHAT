/** Mirrors HUB.DashboardGateway UserProfile (GET /api/v1/directory/users/{id}). */
export interface DirectoryUser {
  id: string;
  name: string;
  email: string;
  avatarClass: string;
  isGlobalAdmin: boolean;
  isDeleted: boolean;
}

/** A repository the user belongs to — in HUB a repository IS a workspace. */
export interface RepositoryMembership {
  repositoryId: string;
  repositoryName: string;
  repositoryCode: string;
  isArchived: boolean;
  roleId: string;
  roleName: string;
  permissions: string[];
  defaultRole: string;
}

/** GET /api/v1/directory/me/memberships. */
export interface UserMemberships {
  userId: string;
  isGlobalAdmin: boolean;
  /** Owning organization (tenant). In HUB the workspace IS the organization. */
  orgId: string;
  orgAlias: string;
  orgName: string;
  repositories: RepositoryMembership[];
}

/** GET /api/v1/directory/work-items/{id}. */
export interface WorkItemContext {
  id: string;
  key: string;
  title: string;
  state: string;
  repositoryId: string;
  repositoryCode: string;
}
