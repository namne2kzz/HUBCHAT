-- ─────────────────────────────────────────────────────────────────────────────
-- HUB — seed data for the chat-service database (hub_chat).
-- Workspace: NEXUS KABAN  (00000000-0000-0000-0002-000000000001)
-- 5 channels · all 10 DASHBOARD users · 40 messages with natural timestamps.
--
-- HOW TO RUN:
--   docker exec -i hub-postgres-1 psql -U hub -d hub_chat < seed/hub_chat_seed.sql
--
-- Idempotent: fixed ids + ON CONFLICT DO NOTHING — safe to re-run.
-- Schema (from InitialCreate migration):
--   channels: Id, WorkspaceId, Name, Slug, Type, Topic, IsPrivate, CreatedBy,
--             IsArchived, LinkType(null), LinkExternalId(null), LinkExternalKey(null),
--             LinkUrl, CreatedAt, UpdatedAt(null)
--   channel_members: Id, ChannelId, UserId, Role, Muted, LastReadAt(null), JoinedAt,
--                    CreatedAt, UpdatedAt(null)  -- unique(ChannelId, UserId)
--   messages: Id, ChannelId, ParentId(null), AuthorId, Body, Format, EditedAt(null),
--             DeletedAt(null), mentions(jsonb), CreatedAt, UpdatedAt(null)
-- ─────────────────────────────────────────────────────────────────────────────

DO $$
DECLARE
    -- ── Workspace ────────────────────────────────────────────────────────────
    v_ws uuid := '00000000-0000-0000-0002-000000000001';  -- NEXUS KABAN

    -- ── Users ────────────────────────────────────────────────────────────────
    u_admin  uuid := '00000000-0000-0000-0001-000000000001';  -- Admin User
    u_dev    uuid := '00000000-0000-0000-0001-000000000002';  -- Dev User
    u_conan  uuid := '4D09A83C-E300-42D3-9E7E-880454742C75';  -- Edogawa Conan
    u_kaito  uuid := 'D089748E-9EAF-422C-AF4E-4D62D488D0D1';  -- Kaito Kid
    u_james  uuid := '747E4FB6-FBA3-4F80-A59B-E9B9AE2580B1';  -- James Carter
    u_justin uuid := '7F486574-3496-4C29-A829-625FE15BD652';  -- Justin Bebe
    u_alez   uuid := '9595DAA2-0551-4121-B6B8-114E58BBFE89';  -- Alez Agato
    u_jex    uuid := 'CE8EF67E-4B9A-4618-84E3-D129DAFB9D9D';  -- Jex Jame
    u_tina   uuid := 'A5AEB562-DD1E-49AF-826C-6AAF71CC3D63';  -- Tina Butera
    u_ji     uuid := '19DB1443-BDF1-4CDA-8A84-BE26BE855328';  -- Ji Chang Wok

    -- ── Channels (fixed ids for idempotency) ─────────────────────────────────
    c_general  uuid := 'cccccccc-0000-0000-0000-000000000001';  -- Public
    c_dev      uuid := 'cccccccc-0000-0000-0000-000000000002';  -- Public
    c_product  uuid := 'cccccccc-0000-0000-0000-000000000003';  -- Private
    c_random   uuid := 'cccccccc-0000-0000-0000-000000000004';  -- Public
    c_announce uuid := 'cccccccc-0000-0000-0000-000000000005';  -- Public

    now_ts timestamptz := now();
BEGIN

    -- ── 1. Channels ──────────────────────────────────────────────────────────
    -- Type: 0=Public, 1=Private, 2=Dm, 3=GroupDm
    INSERT INTO channels
        ("Id","WorkspaceId","Name","Slug","Type","Topic","IsPrivate","CreatedBy","IsArchived","LinkUrl","CreatedAt")
    VALUES
        (c_general,  v_ws, 'general',       'general',       0, 'Company-wide chat for everyone',                 false, u_admin, false, '', now_ts - interval '30 days'),
        (c_dev,      v_ws, 'dev-chat',      'dev-chat',      0, 'Engineering discussions, PRs, and code reviews', false, u_dev,   false, '', now_ts - interval '28 days'),
        (c_product,  v_ws, 'product',       'product',       1, 'Product strategy — private to PM & leads',       true,  u_admin, false, '', now_ts - interval '25 days'),
        (c_random,   v_ws, 'random',        'random',        0, 'Off-topic, memes, and water-cooler banter ☕',   false, u_conan, false, '', now_ts - interval '20 days'),
        (c_announce, v_ws, 'announcements', 'announcements', 0, '📢 Official announcements — admins only',        false, u_admin, false, '', now_ts - interval '30 days')
    ON CONFLICT ("Id") DO NOTHING;

    -- ── 2. Channel members ────────────────────────────────────────────────────
    -- Role: 0=Member, 1=Admin, 2=Owner

    -- #general — all 10 users
    INSERT INTO channel_members ("Id","ChannelId","UserId","Role","Muted","JoinedAt","CreatedAt") VALUES
        (gen_random_uuid(), c_general, u_admin,  2, false, now_ts - interval '30 days', now_ts - interval '30 days'),
        (gen_random_uuid(), c_general, u_dev,    1, false, now_ts - interval '30 days', now_ts - interval '30 days'),
        (gen_random_uuid(), c_general, u_conan,  0, false, now_ts - interval '29 days', now_ts - interval '29 days'),
        (gen_random_uuid(), c_general, u_kaito,  0, false, now_ts - interval '29 days', now_ts - interval '29 days'),
        (gen_random_uuid(), c_general, u_james,  0, false, now_ts - interval '28 days', now_ts - interval '28 days'),
        (gen_random_uuid(), c_general, u_justin, 0, false, now_ts - interval '28 days', now_ts - interval '28 days'),
        (gen_random_uuid(), c_general, u_alez,   0, false, now_ts - interval '27 days', now_ts - interval '27 days'),
        (gen_random_uuid(), c_general, u_jex,    0, false, now_ts - interval '27 days', now_ts - interval '27 days'),
        (gen_random_uuid(), c_general, u_tina,   0, false, now_ts - interval '26 days', now_ts - interval '26 days'),
        (gen_random_uuid(), c_general, u_ji,     0, false, now_ts - interval '26 days', now_ts - interval '26 days')
    ON CONFLICT ("ChannelId","UserId") DO NOTHING;

    -- #dev-chat — 7 devs
    INSERT INTO channel_members ("Id","ChannelId","UserId","Role","Muted","JoinedAt","CreatedAt") VALUES
        (gen_random_uuid(), c_dev, u_dev,    2, false, now_ts - interval '28 days', now_ts - interval '28 days'),
        (gen_random_uuid(), c_dev, u_admin,  1, false, now_ts - interval '28 days', now_ts - interval '28 days'),
        (gen_random_uuid(), c_dev, u_conan,  0, false, now_ts - interval '27 days', now_ts - interval '27 days'),
        (gen_random_uuid(), c_dev, u_kaito,  0, false, now_ts - interval '27 days', now_ts - interval '27 days'),
        (gen_random_uuid(), c_dev, u_james,  0, false, now_ts - interval '26 days', now_ts - interval '26 days'),
        (gen_random_uuid(), c_dev, u_alez,   0, false, now_ts - interval '25 days', now_ts - interval '25 days'),
        (gen_random_uuid(), c_dev, u_jex,    0, false, now_ts - interval '24 days', now_ts - interval '24 days')
    ON CONFLICT ("ChannelId","UserId") DO NOTHING;

    -- #product — 5 PM + leads (private)
    INSERT INTO channel_members ("Id","ChannelId","UserId","Role","Muted","JoinedAt","CreatedAt") VALUES
        (gen_random_uuid(), c_product, u_admin,  2, false, now_ts - interval '25 days', now_ts - interval '25 days'),
        (gen_random_uuid(), c_product, u_tina,   1, false, now_ts - interval '25 days', now_ts - interval '25 days'),
        (gen_random_uuid(), c_product, u_ji,     0, false, now_ts - interval '24 days', now_ts - interval '24 days'),
        (gen_random_uuid(), c_product, u_james,  0, false, now_ts - interval '24 days', now_ts - interval '24 days'),
        (gen_random_uuid(), c_product, u_justin, 0, false, now_ts - interval '23 days', now_ts - interval '23 days')
    ON CONFLICT ("ChannelId","UserId") DO NOTHING;

    -- #random — 9 users (no admin)
    INSERT INTO channel_members ("Id","ChannelId","UserId","Role","Muted","JoinedAt","CreatedAt") VALUES
        (gen_random_uuid(), c_random, u_conan,  2, false, now_ts - interval '20 days', now_ts - interval '20 days'),
        (gen_random_uuid(), c_random, u_kaito,  0, false, now_ts - interval '20 days', now_ts - interval '20 days'),
        (gen_random_uuid(), c_random, u_dev,    0, false, now_ts - interval '19 days', now_ts - interval '19 days'),
        (gen_random_uuid(), c_random, u_james,  0, false, now_ts - interval '19 days', now_ts - interval '19 days'),
        (gen_random_uuid(), c_random, u_justin, 0, false, now_ts - interval '18 days', now_ts - interval '18 days'),
        (gen_random_uuid(), c_random, u_alez,   0, false, now_ts - interval '18 days', now_ts - interval '18 days'),
        (gen_random_uuid(), c_random, u_jex,    0, false, now_ts - interval '17 days', now_ts - interval '17 days'),
        (gen_random_uuid(), c_random, u_tina,   0, false, now_ts - interval '17 days', now_ts - interval '17 days'),
        (gen_random_uuid(), c_random, u_ji,     0, false, now_ts - interval '16 days', now_ts - interval '16 days')
    ON CONFLICT ("ChannelId","UserId") DO NOTHING;

    -- #announcements — all 10 users
    INSERT INTO channel_members ("Id","ChannelId","UserId","Role","Muted","JoinedAt","CreatedAt") VALUES
        (gen_random_uuid(), c_announce, u_admin,  2, false, now_ts - interval '30 days', now_ts - interval '30 days'),
        (gen_random_uuid(), c_announce, u_dev,    1, false, now_ts - interval '30 days', now_ts - interval '30 days'),
        (gen_random_uuid(), c_announce, u_conan,  0, false, now_ts - interval '29 days', now_ts - interval '29 days'),
        (gen_random_uuid(), c_announce, u_kaito,  0, false, now_ts - interval '29 days', now_ts - interval '29 days'),
        (gen_random_uuid(), c_announce, u_james,  0, false, now_ts - interval '28 days', now_ts - interval '28 days'),
        (gen_random_uuid(), c_announce, u_justin, 0, false, now_ts - interval '28 days', now_ts - interval '28 days'),
        (gen_random_uuid(), c_announce, u_alez,   0, false, now_ts - interval '27 days', now_ts - interval '27 days'),
        (gen_random_uuid(), c_announce, u_jex,    0, false, now_ts - interval '27 days', now_ts - interval '27 days'),
        (gen_random_uuid(), c_announce, u_tina,   0, false, now_ts - interval '26 days', now_ts - interval '26 days'),
        (gen_random_uuid(), c_announce, u_ji,     0, false, now_ts - interval '26 days', now_ts - interval '26 days')
    ON CONFLICT ("ChannelId","UserId") DO NOTHING;

    -- ── 3. Messages ───────────────────────────────────────────────────────────
    -- Columns: Id, ChannelId, ParentId(null), AuthorId, Body, Format, EditedAt(null),
    --          DeletedAt(null), mentions(jsonb), CreatedAt
    -- Format 0 = Plain

    -- #general (12 messages)
    INSERT INTO messages ("Id","ChannelId","AuthorId","Body","Format","mentions","CreatedAt") VALUES
        ('dddddddd-0000-0000-0001-000000000001', c_general, u_admin,  'Hey everyone! Welcome to Nexus HUB 🎉 This is our team chat — same crew, new home.', 0, '[]', now_ts - interval '29 days 14 hours'),
        ('dddddddd-0000-0000-0001-000000000002', c_general, u_dev,    'Looking good! Finally no more email threads 🙌', 0, '[]', now_ts - interval '29 days 13 hours'),
        ('dddddddd-0000-0000-0001-000000000003', c_general, u_conan,  'This is great. Can we pin important messages?', 0, '[]', now_ts - interval '29 days 12 hours'),
        ('dddddddd-0000-0000-0001-000000000004', c_general, u_admin,  'Yes! Right-click a message for options once we ship that feature 😄', 0, '[]', now_ts - interval '29 days 11 hours 55 minutes'),
        ('dddddddd-0000-0000-0001-000000000005', c_general, u_kaito,  'Sprint 12 planning starts Monday 9am. Block your calendar 📅', 0, '[]', now_ts - interval '7 days 8 hours'),
        ('dddddddd-0000-0000-0001-000000000006', c_general, u_james,  'On it. Should we move the retro to Thursday?', 0, '[]', now_ts - interval '7 days 7 hours 55 minutes'),
        ('dddddddd-0000-0000-0001-000000000007', c_general, u_kaito,  'Thursday works. I''ll update the calendar invite.', 0, '[]', now_ts - interval '7 days 7 hours 50 minutes'),
        ('dddddddd-0000-0000-0001-000000000008', c_general, u_tina,   'Reminder: design review at 2pm today — link in the pinned Figma doc 🎨', 0, '[]', now_ts - interval '2 days 6 hours'),
        ('dddddddd-0000-0000-0001-000000000009', c_general, u_justin, 'Thanks Tina! I''ll be there.', 0, '[]', now_ts - interval '2 days 5 hours 55 minutes'),
        ('dddddddd-0000-0000-0001-000000000010', c_general, u_alez,   'Same, see you at 2 👍', 0, '[]', now_ts - interval '2 days 5 hours 45 minutes'),
        ('dddddddd-0000-0000-0001-000000000011', c_general, u_ji,     'Deploy to staging just completed ✅ All green.', 0, '[]', now_ts - interval '3 hours'),
        ('dddddddd-0000-0000-0001-000000000012', c_general, u_dev,    'Nice work! Production deploy scheduled for 18:00.', 0, '[]', now_ts - interval '2 hours 45 minutes')
    ON CONFLICT ("Id") DO NOTHING;

    -- #dev-chat (10 messages)
    INSERT INTO messages ("Id","ChannelId","AuthorId","Body","Format","mentions","CreatedAt") VALUES
        ('dddddddd-0000-0000-0002-000000000001', c_dev, u_dev,   'Just pushed the infinite-scroll refactor. PR is up — please review when you get a chance 🙏', 0, '[]', now_ts - interval '5 days 10 hours'),
        ('dddddddd-0000-0000-0002-000000000002', c_dev, u_conan, 'On it. Left a couple of comments on the pagination hook.', 0, '[]', now_ts - interval '5 days 9 hours'),
        ('dddddddd-0000-0000-0002-000000000003', c_dev, u_dev,   'Thanks! Fixed both — LGTM?', 0, '[]', now_ts - interval '5 days 8 hours'),
        ('dddddddd-0000-0000-0002-000000000004', c_dev, u_conan, 'LGTM ✅ Merging.', 0, '[]', now_ts - interval '5 days 7 hours 30 minutes'),
        ('dddddddd-0000-0000-0002-000000000005', c_dev, u_kaito, 'Anyone see the flaky test on CI for AuthController? Failing ~30% of runs.', 0, '[]', now_ts - interval '3 days 11 hours'),
        ('dddddddd-0000-0000-0002-000000000006', c_dev, u_alez,  'Yeah, it''s a timing issue in the token refresh test. I''ll take a look.', 0, '[]', now_ts - interval '3 days 10 hours 45 minutes'),
        ('dddddddd-0000-0000-0002-000000000007', c_dev, u_alez,  'Fixed! Added a proper await on the refresh response. Should be green now.', 0, '[]', now_ts - interval '3 days 9 hours'),
        ('dddddddd-0000-0000-0002-000000000008', c_dev, u_jex,   'EF Core migration for labels/components/versions is ready. Anyone want to test locally first?', 0, '[]', now_ts - interval '1 day 14 hours'),
        ('dddddddd-0000-0000-0002-000000000009', c_dev, u_dev,   'I''ll spin it up now.', 0, '[]', now_ts - interval '1 day 13 hours 55 minutes'),
        ('dddddddd-0000-0000-0002-000000000010', c_dev, u_dev,   'All migrations applied cleanly. Go for it 👌', 0, '[]', now_ts - interval '1 day 13 hours 20 minutes')
    ON CONFLICT ("Id") DO NOTHING;

    -- #product private (6 messages)
    INSERT INTO messages ("Id","ChannelId","AuthorId","Body","Format","mentions","CreatedAt") VALUES
        ('dddddddd-0000-0000-0003-000000000001', c_product, u_admin,  'Q3 roadmap review: I''ve shared the Notion doc in the description. Please add comments by Friday.', 0, '[]', now_ts - interval '10 days 9 hours'),
        ('dddddddd-0000-0000-0003-000000000002', c_product, u_tina,   'Added comments on the Analytics and Wiki sections. Happy to discuss.', 0, '[]', now_ts - interval '10 days 8 hours'),
        ('dddddddd-0000-0000-0003-000000000003', c_product, u_ji,     'User research for sprint board usability is done. 8/10 satisfaction. Report attached.', 0, '[]', now_ts - interval '6 days 11 hours'),
        ('dddddddd-0000-0000-0003-000000000004', c_product, u_james,  'Excellent result 🎉 That should justify the infinite-scroll work we merged.', 0, '[]', now_ts - interval '6 days 10 hours'),
        ('dddddddd-0000-0000-0003-000000000005', c_product, u_admin,  'Agreed. Let''s surface these metrics in the next all-hands.', 0, '[]', now_ts - interval '6 days 9 hours 30 minutes'),
        ('dddddddd-0000-0000-0003-000000000006', c_product, u_justin, 'Should we also track time-to-first-message as an engagement metric for HUB?', 0, '[]', now_ts - interval '4 hours')
    ON CONFLICT ("Id") DO NOTHING;

    -- #random (8 messages)
    INSERT INTO messages ("Id","ChannelId","AuthorId","Body","Format","mentions","CreatedAt") VALUES
        ('dddddddd-0000-0000-0004-000000000001', c_random, u_conan,  'First message in #random — rules: no rules 😎', 0, '[]', now_ts - interval '20 days 8 hours'),
        ('dddddddd-0000-0000-0004-000000000002', c_random, u_kaito,  'Does anyone else think tabs > spaces? Asking for a friend 🫣', 0, '[]', now_ts - interval '15 days 11 hours'),
        ('dddddddd-0000-0000-0004-000000000003', c_random, u_dev,    'This conversation is over. Spaces. Final answer.', 0, '[]', now_ts - interval '15 days 10 hours 55 minutes'),
        ('dddddddd-0000-0000-0004-000000000004', c_random, u_alez,   'EditorConfig solves this. End of debate 😂', 0, '[]', now_ts - interval '15 days 10 hours 50 minutes'),
        ('dddddddd-0000-0000-0004-000000000005', c_random, u_justin, 'Who broke prod? (asking the important questions)', 0, '[]', now_ts - interval '8 days 15 hours'),
        ('dddddddd-0000-0000-0004-000000000006', c_random, u_jex,    'Wasn''t me 👀', 0, '[]', now_ts - interval '8 days 14 hours 58 minutes'),
        ('dddddddd-0000-0000-0004-000000000007', c_random, u_tina,   'Friday vibes 🎶 Anyone else listening to lo-fi while coding?', 0, '[]', now_ts - interval '1 day 15 hours'),
        ('dddddddd-0000-0000-0004-000000000008', c_random, u_ji,     'Already on it 🎧', 0, '[]', now_ts - interval '1 day 14 hours 55 minutes')
    ON CONFLICT ("Id") DO NOTHING;

    -- #announcements (5 messages)
    INSERT INTO messages ("Id","ChannelId","AuthorId","Body","Format","mentions","CreatedAt") VALUES
        ('dddddddd-0000-0000-0005-000000000001', c_announce, u_admin, '🚀 Nexus HUB is now live! Use it for all internal communication going forward.', 0, '[]', now_ts - interval '30 days 9 hours'),
        ('dddddddd-0000-0000-0005-000000000002', c_announce, u_admin, '📋 Sprint 11 has started. Goals and stories are in DASHBOARD — check the Sprint Planning page.', 0, '[]', now_ts - interval '14 days 9 hours'),
        ('dddddddd-0000-0000-0005-000000000003', c_announce, u_admin, '🔒 Reminder: all production access requires approval via the ServiceDesk ticket. No exceptions.', 0, '[]', now_ts - interval '10 days 8 hours'),
        ('dddddddd-0000-0000-0005-000000000004', c_announce, u_admin, '✅ Sprint 11 closed. Velocity: 47 points. Great work, team!', 0, '[]', now_ts - interval '7 days 17 hours'),
        ('dddddddd-0000-0000-0005-000000000005', c_announce, u_admin, '📋 Sprint 12 starts Monday. Planning session at 9am — see you there!', 0, '[]', now_ts - interval '2 days 9 hours')
    ON CONFLICT ("Id") DO NOTHING;

    RAISE NOTICE 'Seed complete: 5 channels, 41 members, 41 messages — workspace NEXUS KABAN';
END $$;
