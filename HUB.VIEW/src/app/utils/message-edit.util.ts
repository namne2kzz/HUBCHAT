/**
 * How long after posting the author may still edit or delete their own message — mirrors
 * `Message.AuthorEditWindow` on the server (24h). A moderator is not bound by this.
 */
export const MESSAGE_EDIT_WINDOW_MS = 24 * 60 * 60 * 1000;
