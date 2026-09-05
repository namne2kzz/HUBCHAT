/** localStorage keys used throughout the app. Centralised to avoid typos. */
export const StorageKeys = {
  accessToken:  'hub_access_token',
  refreshToken: 'hub_refresh_token',
  userProfile:  'hub_user_profile',
  theme:        'hub_theme',
  activeWorkspace: 'hub_active_workspace',
} as const;
